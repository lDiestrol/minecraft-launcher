using System.Globalization;
using Launcher.Infrastructure.Persistence;
using Microsoft.Win32;

namespace Launcher.Infrastructure.Installation;

// Snapshot of the per-user entry, not a request to create or repair registry values ourselves.
internal sealed record InstallationRegistration(string ProductId, IReadOnlyDictionary<string, object?> Values)
{
    internal static InstallationRegistration? Read(string productId)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{productId}");
        if (key is null) return null;
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in key.GetValueNames())
            values[name] = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return new(productId, values);
    }

    internal bool Matches(string productId, string app, string mainExe, string version, string title, string authors)
    {
        string Text(string name) => Values.GetValueOrDefault(name) as string ?? "";
        bool PathMatches(string name, string expected)
        {
            string path = StoragePathPolicy.NormalizeDirectory(Text(name));
            StoragePathPolicy.EnsureNoReparsePoints(path);
            return path.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }
        try
        {
            string update = Path.Combine(app, "Update.exe");
            // Setup161 writes major.minor.patch, without SemVer prerelease/build suffixes.
            string shortVersion = version.Split(['-', '+'])[0];
            return ProductId == productId && PathMatches("InstallLocation", app) &&
                PathMatches("DisplayIcon", Path.Combine(app, "current", mainExe)) &&
                Text("DisplayVersion") == shortVersion && Text("DisplayName") == title &&
                Values.GetValueOrDefault("Publisher") is string && Text("Publisher") == authors &&
                Text("UninstallString").Equals($"\"{update}\" --uninstall", StringComparison.OrdinalIgnoreCase) &&
                Text("QuietUninstallString").Equals($"\"{update}\" --uninstall --silent", StringComparison.OrdinalIgnoreCase) &&
                DateTime.TryParseExact(Text("InstallDate"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
                Values.GetValueOrDefault("EstimatedSize") is int &&
                Values.GetValueOrDefault("NoModify") is int noModify && noModify == 1 &&
                Values.GetValueOrDefault("NoRepair") is int noRepair && noRepair == 1 &&
                Values.GetValueOrDefault("Language") is int language && language == 0x0409;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or StorageRootException)
        { return false; }
    }
}
