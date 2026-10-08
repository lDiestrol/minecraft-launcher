using Launcher.Core.Services;
using Launcher.Core.Validation;

namespace Launcher.Infrastructure.Persistence;

internal static class ProfileLockFiles
{
    public static string GetLocksDirectory(LauncherDataPaths paths)
    {
        Directory.CreateDirectory(paths.RootDirectory);
        EnsureNotReparsePoint(paths.RootDirectory);

        string locksDirectory = Path.Combine(paths.RootDirectory, ".locks");
        Directory.CreateDirectory(locksDirectory);
        EnsureNotReparsePoint(locksDirectory);
        return locksDirectory;
    }

    public static string GetProfilePath(
        LauncherDataPaths paths,
        string profileId,
        string suffix)
    {
        if (!ProfileValueValidator.IsValidProfileId(profileId))
        {
            throw new ArgumentException("Profile id is not safe for a lock file.", nameof(profileId));
        }

        string locksDirectory = GetLocksDirectory(paths);
        string lockPath = ResolveChildPath(locksDirectory, $"{profileId}{suffix}");
        EnsureNotReparsePoint(lockPath);
        return lockPath;
    }

    public static string GetSharedPath(LauncherDataPaths paths, string fileName)
    {
        string locksDirectory = GetLocksDirectory(paths);
        string path = ResolveChildPath(locksDirectory, fileName);
        EnsureNotReparsePoint(path);
        return path;
    }

    public static string ResolveChildPath(string parentDirectory, string fileName)
    {
        string parent = Path.GetFullPath(parentDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string path = Path.GetFullPath(Path.Combine(parent, fileName));
        if (!path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new PackSyncException(
                PackSyncError.UnsafeManagedPath,
                "Путь блокировки Launcher небезопасен.",
                $"Lock path escapes its managed directory: {path}.");
        }

        return path;
    }

    public static void EnsureNotReparsePoint(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new PackSyncException(
                PackSyncError.UnsafeManagedPath,
                "Путь блокировки Launcher проходит через небезопасную ссылку.",
                $"Reparse point is not allowed in lock path: {path}.");
        }
    }

    public static bool IsContention(IOException exception)
    {
        const int sharingViolation = 32;
        const int lockViolation = 33;
        int nativeError = exception.HResult & 0xFFFF;
        return nativeError is sharingViolation or lockViolation;
    }
}
