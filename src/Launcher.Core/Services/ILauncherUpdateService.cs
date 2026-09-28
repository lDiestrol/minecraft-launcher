using Launcher.Core.Models;

namespace Launcher.Core.Services;

public interface ILauncherUpdateService
{
    string CurrentVersion { get; }

    bool IsInstalled { get; }

    bool IsPortable { get; }

    Task<LauncherUpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken);

    Task DownloadUpdateAsync(
        LauncherUpdateInfo update,
        IProgress<int>? progress,
        CancellationToken cancellationToken);

    void ApplyUpdateAndRestart(LauncherUpdateInfo update);
}
