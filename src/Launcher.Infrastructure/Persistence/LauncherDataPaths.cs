namespace Launcher.Infrastructure.Persistence;

public sealed class LauncherDataPaths
{
    public LauncherDataPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MinecraftLauncher");
    }

    public string RootDirectory { get; }

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public string InstancesDirectory => Path.Combine(RootDirectory, "instances");

    public string GetInstanceDirectory(string validatedProfileId) =>
        Path.Combine(InstancesDirectory, validatedProfileId);
}
