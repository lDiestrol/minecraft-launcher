using Launcher.Core.Services;

namespace Launcher.Infrastructure.Pack;

internal static class ManagedPathPolicy
{
    private static readonly HashSet<string> ReservedNames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        ],
        StringComparer.OrdinalIgnoreCase);

    public static string ValidateManifestPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.Contains('\\') ||
            path.Contains('\0') ||
            path.StartsWith('/') ||
            path.EndsWith('/') ||
            Path.IsPathFullyQualified(path) ||
            path.Contains(':'))
        {
            throw Unsafe(path);
        }

        string[] segments = path.Split('/');
        if (segments.Length < 2 ||
            (!segments[0].Equals("mods", StringComparison.OrdinalIgnoreCase) &&
             !segments[0].Equals("config", StringComparison.OrdinalIgnoreCase)))
        {
            throw Unsafe(path);
        }

        foreach (string segment in segments)
        {
            if (segment.Length == 0 ||
                segment is "." or ".." ||
                segment.Equals(".launcher", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(' ') ||
                segment.EndsWith('.') ||
                segment.Any(character => character < ' ') ||
                segment.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0 ||
                IsReservedName(segment))
            {
                throw Unsafe(path);
            }
        }

        return path;
    }

    public static string ResolveDestination(string instanceRoot, string validatedPath)
    {
        string root = Path.GetFullPath(instanceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string destination = Path.GetFullPath(
            Path.Combine(root, validatedPath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw Unsafe(validatedPath);
        }

        EnsureNoReparsePoints(root, destination);
        return destination;
    }

    private static void EnsureNoReparsePoints(string instanceRoot, string destination)
    {
        EnsureNotReparsePoint(instanceRoot);
        string? directory = Path.GetDirectoryName(destination);
        if (directory is null)
        {
            throw Unsafe(destination);
        }

        string relative = Path.GetRelativePath(instanceRoot, directory);
        string current = instanceRoot;
        if (!relative.Equals(".", StringComparison.Ordinal))
        {
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);
                EnsureNotReparsePoint(current);
            }
        }

        EnsureNotReparsePoint(destination);
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new PackSyncException(
                PackSyncError.UnsafeManagedPath,
                "Путь сборки проходит через небезопасную ссылку файловой системы.",
                $"Reparse point is not allowed in managed path: {path}.");
        }
    }

    private static bool IsReservedName(string segment)
    {
        string baseName = segment.Split('.')[0];
        return ReservedNames.Contains(baseName);
    }

    private static PackSyncException Unsafe(string? path) =>
        new(
            PackSyncError.UnsafeManagedPath,
            "Manifest содержит небезопасный путь файла.",
            $"Unsafe managed path: '{path}'.");
}
