using Launcher.Core.Validation;

namespace Launcher.Infrastructure.Persistence;

public sealed class LauncherDataPaths
{
    private readonly bool _requireExistingRoot;

    public LauncherDataPaths(string? rootDirectory = null, bool requireExistingRoot = false)
    {
        string configuredRoot = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MinecraftLauncher");
        RootDirectory = StoragePathPolicy.NormalizeDirectory(configuredRoot);
        _requireExistingRoot = requireExistingRoot;
    }

    public string RootDirectory { get; }

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public string InstancesDirectory => Path.Combine(RootDirectory, "instances");

    public string RuntimeDirectory => Path.Combine(RootDirectory, "runtime");

    public void EnsureRootAvailable()
    {
        StoragePathPolicy.EnsureNoReparsePoints(RootDirectory);
        if (_requireExistingRoot)
        {
            if (!Directory.Exists(RootDirectory))
            {
                throw new StorageRootException(StorageRootError.RootUnavailable,
                    "Выбранный каталог игровых данных недоступен. Другой каталог не выбран.");
            }

            return;
        }

        Directory.CreateDirectory(RootDirectory);
        StoragePathPolicy.EnsureNoReparsePoints(RootDirectory);
    }

    public string GetInstanceDirectory(string profileId)
    {
        if (!ProfileValueValidator.IsValidProfileId(profileId))
        {
            throw new ArgumentException("Profile id is not safe for an instance directory.", nameof(profileId));
        }

        if (_requireExistingRoot)
        {
            EnsureRootAvailable();
        }

        string instanceDirectory = Path.GetFullPath(Path.Combine(InstancesDirectory, profileId));
        if (!IsPathWithin(instanceDirectory, InstancesDirectory))
        {
            throw new InvalidOperationException("Resolved instance directory is outside the launcher instances root.");
        }

        StoragePathPolicy.EnsureNoReparsePoints(instanceDirectory);

        return instanceDirectory;
    }

    public bool IsManagedRuntimePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && IsPathWithin(path, RuntimeDirectory);

    private static bool IsPathWithin(string candidatePath, string parentDirectory)
    {
        string candidate = Path.GetFullPath(candidatePath);
        string parent = Path.GetFullPath(parentDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }
}
