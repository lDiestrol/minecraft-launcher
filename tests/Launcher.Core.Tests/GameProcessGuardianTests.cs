using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class GameProcessGuardianTests
{
    [Fact]
    public async Task StartAsync_CancelledBeforeGuardian_DoesNotInvokeStarter()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProcessStartInfo gameStartInfo = CreateGameStartInfo(paths);
        int starterCalls = 0;
        GameProcessGuardianClient client = CreateClient(paths, _ =>
        {
            starterCalls++;
            return Process.GetCurrentProcess();
        });
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.StartAsync("main", gameStartInfo, cancellation.Token));

        Assert.Equal(0, starterCalls);
    }

    [Fact]
    public async Task StartAsync_CancelledWhileAwaitingGuardian_PreventsJavaStart()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProcessStartInfo gameStartInfo = CreateGameStartInfo(paths);
        TaskCompletionSource starterInvoked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource allowGuardian = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>? guardianTask = null;
        string? requestPath = null;
        GameProcessGuardianClient client = CreateClient(paths, path =>
        {
            requestPath = path;
            guardianTask = Task.Run(async () =>
            {
                await allowGuardian.Task;
                return await GameProcessGuardian.RunAsync(paths, path, new NullAppLogger());
            });
            starterInvoked.TrySetResult();
            return Process.GetCurrentProcess();
        });
        using CancellationTokenSource cancellation = new();

        Task<GuardedGameSession> start = client.StartAsync("main", gameStartInfo, cancellation.Token);
        await starterInvoked.Task;
        cancellation.Cancel();
        await WaitForFileAsync(GameProcessGuardian.GetCancellationPath(requestPath!));
        allowGuardian.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        Assert.Equal(5, await guardianTask!);
        Assert.False(File.Exists(new ProfileGameActivity(paths, new NullAppLogger()).GetStatePath("main")));
    }

    [Fact]
    public async Task StartAsync_CancelledAtPreJavaGate_PreventsProcessCreation()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProcessStartInfo gameStartInfo = CreateGameStartInfo(paths);
        TaskCompletionSource beforeJava = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource allowDecision = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>? guardianTask = null;
        string? requestPath = null;
        GameProcessGuardianClient client = CreateClient(paths, path =>
        {
            requestPath = path;
            guardianTask = GameProcessGuardian.RunAsync(
                paths,
                path,
                new NullAppLogger(),
                beforeProcessStart: async () =>
                {
                    beforeJava.TrySetResult();
                    await allowDecision.Task;
                });
            return Process.GetCurrentProcess();
        });
        using CancellationTokenSource cancellation = new();

        Task<GuardedGameSession> start = client.StartAsync("main", gameStartInfo, cancellation.Token);
        await beforeJava.Task;
        cancellation.Cancel();
        await WaitForFileAsync(GameProcessGuardian.GetCancellationPath(requestPath!));
        allowDecision.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        Assert.Equal(5, await guardianTask!);
    }

    [Fact]
    public async Task CancellationAfterGuardianStartDecision_CompletesProtectedHandoff()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProcessStartInfo gameStartInfo = CreateGameStartInfo(paths, pingCount: 2);
        TaskCompletionSource startDecisionMade = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource allowJavaStart = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>? guardianTask = null;
        string? requestPath = null;
        GameProcessGuardianClient client = CreateClient(paths, path =>
        {
            requestPath = path;
            guardianTask = GameProcessGuardian.RunAsync(
                paths,
                path,
                new NullAppLogger(),
                afterCancellationDecision: async () =>
                {
                    startDecisionMade.TrySetResult();
                    await allowJavaStart.Task;
                });
            return Process.GetCurrentProcess();
        });
        using CancellationTokenSource cancellation = new();

        Task<GuardedGameSession> start = client.StartAsync("main", gameStartInfo, cancellation.Token);
        await startDecisionMade.Task;
        cancellation.Cancel();
        await WaitForFileAsync(GameProcessGuardian.GetCancellationPath(requestPath!));
        allowJavaStart.TrySetResult();

        GuardedGameSession session = await start;
        Assert.True(session.ProcessId > 0);
        Assert.Equal(0, await session.WaitForExitAsync());
        Assert.Equal(0, await guardianTask!);
    }

    [Fact]
    public async Task CancellationAfterJavaStart_CompletesHandoffAndKeepsProfileLockedUntilExit()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProcessStartInfo gameStartInfo = CreateGameStartInfo(paths, pingCount: 3);
        TaskCompletionSource javaStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource allowStatus = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>? guardianTask = null;
        string? requestPath = null;
        int childProcessId = 0;
        GameProcessGuardianClient client = CreateClient(paths, path =>
        {
            requestPath = path;
            guardianTask = GameProcessGuardian.RunAsync(
                paths,
                path,
                new NullAppLogger(),
                afterProcessStart: async process =>
                {
                    childProcessId = process.Id;
                    javaStarted.TrySetResult();
                    await allowStatus.Task;
                });
            return Process.GetCurrentProcess();
        });
        using CancellationTokenSource cancellation = new();

        Task<GuardedGameSession> start = client.StartAsync("main", gameStartInfo, cancellation.Token);
        await javaStarted.Task;
        cancellation.Cancel();
        await WaitForFileAsync(GameProcessGuardian.GetCancellationPath(requestPath!));
        allowStatus.TrySetResult();

        GuardedGameSession session = await start;
        Assert.Equal(childProcessId, session.ProcessId);
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());
        Assert.False(profileLock.TryAcquire("main", out _));

        Assert.Equal(0, await session.WaitForExitAsync());
        Assert.Equal(0, await guardianTask!);
        Assert.True(profileLock.TryAcquire("main", out IDisposable? recoveredLease));
        recoveredLease!.Dispose();
    }

    [Fact]
    public async Task LauncherCloseSimulation_ReleasesOperationHandleButGuardianKeepsProfileProtected()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProcessStartInfo gameStartInfo = CreateGameStartInfo(paths, pingCount: 3);
        string token = Guid.NewGuid().ToString("N");
        string requestPath = WriteRequest(
            paths,
            token,
            gameStartInfo.FileName,
            gameStartInfo.WorkingDirectory,
            gameStartInfo.Arguments);
        FileProfileOperationLock launcherLock = new(paths, new NullAppLogger());
        Assert.True(launcherLock.TryAcquire("main", out IDisposable? launcherOperation));

        Task<int> guardian = GameProcessGuardian.RunAsync(
            paths,
            requestPath,
            new NullAppLogger());
        await WaitForStateAsync(GameProcessGuardian.GetStatusPath(requestPath), "started");
        launcherOperation!.Dispose();

        FileProfileOperationLock reopenedLauncher = new(paths, new NullAppLogger());
        Assert.False(reopenedLauncher.TryAcquire("main", out IDisposable? unsafeLease));
        Assert.Null(unsafeLease);

        Assert.Equal(0, await guardian);
        Assert.True(reopenedLauncher.TryAcquire("main", out IDisposable? recoveredLease));
        recoveredLease!.Dispose();
    }

    [Fact]
    public async Task RunAsync_HoldsProfileHandleUntilChildExitAndCleansPersistentState()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        string instanceDirectory = paths.GetInstanceDirectory("main");
        Directory.CreateDirectory(instanceDirectory);
        Directory.CreateDirectory(paths.RuntimeDirectory);
        string javaPath = Path.Combine(paths.RuntimeDirectory, "javaw.exe");
        File.Copy(GetCommandInterpreter(), javaPath);
        string token = Guid.NewGuid().ToString("N");
        string requestPath = WriteRequest(
            paths,
            token,
            javaPath,
            instanceDirectory,
            "/d /c \"ping 127.0.0.1 -n 2 > nul\"");

        Task<int> guardian = GameProcessGuardian.RunAsync(
            paths,
            requestPath,
            new NullAppLogger());
        string statusPath = GameProcessGuardian.GetStatusPath(requestPath);
        await WaitForStateAsync(statusPath, "started");

        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());
        Assert.False(profileLock.TryAcquire("main", out IDisposable? blockedLease));
        Assert.Null(blockedLease);

        Assert.Equal(0, await guardian);
        GameGuardianStatus exited = await WaitForStateAsync(statusPath, "exited");
        Assert.Equal(0, exited.ExitCode);
        Assert.True(profileLock.TryAcquire("main", out IDisposable? recoveredLease));
        Assert.False(File.Exists(new ProfileGameActivity(paths, new NullAppLogger()).GetStatePath("main")));
        recoveredLease!.Dispose();
    }

    [Fact]
    public async Task RunAsync_RejectsExecutableOutsideManagedRuntime()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        string instanceDirectory = paths.GetInstanceDirectory("main");
        Directory.CreateDirectory(instanceDirectory);
        string token = Guid.NewGuid().ToString("N");
        string requestPath = WriteRequest(
            paths,
            token,
            GetCommandInterpreter(),
            instanceDirectory,
            "/d /c \"exit /b 0\"");

        int exitCode = await GameProcessGuardian.RunAsync(
            paths,
            requestPath,
            new NullAppLogger());

        Assert.Equal(2, exitCode);
        GameGuardianStatus failed = await WaitForStateAsync(
            GameProcessGuardian.GetStatusPath(requestPath),
            "failed");
        Assert.Contains("managed Java runtime", failed.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--other")]
    [InlineData("--game-guardian")]
    public void TryGetRequestPath_RequiresExactPrivateCommand(string argument)
    {
        Assert.False(GameProcessGuardian.TryGetRequestPath([argument], out string? requestPath));
        Assert.Null(requestPath);
    }

    private static string WriteRequest(
        LauncherDataPaths paths,
        string token,
        string executable,
        string workingDirectory,
        string arguments)
    {
        string requestPath = Path.Combine(
            GameProcessGuardian.GetGuardianDirectory(paths),
            $"{token}.request.json");
        Dictionary<string, string?> environment = new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
        {
            environment[(string)pair.Key] = pair.Value as string;
        }

        GameGuardianRequest request = new(
            1,
            token,
            "main",
            paths.RootDirectory,
            executable,
            arguments,
            [],
            workingDirectory,
            environment,
            false,
            false,
            true);
        File.WriteAllText(requestPath, JsonSerializer.Serialize(request));
        return requestPath;
    }

    private static GameProcessGuardianClient CreateClient(
        LauncherDataPaths paths,
        Func<string, Process> guardianStarter) => new(
            paths,
            new NullAppLogger(),
            guardianStarter,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(10));

    private static ProcessStartInfo CreateGameStartInfo(
        LauncherDataPaths paths,
        int pingCount = 2)
    {
        string instanceDirectory = paths.GetInstanceDirectory("main");
        Directory.CreateDirectory(instanceDirectory);
        Directory.CreateDirectory(paths.RuntimeDirectory);
        string javaPath = Path.Combine(paths.RuntimeDirectory, "javaw.exe");
        File.Copy(GetCommandInterpreter(), javaPath, true);
        return new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = $"/d /c \"ping 127.0.0.1 -n {pingCount} > nul\"",
            WorkingDirectory = instanceDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    private static async Task WaitForFileAsync(string path)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        while (!File.Exists(path))
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static async Task<GameGuardianStatus> WaitForStateAsync(
        string statusPath,
        string expectedState)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (File.Exists(statusPath))
            {
                try
                {
                    GameGuardianStatus? status = JsonSerializer.Deserialize<GameGuardianStatus>(
                        await File.ReadAllTextAsync(statusPath, timeout.Token));
                    if (status?.State.Equals(expectedState, StringComparison.Ordinal) == true)
                    {
                        return status;
                    }
                }
                catch (JsonException)
                {
                }
                catch (IOException)
                {
                }
            }

            await Task.Delay(25, timeout.Token);
        }
    }

    private static string GetCommandInterpreter() =>
        Environment.GetEnvironmentVariable("ComSpec") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MinecraftLauncherGuardianTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
