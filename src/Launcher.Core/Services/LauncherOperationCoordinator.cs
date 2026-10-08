using Launcher.Core.Models;

namespace Launcher.Core.Services;

public sealed class LauncherOperationCoordinator
{
    private readonly IPackSyncService _packSyncService;
    private readonly IGameLaunchService _gameLaunchService;
    private readonly IProfileOperationLock _profileOperationLock;
    private int _activeOperations;

    public LauncherOperationCoordinator(
        IPackSyncService packSyncService,
        IGameLaunchService gameLaunchService,
        IProfileOperationLock profileOperationLock)
    {
        _packSyncService = packSyncService;
        _gameLaunchService = gameLaunchService;
        _profileOperationLock = profileOperationLock;
    }

    public bool IsActive => Volatile.Read(ref _activeOperations) != 0;

    public async Task<(PackSyncResult Pack, GameLaunchResult Game)> PlayAsync(
        GameProfile profile,
        GameLaunchRequest launchRequest,
        IProgress<PackSyncProgress> packProgress,
        IProgress<GameLaunchProgress> gameProgress,
        CancellationToken cancellationToken)
    {
        IDisposable operation = Enter(profile.Id);
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
            Exit(operation);
        }
    }

    public async Task<PackSyncResult> RepairAsync(
        GameProfile profile,
        IProgress<PackSyncProgress> progress,
        CancellationToken cancellationToken)
    {
        IDisposable operation = Enter(profile.Id);
        try
        {
            return await _packSyncService.SyncAsync(profile, progress, cancellationToken);
        }
        finally
        {
            Exit(operation);
        }
    }

    public async Task<ManagedPackRemovalResult> RemoveManagedFilesAsync(
        GameProfile profile,
        CancellationToken cancellationToken)
    {
        IDisposable operation = Enter(profile.Id);
        try
        {
            return await _packSyncService.RemoveManagedFilesAsync(profile.Id, cancellationToken);
        }
        finally
        {
            Exit(operation);
        }
    }

    private IDisposable Enter(string profileId)
    {
        if (!_profileOperationLock.TryAcquire(profileId, out IDisposable? lease) || lease is null)
        {
            throw new PackSyncException(
                PackSyncError.AlreadyRunning,
                "Эта игровая сборка уже используется другим окном Launcher или запущенным Minecraft. " +
                "Закройте игру или дождитесь завершения операции.",
                $"Profile operation lock is already held for '{profileId}'.");
        }

        Interlocked.Increment(ref _activeOperations);
        return lease;
    }

    private void Exit(IDisposable operation)
    {
        try
        {
            operation.Dispose();
        }
        finally
        {
            Interlocked.Decrement(ref _activeOperations);
        }
    }
}
