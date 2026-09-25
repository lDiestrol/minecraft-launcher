namespace Launcher.Core.Models;

public sealed class LauncherSettings
{
    public string? ServerUrl { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public string? SelectedProfileId { get; set; }

    public int RamMb { get; set; }
}
