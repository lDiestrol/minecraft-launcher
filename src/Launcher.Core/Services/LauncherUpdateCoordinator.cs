using Launcher.Core.Models;

namespace Launcher.Core.Services;

public sealed class LauncherUpdateCoordinator
{
    private readonly ILauncherUpdateService _updateService;
    private readonly LauncherOperationCoordinator _launcherOperations;

    public LauncherUpdateCoordinator(
        ILauncherUpdateService updateService,
        LauncherOperationCoordinator launcherOperations)
    {
        _updateService = updateService;
        _launcherOperations = launcherOperations;
    }

    public string CurrentVersion => _updateService.CurrentVersion;

    public bool IsInstalled => _updateService.IsInstalled;

    public bool IsPortable => _updateService.IsPortable;

    public Task<LauncherUpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken) =>
        _updateService.CheckForUpdatesAsync(cancellationToken);

    public Task DownloadUpdateAsync(
        LauncherUpdateInfo update,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        EnsureGameOperationIsIdle();
        return _updateService.DownloadUpdateAsync(update, progress, cancellationToken);
    }

    public void ApplyUpdateAndRestart(LauncherUpdateInfo update)
    {
        EnsureGameOperationIsIdle();
        _updateService.ApplyUpdateAndRestart(update);
    }

    private void EnsureGameOperationIsIdle()
    {
        if (_launcherOperations.IsActive)
        {
            throw new LauncherUpdateException(
                LauncherUpdateError.GameOperationActive,
                "Дождитесь завершения запуска, синхронизации или восстановления игры.",
                "Launcher update was requested while a game operation was active.");
        }
    }
}
