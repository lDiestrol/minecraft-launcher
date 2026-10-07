using Launcher.Core.Models;

namespace Launcher.Core.Services;

public sealed class LauncherUpdateCoordinator
{
    private readonly ILauncherUpdateService _updateService;
    private readonly LauncherOperationCoordinator _launcherOperations;
    private readonly object _checkSync = new();
    private Task<LauncherUpdateInfo?>? _activeCheck;

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

    public Task<LauncherUpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        Task<LauncherUpdateInfo?> check;
        lock (_checkSync)
        {
            if (_activeCheck is null)
            {
                Task<LauncherUpdateInfo?> newCheck =
                    _updateService.CheckForUpdatesAsync(CancellationToken.None);
                _activeCheck = newCheck;
                check = newCheck;
                _ = ClearCompletedCheckAsync(newCheck);
            }
            else
            {
                check = _activeCheck;
            }
        }

        return cancellationToken.CanBeCanceled
            ? check.WaitAsync(cancellationToken)
            : check;
    }

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

    private async Task ClearCompletedCheckAsync(Task<LauncherUpdateInfo?> check)
    {
        try
        {
            await check;
        }
        catch (Exception)
        {
            // The original task remains faulted for every caller; this observer only clears the shared slot.
        }
        finally
        {
            lock (_checkSync)
            {
                if (ReferenceEquals(_activeCheck, check))
                {
                    _activeCheck = null;
                }
            }
        }
    }
}
