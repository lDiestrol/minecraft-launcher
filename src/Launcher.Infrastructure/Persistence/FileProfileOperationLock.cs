using Launcher.Core.Services;

namespace Launcher.Infrastructure.Persistence;

public sealed class FileProfileOperationLock : IProfileOperationLock
{
    private const int SharingViolation = 32;
    private const int LockViolation = 33;
    private readonly LauncherDataPaths _paths;
    private readonly IAppLogger _logger;

    public FileProfileOperationLock(LauncherDataPaths paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public bool TryAcquire(string profileId, out IDisposable? lease)
    {
        _ = _paths.GetInstanceDirectory(profileId);
        Directory.CreateDirectory(_paths.RootDirectory);
        EnsureNotReparsePoint(_paths.RootDirectory);

        string locksDirectory = Path.Combine(_paths.RootDirectory, ".locks");
        Directory.CreateDirectory(locksDirectory);
        EnsureNotReparsePoint(locksDirectory);

        string lockPath = ResolveLockPath(locksDirectory, profileId);
        EnsureNotReparsePoint(lockPath);
        try
        {
            FileStream stream = new(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
            _logger.Info($"Acquired profile operation lock: profile={profileId}, path={lockPath}.");
            lease = new FileLease(stream, profileId, lockPath, _logger);
            return true;
        }
        catch (IOException exception) when (IsContention(exception))
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
    }

    private static string ResolveLockPath(string locksDirectory, string profileId)
    {
        string root = Path.GetFullPath(locksDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string lockPath = Path.GetFullPath(Path.Combine(root, $"{profileId}.lock"));
        if (!lockPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new PackSyncException(
                PackSyncError.UnsafeManagedPath,
                "Путь блокировки игровой сборки небезопасен.",
                $"Profile lock path escapes the locks root: {lockPath}.");
        }

        return lockPath;
    }

    private static bool IsContention(IOException exception)
    {
        int nativeError = exception.HResult & 0xFFFF;
        return nativeError is SharingViolation or LockViolation;
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new PackSyncException(
                PackSyncError.UnsafeManagedPath,
                "Путь блокировки игровой сборки проходит через небезопасную ссылку.",
                $"Reparse point is not allowed in profile lock path: {path}.");
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
