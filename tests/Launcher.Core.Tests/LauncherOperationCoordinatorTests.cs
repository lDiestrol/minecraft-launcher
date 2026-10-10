using System.Diagnostics;
using System.Text;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class LauncherOperationCoordinatorTests
{
    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task PlayAsync_SynchronizesPackBeforeStartingGame(GameLaunchMode mode)
    {
        List<string> calls = [];
        FakePackSyncService pack = new((_, _, _) =>
        {
            calls.Add("pack");
            return Task.FromResult(PackResult());
        });
        FakeGameLaunchService game = new((request, _, _) =>
        {
            Assert.Equal(mode, request.Mode);
            calls.Add("game");
            return Task.FromResult(GameResult());
        });
        LauncherOperationCoordinator coordinator = new(pack, game, new TestProfileOperationLock());

        await coordinator.PlayAsync(
            Profile(),
            Request() with { Mode = mode },
            Progress<PackSyncProgress>(),
            Progress<GameLaunchProgress>(),
            CancellationToken.None);

        Assert.Equal(["pack", "game"], calls);
    }

    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task PlayAsync_DoesNotStartGameWhenPackSyncFails(GameLaunchMode mode)
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
            Request() with { Mode = mode },
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

    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task ConcurrentPlayAndRepairAreRejected(GameLaunchMode mode)
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
            Request() with { Mode = mode },
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

    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task RemoveManagedFilesAsync_IsRejectedWhilePlayIsActive(GameLaunchMode mode)
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
            Request() with { Mode = mode },
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

    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task PlayAsync_HoldsProfileLockUntilMinecraftExits(GameLaunchMode mode)
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
            Request() with { Mode = mode },
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

    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task PlayAsync_RejectsProfileLockedByAnotherProcess_ThenRecovers(GameLaunchMode mode)
    {
        string directory = Path.Combine(Path.GetTempPath(), "launcher-mode-process-lock-tests", Guid.NewGuid().ToString("N"));
        LauncherDataPaths paths = new(directory);
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());
        // Ask the real provider to create/resolve the lock directory first.
        Assert.True(profileLock.TryAcquire("main", out IDisposable? initialLease));
        initialLease!.Dispose();
        string lockPath = Path.Combine(directory, ".locks", "main.lock");
        string escapedPath = lockPath.Replace("'", "''", StringComparison.Ordinal);
        string script = $"$lease = [IO.File]::Open('{escapedPath}', [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); " +
            "try { [Console]::Out.WriteLine('locked'); [Console]::Out.Flush(); [void][Console]::In.ReadLine() } finally { $lease.Dispose() }";
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        using Process owner = Process.Start(startInfo)!;
        FakePackSyncService pack = new((_, _, _) => Task.FromResult(PackResult()));
        FakeGameLaunchService game = new((_, _, _) => Task.FromResult(GameResult()));
        LauncherOperationCoordinator coordinator = new(pack, game, profileLock);
        try
        {
            Assert.Equal("locked", await owner.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
            PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(() => coordinator.PlayAsync(
                Profile(), Request() with { Mode = mode }, Progress<PackSyncProgress>(), Progress<GameLaunchProgress>(), CancellationToken.None));
            Assert.Equal(PackSyncError.AlreadyRunning, exception.Error);
            Assert.Equal(0, pack.CallCount);
            Assert.Equal(0, game.CallCount);
            Assert.False(coordinator.IsActive);

            await owner.StandardInput.WriteLineAsync("release");
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, owner.ExitCode);
            await coordinator.PlayAsync(Profile(), Request() with { Mode = mode },
                Progress<PackSyncProgress>(), Progress<GameLaunchProgress>(), CancellationToken.None);
            Assert.Equal(1, game.CallCount);
            Assert.False(coordinator.IsActive);
        }
        finally
        {
            owner.StandardInput.Close();
            if (!owner.WaitForExit(5000))
            {
                owner.Kill(entireProcessTree: true);
                await owner.WaitForExitAsync();
            }

            Directory.Delete(directory, recursive: true);
        }
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

    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task PlayAsync_FileLockBlocksBothModesAndReleasesAfterCancellation(GameLaunchMode mode)
    {
        string directory = Path.Combine(Path.GetTempPath(), "launcher-mode-lock-tests", Guid.NewGuid().ToString("N"));
        try
        {
            LauncherDataPaths paths = new(directory);
            using CancellationTokenSource cancellation = new();
            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            FakeGameLaunchService game = new((_, _, _) => Task.FromResult(GameResult()));
            LauncherOperationCoordinator first = new(
                new FakePackSyncService(async (_, _, token) =>
                {
                    started.SetResult();
                    await Task.Delay(Timeout.Infinite, token);
                    return PackResult();
                }),
                game,
                new FileProfileOperationLock(paths, new NullAppLogger()));
            LauncherOperationCoordinator second = new(
                new FakePackSyncService((_, _, _) => Task.FromResult(PackResult())),
                game,
                new FileProfileOperationLock(paths, new NullAppLogger()));
            Task<(PackSyncResult Pack, GameLaunchResult Game)> pending = first.PlayAsync(
                Profile(), Request() with { Mode = mode }, Progress<PackSyncProgress>(),
                Progress<GameLaunchProgress>(), cancellation.Token);
            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                foreach (GameLaunchMode blockedMode in Enum.GetValues<GameLaunchMode>())
                {
                    PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(() => second.PlayAsync(
                        Profile(), Request() with { Mode = blockedMode }, Progress<PackSyncProgress>(),
                        Progress<GameLaunchProgress>(), CancellationToken.None));
                    Assert.Equal(PackSyncError.AlreadyRunning, exception.Error);
                }
                Assert.Equal(0, game.CallCount);
            }
            finally
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            Assert.False(first.IsActive);
            await second.PlayAsync(Profile(), Request() with { Mode = mode }, Progress<PackSyncProgress>(),
                Progress<GameLaunchProgress>(), CancellationToken.None);
            Assert.Equal(1, game.CallCount);
            Assert.False(second.IsActive);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task PlayAsync_CancellationAfterSyncDoesNotStartGame(GameLaunchMode mode)
    {
        using CancellationTokenSource cancellation = new();
        FakeGameLaunchService game = new((_, _, _) => Task.FromResult(GameResult()));
        LauncherOperationCoordinator coordinator = new(
            new FakePackSyncService((_, _, _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(PackResult());
            }), game, new TestProfileOperationLock());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.PlayAsync(
            Profile(), Request() with { Mode = mode }, Progress<PackSyncProgress>(),
            Progress<GameLaunchProgress>(), cancellation.Token));
        Assert.Equal(0, game.CallCount);
        Assert.False(coordinator.IsActive);
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
