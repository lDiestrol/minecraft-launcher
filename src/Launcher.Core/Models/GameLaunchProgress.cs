namespace Launcher.Core.Models;

public sealed record GameLaunchProgress(
    GameLaunchStage Stage,
    string Message,
    int? TotalFiles = null,
    int? CompletedFiles = null,
    long? TotalBytes = null,
    long? CompletedBytes = null,
    int? ProcessId = null)
{
    public double? Percentage => TotalBytes > 0 && CompletedBytes is not null
        ? Math.Clamp(CompletedBytes.Value * 100d / TotalBytes.Value, 0d, 100d)
        : TotalFiles > 0 && CompletedFiles is not null
            ? Math.Clamp(CompletedFiles.Value * 100d / TotalFiles.Value, 0d, 100d)
            : null;
}
