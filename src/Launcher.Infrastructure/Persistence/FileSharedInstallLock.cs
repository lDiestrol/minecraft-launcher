using System.Diagnostics;
using Launcher.Core.Services;

namespace Launcher.Infrastructure.Persistence;

public sealed class FileSharedInstallLock : ISharedInstallLock
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(100);
    private readonly LauncherDataPaths _paths;
    private readonly IAppLogger _logger;

    public FileSharedInstallLock(LauncherDataPaths paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<IDisposable?> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        string lockPath = ProfileLockFiles.GetSharedPath(_paths, "shared-install.lock");
        long started = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                FileStream stream = new(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.WriteThrough);
                _logger.Info($"Acquired shared Minecraft install lock: path={lockPath}.");
                return new FileLease(stream, lockPath, _logger);
            }
            catch (IOException exception) when (ProfileLockFiles.IsContention(exception))
            {
                TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
                if (elapsed >= timeout)
                {
                    _logger.Info($"Shared Minecraft install lock timed out: path={lockPath}.");
                    return null;
                }

                TimeSpan remaining = timeout - elapsed;
                await Task.Delay(
                    remaining < RetryInterval ? remaining : RetryInterval,
                    cancellationToken);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new GameLaunchException(
                    GameLaunchError.DirectoryAccessDenied,
                    "Нет доступа к общей блокировке установки Minecraft.",
                    $"Access denied while acquiring shared install lock '{lockPath}'.",
                    exception);
            }
            catch (IOException exception)
            {
                throw new GameLaunchException(
                    GameLaunchError.Unknown,
                    "Не удалось проверить общие файлы Minecraft.",
                    $"I/O failure while acquiring shared install lock '{lockPath}'.",
                    exception);
            }
        }
    }

    private sealed class FileLease : IDisposable
    {
        private readonly string _lockPath;
        private readonly IAppLogger _logger;
        private FileStream? _stream;

        public FileLease(FileStream stream, string lockPath, IAppLogger logger)
        {
            _stream = stream;
            _lockPath = lockPath;
            _logger = logger;
        }

        public void Dispose()
        {
            FileStream? stream = Interlocked.Exchange(ref _stream, null);
            if (stream is null)
            {
                return;
            }

            stream.Dispose();
            _logger.Info($"Released shared Minecraft install lock: path={_lockPath}.");
        }
    }
}
