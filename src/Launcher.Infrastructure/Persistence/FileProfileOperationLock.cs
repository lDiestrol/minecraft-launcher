using Launcher.Core.Services;

namespace Launcher.Infrastructure.Persistence;

public sealed class FileProfileOperationLock : IProfileOperationLock
{
    private readonly LauncherDataPaths _paths;
    private readonly IAppLogger _logger;
    private readonly ProfileGameActivity _gameActivity;

    public FileProfileOperationLock(LauncherDataPaths paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
        _gameActivity = new ProfileGameActivity(paths, logger);
    }

    public bool TryAcquire(string profileId, out IDisposable? lease)
    {
        string lockPath = ProfileLockFiles.GetProfilePath(_paths, profileId, ".lock");
        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);

            if (_gameActivity.IsActive(profileId))
            {
                stream.Dispose();
                _logger.Info($"Profile operation rejected because Minecraft is active: profile={profileId}.");
                lease = null;
                return false;
            }

            _logger.Info($"Acquired profile operation lock: profile={profileId}, path={lockPath}.");
            lease = new FileLease(stream, profileId, lockPath, _logger);
            stream = null;
            return true;
        }
        catch (IOException exception) when (ProfileLockFiles.IsContention(exception))
        {
            _logger.Info($"Profile operation lock is busy: profile={profileId}, path={lockPath}.");
            lease = null;
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PackSyncException(
                PackSyncError.DirectoryAccessDenied,
                "Нет доступа к блокировке игровой сборки.",
                $"Access denied while acquiring profile lock '{lockPath}'.",
                exception);
        }
        catch (IOException exception)
        {
            throw new PackSyncException(
                PackSyncError.Unknown,
                "Не удалось проверить, свободна ли игровая сборка.",
                $"I/O failure while acquiring profile lock '{lockPath}'.",
                exception);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    private sealed class FileLease : IDisposable
    {
        private readonly string _profileId;
        private readonly string _lockPath;
        private readonly IAppLogger _logger;
        private FileStream? _stream;

        public FileLease(FileStream stream, string profileId, string lockPath, IAppLogger logger)
        {
            _stream = stream;
            _profileId = profileId;
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
            _logger.Info($"Released profile operation lock: profile={_profileId}, path={_lockPath}.");
        }
    }
}
