using System.Reflection;

namespace Launcher.Core;

public static class LauncherVersion
{
    public static string Current { get; } = GetCurrentVersion();

    private static string GetCurrentVersion()
    {
        Assembly assembly = typeof(LauncherVersion).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "unknown";
    }
}
