namespace Launcher.Infrastructure.Persistence;

internal static class StoragePathPolicy
{
    public static string NormalizeDirectory(string path, Func<string, DriveType>? volumeType = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith(@"\\", StringComparison.Ordinal) || path.Any(char.IsControl))
        {
            throw Unsafe();
        }

        string volume = Path.GetPathRoot(path) ?? throw Unsafe();
        foreach (string segment in path[volume.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                continue;
            }

            string name = segment.Split('.')[0];
            if (segment.EndsWith('.') || segment.EndsWith(' ') ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                (name.Length == 4 && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                                     name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                 name[3] is >= '1' and <= '9'))
            {
                throw Unsafe();
            }
        }

        string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string normalizedVolume = Path.GetPathRoot(normalized) ?? throw Unsafe();
        // Check the canonical namespace, including mixed separators and device aliases.
        if (normalizedVolume.Length != 3 || !char.IsAsciiLetter(normalizedVolume[0]) ||
            normalizedVolume[1] != ':' || normalizedVolume[2] != '\\' ||
            (volumeType?.Invoke(normalizedVolume) ?? new DriveInfo(normalizedVolume).DriveType) == DriveType.Network)
        {
            throw Unsafe();
        }

        return normalized;
    }

    public static bool IsWithinOrEqual(string candidate, string parent) =>
        candidate.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    public static void EnsureNoReparsePoints(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw Unsafe();
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }

            current = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current));
        }
    }

    private static StorageRootException Unsafe() => new(
        StorageRootError.UnsafePath,
        "Каталог игровых данных небезопасен: нужен абсолютный локальный путь без ссылок файловой системы.");
}
