using Launcher.Core.Validation;

namespace Launcher.Infrastructure.Persistence;

public sealed class LauncherDataPaths
{
    public LauncherDataPaths(string? rootDirectory = null)
    {
        string configuredRoot = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MinecraftLauncher");
        RootDirectory = Path.GetFullPath(configuredRoot);
    }

    public string RootDirectory { get; }

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public string InstancesDirectory => Path.Combine(RootDirectory, "instances");

    public string RuntimeDirectory => Path.Combine(RootDirectory, "runtime");

    public string GetInstanceDirectory(string profileId)
    {
        if (!ProfileValueValidator.IsValidProfileId(profileId))
        {
            throw new ArgumentException("Profile id is not safe for an instance directory.", nameof(profileId));
        }

        string instanceDirectory = Path.GetFullPath(Path.Combine(InstancesDirectory, profileId));
        if (!IsPathWithin(instanceDirectory, InstancesDirectory))
        {
            throw new InvalidOperationException("Resolved instance directory is outside the launcher instances root.");
        }

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
