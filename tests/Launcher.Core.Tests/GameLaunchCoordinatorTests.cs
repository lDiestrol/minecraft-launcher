using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Core.Tests;

public sealed class GameLaunchCoordinatorTests
{
    [Fact]
    public async Task LaunchAsync_RejectsSecondConcurrentLaunch()
    {
        TaskCompletionSource<GameLaunchResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubGameLaunchService service = new((_, _, _) => completion.Task);
        GameLaunchCoordinator coordinator = new(service);

        Task<GameLaunchResult> firstLaunch = coordinator.LaunchAsync(
            Request(),
            new NullProgress(),
            CancellationToken.None);

        Assert.True(coordinator.IsActive);

        GameLaunchException exception = await Assert.ThrowsAsync<GameLaunchException>(
            () => coordinator.LaunchAsync(Request(), new NullProgress(), CancellationToken.None));
        Assert.Equal(GameLaunchError.AlreadyRunning, exception.Error);

        completion.SetResult(Result());
        await firstLaunch;
        Assert.False(coordinator.IsActive);
    }

    [Fact]
    public async Task LaunchAsync_ReleasesGuardAfterFailure()
    {
        int calls = 0;
        StubGameLaunchService service = new((_, _, _) =>
        {
            calls++;
            return calls == 1
                ? Task.FromException<GameLaunchResult>(new InvalidOperationException("failure"))
                : Task.FromResult(Result());
        });
        GameLaunchCoordinator coordinator = new(service);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.LaunchAsync(Request(), new NullProgress(), CancellationToken.None));
        GameLaunchResult result = await coordinator.LaunchAsync(
            Request(),
            new NullProgress(),
            CancellationToken.None);

        Assert.Equal(42, result.ProcessId);
        Assert.False(coordinator.IsActive);
    }

    [Fact]
    public async Task LaunchAsync_ReleasesGuardAfterCancellation()
    {
        StubGameLaunchService service = new((_, _, token) =>
            Task.FromCanceled<GameLaunchResult>(token));
        GameLaunchCoordinator coordinator = new(service);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.LaunchAsync(Request(), new NullProgress(), cancellation.Token));

        Assert.False(coordinator.IsActive);
    }

    private static GameLaunchRequest Request() => new(
        "main", "1.20.1", "fabric", "0.16.14", "Player_1", 4096, "localhost", 25565);

    private static GameLaunchResult Result() => new(42, 0, "fabric-id", "javaw.exe", "instance");

    private sealed class StubGameLaunchService : IGameLaunchService
    {
        private readonly Func<GameLaunchRequest, IProgress<GameLaunchProgress>, CancellationToken, Task<GameLaunchResult>> _launch;

        public StubGameLaunchService(
            Func<GameLaunchRequest, IProgress<GameLaunchProgress>, CancellationToken, Task<GameLaunchResult>> launch)
        {
            _launch = launch;
        }

        public Task<GameLaunchResult> LaunchAsync(
            GameLaunchRequest request,
            IProgress<GameLaunchProgress> progress,
            CancellationToken cancellationToken) => _launch(request, progress, cancellationToken);
    }

    private sealed class NullProgress : IProgress<GameLaunchProgress>
    {
        public void Report(GameLaunchProgress value)
        {
        }
    }
}
