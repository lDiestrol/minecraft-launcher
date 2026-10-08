using System.Collections;
using System.Text.Json;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class GameProcessGuardianTests
{
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
