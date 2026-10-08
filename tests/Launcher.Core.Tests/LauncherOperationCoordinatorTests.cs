using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Core.Tests;

public sealed class LauncherOperationCoordinatorTests
{
    [Fact]
    public async Task PlayAsync_SynchronizesPackBeforeStartingGame()
    {
        List<string> calls = [];
        FakePackSyncService pack = new((_, _, _) =>
        {
            calls.Add("pack");
            return Task.FromResult(PackResult());
        });
        FakeGameLaunchService game = new((_, _, _) =>
        {
            calls.Add("game");
            return Task.FromResult(GameResult());
        });
        LauncherOperationCoordinator coordinator = new(pack, game, new TestProfileOperationLock());

        await coordinator.PlayAsync(
            Profile(),
            Request(),
            Progress<PackSyncProgress>(),
            Progress<GameLaunchProgress>(),
            CancellationToken.None);

        Assert.Equal(["pack", "game"], calls);
    }

    [Fact]
    public async Task PlayAsync_DoesNotStartGameWhenPackSyncFails()
    {
        FakePackSyncService pack = new((_, _, _) => throw new PackSyncException(
            PackSyncError.HashMismatch,
            "bad pack",
            "bad pack"));
        FakeGameLaunchService game = new((_, _, _) => throw new InvalidOperationException(
            "Game must not start."));
        LauncherOperationCoordinator coordinator = new(pack, game, new TestProfileOperationLock());

        await Assert.ThrowsAsync<PackSyncException>(() => coordinator.PlayAsync(
            Profile(),
            Request(),
            Progress<PackSyncProgress>(),
            Progress<GameLaunchProgress>(),
            CancellationToken.None));

        Assert.Equal(0, game.CallCount);
    }

    [Fact]
    public async Task RepairAsync_SynchronizesPackWithoutStartingGame()
    {
        FakePackSyncService pack = new((_, _, _) => Task.FromResult(PackResult()));
        FakeGameLaunchService game = new((_, _, _) => Task.FromResult(GameResult()));
        LauncherOperationCoordinator coordinator = new(pack, game, new TestProfileOperationLock());

        PackSyncResult result = await coordinator.RepairAsync(
            Profile(),
            Progress<PackSyncProgress>(),
            CancellationToken.None);

        Assert.Equal("1.0.0", result.PackVersion);
        Assert.Equal(1, pack.CallCount);
        Assert.Equal(0, game.CallCount);
    }

    [Fact]
    public async Task ConcurrentPlayAndRepairAreRejected()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakePackSyncService pack = new(async (_, _, cancellationToken) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return PackResult();
        });
        FakeGameLaunchService game = new((_, _, _) => Task.FromResult(GameResult()));
        LauncherOperationCoordinator coordinator = new(pack, game, new TestProfileOperationLock());
        Task<(PackSyncResult Pack, GameLaunchResult Game)> play = coordinator.PlayAsync(
            Profile(),
            Request(),
            Progress<PackSyncProgress>(),
            Progress<GameLaunchProgress>(),
            CancellationToken.None);
        await started.Task;

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(() =>
            coordinator.RepairAsync(Profile(), Progress<PackSyncProgress>(), CancellationToken.None));

        Assert.Equal(PackSyncError.AlreadyRunning, exception.Error);
        release.TrySetResult();
        await play;
        Assert.False(coordinator.IsActive);
    }

    [Fact]
    public async Task RemoveManagedFilesAsync_IsRejectedWhilePlayIsActive()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakePackSyncService pack = new(async (_, _, cancellationToken) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return PackResult();
        });
        LauncherOperationCoordinator coordinator = new(
            pack,
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            new TestProfileOperationLock());
        Task<(PackSyncResult Pack, GameLaunchResult Game)> play = coordinator.PlayAsync(
            Profile(),
            Request(),
            Progress<PackSyncProgress>(),
            Progress<GameLaunchProgress>(),
            CancellationToken.None);
        await started.Task;

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(() =>
            coordinator.RemoveManagedFilesAsync(Profile(), CancellationToken.None));

        Assert.Equal(PackSyncError.AlreadyRunning, exception.Error);
        Assert.Equal(0, pack.RemoveCallCount);
        release.TrySetResult();
        await play;
    }

    [Fact]
    public async Task RemoveManagedFilesAsync_IsRejectedWhileRepairIsActive()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakePackSyncService pack = new(async (_, _, cancellationToken) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return PackResult();
        });
        LauncherOperationCoordinator coordinator = new(
            pack,
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            new TestProfileOperationLock());
        Task<PackSyncResult> repair = coordinator.RepairAsync(
            Profile(),
            Progress<PackSyncProgress>(),
            CancellationToken.None);
        await started.Task;

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(() =>
            coordinator.RemoveManagedFilesAsync(Profile(), CancellationToken.None));

        Assert.Equal(PackSyncError.AlreadyRunning, exception.Error);
        Assert.Equal(0, pack.RemoveCallCount);
        release.TrySetResult();
        await repair;
    }

    [Fact]
    public async Task IndependentCoordinators_RejectSameProfileWithoutWaiting()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestProfileOperationLock sharedLock = new();
        FakePackSyncService firstPack = new(async (_, _, cancellationToken) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return PackResult();
        });
        LauncherOperationCoordinator first = new(
            firstPack,
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            sharedLock);
        LauncherOperationCoordinator second = new(
            new FakePackSyncService((_, _, _) => Task.FromResult(PackResult())),
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            sharedLock);
        Task<PackSyncResult> activeRepair = first.RepairAsync(
            Profile(),
            Progress<PackSyncProgress>(),
            CancellationToken.None);
        await started.Task;

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(() =>
            second.RepairAsync(Profile(), Progress<PackSyncProgress>(), CancellationToken.None));

        Assert.Equal(PackSyncError.AlreadyRunning, exception.Error);
        Assert.Contains("другим окном Launcher", exception.UserMessage, StringComparison.Ordinal);
        release.TrySetResult();
        await activeRepair;
    }

    [Fact]
    public async Task IndependentCoordinators_AllowDifferentProfilesConcurrently()
    {
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestProfileOperationLock sharedLock = new();
        LauncherOperationCoordinator first = new(
            new FakePackSyncService(async (_, _, cancellationToken) =>
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
                return PackResult();
            }),
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            sharedLock);
        LauncherOperationCoordinator second = new(
            new FakePackSyncService((_, _, _) => Task.FromResult(PackResult())),
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            sharedLock);
        Task<PackSyncResult> activeRepair = first.RepairAsync(
            Profile("main"),
            Progress<PackSyncProgress>(),
            CancellationToken.None);
        await firstStarted.Task;

        PackSyncResult otherResult = await second.RepairAsync(
            Profile("other"),
            Progress<PackSyncProgress>(),
            CancellationToken.None);

        Assert.Equal("1.0.0", otherResult.PackVersion);
        Assert.True(first.IsActive);
        Assert.False(second.IsActive);
        releaseFirst.TrySetResult();
        await activeRepair;
    }

    [Fact]
    public async Task PlayAsync_HoldsProfileLockUntilMinecraftExits()
    {
        TaskCompletionSource gameStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource gameExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestProfileOperationLock sharedLock = new();
        LauncherOperationCoordinator playing = new(
            new FakePackSyncService((_, _, _) => Task.FromResult(PackResult())),
            new FakeGameLaunchService(async (_, _, cancellationToken) =>
            {
                gameStarted.TrySetResult();
                await gameExited.Task.WaitAsync(cancellationToken);
                return GameResult();
            }),
            sharedLock);
        LauncherOperationCoordinator repairing = new(
            new FakePackSyncService((_, _, _) => Task.FromResult(PackResult())),
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            sharedLock);
        Task<(PackSyncResult Pack, GameLaunchResult Game)> activePlay = playing.PlayAsync(
            Profile(),
            Request(),
            Progress<PackSyncProgress>(),
            Progress<GameLaunchProgress>(),
            CancellationToken.None);
        await gameStarted.Task;

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(() =>
            repairing.RepairAsync(Profile(), Progress<PackSyncProgress>(), CancellationToken.None));

        Assert.Equal(PackSyncError.AlreadyRunning, exception.Error);
        gameExited.TrySetResult();
        await activePlay;
    }

    [Fact]
    public async Task FailedOperation_ReleasesProfileLock()
    {
        TestProfileOperationLock sharedLock = new();
        LauncherOperationCoordinator failing = new(
            new FakePackSyncService((_, _, _) => throw new PackSyncException(
                PackSyncError.HashMismatch,
                "bad pack",
                "bad pack")),
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            sharedLock);
        LauncherOperationCoordinator succeeding = new(
            new FakePackSyncService((_, _, _) => Task.FromResult(PackResult())),
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            sharedLock);

        await Assert.ThrowsAsync<PackSyncException>(() => failing.RepairAsync(
            Profile(),
            Progress<PackSyncProgress>(),
            CancellationToken.None));

        PackSyncResult result = await succeeding.RepairAsync(
            Profile(),
            Progress<PackSyncProgress>(),
            CancellationToken.None);
        Assert.Equal("1.0.0", result.PackVersion);
    }

    [Fact]
    public async Task CancelledOperation_ReleasesProfileLock()
    {
        TestProfileOperationLock sharedLock = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        LauncherOperationCoordinator cancelled = new(
            new FakePackSyncService((_, _, cancellationToken) =>
                Task.FromCanceled<PackSyncResult>(cancellationToken)),
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            sharedLock);
        LauncherOperationCoordinator succeeding = new(
            new FakePackSyncService((_, _, _) => Task.FromResult(PackResult())),
            new FakeGameLaunchService((_, _, _) => Task.FromResult(GameResult())),
            sharedLock);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.RepairAsync(
            Profile(),
            Progress<PackSyncProgress>(),
            cancellation.Token));

        PackSyncResult result = await succeeding.RepairAsync(
            Profile(),
            Progress<PackSyncProgress>(),
            CancellationToken.None);
        Assert.Equal("1.0.0", result.PackVersion);
    }

    private static GameProfile Profile(string id = "main") => new(
        id,
        "Main",
        "1.20.1",
        "fabric",
        "0.16.14",
        "1.0.0",
        new Uri("https://example.test/manifest.json"),
        "localhost",
        25565);

    private static GameLaunchRequest Request() => new(
        "main",
        "1.20.1",
        "fabric",
        "0.16.14",
        "Player",
        4096,
        "localhost",
        25565);

    private static PackSyncResult PackResult() => new(1, 0, 0, 0, 0, "1.0.0");

    private static GameLaunchResult GameResult() => new(123, 0, "fabric", "javaw.exe", "instance");

    private static IProgress<T> Progress<T>() => new ImmediateProgress<T>();

    private sealed class ImmediateProgress<T> : IProgress<T>
    {
        public void Report(T value)
        {
        }
    }

    private sealed class FakePackSyncService : IPackSyncService
    {
        private readonly Func<GameProfile, IProgress<PackSyncProgress>, CancellationToken, Task<PackSyncResult>> _run;
        private readonly Func<string, CancellationToken, Task<ManagedPackRemovalResult>> _remove;

        public FakePackSyncService(
            Func<GameProfile, IProgress<PackSyncProgress>, CancellationToken, Task<PackSyncResult>> run,
            Func<string, CancellationToken, Task<ManagedPackRemovalResult>>? remove = null)
        {
            _run = run;
            _remove = remove ?? ((_, _) => Task.FromResult(new ManagedPackRemovalResult(0, 0, false)));
        }

        public int CallCount { get; private set; }

        public int RemoveCallCount { get; private set; }

        public Task<PackSyncResult> SyncAsync(
            GameProfile profile,
            IProgress<PackSyncProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _run(profile, progress, cancellationToken);
        }

        public Task<ManagedPackRemovalResult> RemoveManagedFilesAsync(
            string profileId,
            CancellationToken cancellationToken)
        {
            RemoveCallCount++;
            return _remove(profileId, cancellationToken);
        }
    }

    private sealed class TestProfileOperationLock : IProfileOperationLock
    {
        private readonly HashSet<string> _activeProfiles = new(StringComparer.Ordinal);

        public bool TryAcquire(string profileId, out IDisposable? lease)
        {
            lock (_activeProfiles)
            {
                if (!_activeProfiles.Add(profileId))
                {
                    lease = null;
                    return false;
                }
            }

            lease = new CallbackDisposable(() =>
            {
                lock (_activeProfiles)
                {
                    _activeProfiles.Remove(profileId);
                }
            });
            return true;
        }
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private Action? _callback = callback;

        public void Dispose() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }

    private sealed class FakeGameLaunchService : IGameLaunchService
    {
        private readonly Func<GameLaunchRequest, IProgress<GameLaunchProgress>, CancellationToken, Task<GameLaunchResult>> _run;

        public FakeGameLaunchService(
            Func<GameLaunchRequest, IProgress<GameLaunchProgress>, CancellationToken, Task<GameLaunchResult>> run)
        {
            _run = run;
        }

        public int CallCount { get; private set; }

        public Task<GameLaunchResult> LaunchAsync(
            GameLaunchRequest request,
            IProgress<GameLaunchProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _run(request, progress, cancellationToken);
        }
    }
}
