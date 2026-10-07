using Launcher.Core.Models;

namespace Launcher.Core.Services;

public sealed class LauncherOperationCoordinator
{
    private readonly IPackSyncService _packSyncService;
    private readonly IGameLaunchService _gameLaunchService;
    private int _isActive;

    public LauncherOperationCoordinator(
        IPackSyncService packSyncService,
        IGameLaunchService gameLaunchService)
    {
        _packSyncService = packSyncService;
        _gameLaunchService = gameLaunchService;
    }

    public bool IsActive => Volatile.Read(ref _isActive) != 0;

    public async Task<(PackSyncResult Pack, GameLaunchResult Game)> PlayAsync(
        GameProfile profile,
        GameLaunchRequest launchRequest,
        IProgress<PackSyncProgress> packProgress,
        IProgress<GameLaunchProgress> gameProgress,
        CancellationToken cancellationToken)
    {
        Enter();
        try
        {
            PackSyncResult packResult = await _packSyncService.SyncAsync(
                profile,
                packProgress,
                cancellationToken);
            GameLaunchResult gameResult = await _gameLaunchService.LaunchAsync(
                launchRequest,
                gameProgress,
                cancellationToken);
            return (packResult, gameResult);
        }
        finally
        {
            Exit();
        }
    }

    public async Task<PackSyncResult> RepairAsync(
        GameProfile profile,
        IProgress<PackSyncProgress> progress,
        CancellationToken cancellationToken)
    {
        Enter();
        try
        {
            return await _packSyncService.SyncAsync(profile, progress, cancellationToken);
        }
        finally
        {
            Exit();
        }
    }

    private void Enter()
    {
        if (Interlocked.CompareExchange(ref _isActive, 1, 0) != 0)
        {
            throw new PackSyncException(
                PackSyncError.AlreadyRunning,
                "Другая операция Launcher уже выполняется.",
                "A play or repair operation is already active.");
        }
    }

    private void Exit() => Volatile.Write(ref _isActive, 0);
}
