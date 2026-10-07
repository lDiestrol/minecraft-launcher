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
        LauncherOperationCoordinator coordinator = new(pack, game);

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
        LauncherOperationCoordinator coordinator = new(pack, game);

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
        LauncherOperationCoordinator coordinator = new(pack, game);

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
        LauncherOperationCoordinator coordinator = new(pack, game);
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

    private static GameProfile Profile() => new(
        "main",
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

        public FakePackSyncService(
            Func<GameProfile, IProgress<PackSyncProgress>, CancellationToken, Task<PackSyncResult>> run)
        {
            _run = run;
        }

        public int CallCount { get; private set; }

        public Task<PackSyncResult> SyncAsync(
            GameProfile profile,
            IProgress<PackSyncProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _run(profile, progress, cancellationToken);
        }
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
