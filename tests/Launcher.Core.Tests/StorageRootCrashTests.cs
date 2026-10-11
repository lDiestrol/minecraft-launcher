using System.Diagnostics;
using System.Text.Json;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class StorageRootCrashTests
{
    [Theory]
    [InlineData("intent", false)]
    [InlineData("created", false)]
    [InlineData("state", false)]
    [InlineData("intent", true)]
    [InlineData("created", true)]
    [InlineData("state", true)]
    public async Task KilledGuardian_RecoversOnlyAfterAtomicJobIsEmpty(string phase, bool legacy)
    {
        using TemporaryStorage directory = new();
        string local = Path.Combine(directory.Path, "local");
        string app = Path.Combine(directory.Path, "App");
        StorageRootResolver resolver = new(local, app);
        string root = legacy ? resolver.LegacyRootDirectory : Path.Combine(directory.Path, "GameData");
        if (!legacy)
        {
            resolver.ConfigureNewRoot(root);
        }

        StorageRootSession launcher = resolver.OpenSession();
        LauncherDataPaths paths = launcher.Paths;
        string instance = paths.GetInstanceDirectory("main");
        Directory.CreateDirectory(instance);
        string world = Path.Combine(instance, "saves", "integration-world", "level.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(world)!);
        await File.WriteAllTextAsync(world, "user-world-sentinel");
        Directory.CreateDirectory(paths.RuntimeDirectory);
        string executable = Path.Combine(paths.RuntimeDirectory, "javaw.exe");
        File.Copy(Environment.GetEnvironmentVariable("ComSpec")!, executable);
        string release = Path.Combine(instance, "release.txt");
        string script = Path.Combine(instance, "child.cmd");
        await File.WriteAllTextAsync(script,
            "@echo off\r\n:wait\r\nif exist release.txt exit /b 37\r\nping 127.0.0.1 -n 2 >nul\r\ngoto wait\r\n");
        string token = Guid.NewGuid().ToString("N");
        string requestPath = Path.Combine(GameProcessGuardian.GetGuardianDirectory(paths), token + ".request.json");
        GameGuardianRequest request = new(1, token, "main", root, executable, "/d /c child.cmd", [], instance,
            Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(pair => (string)pair.Key, pair => (string?)pair.Value), false, false, true);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request));
        string marker = Path.Combine(directory.Path, "gate.txt");
        ProcessStartInfo host = new("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Launcher.Guardian.TestHost.dll"), local, app, requestPath, phase, marker,
        })
        {
            host.ArgumentList.Add(argument);
        }

        using Process guardian = Process.Start(host)!;
        Process? child = null;
        try
        {
            await WaitUntilAsync(() => File.Exists(marker), guardian);
            ProfileGameActivity activity = new(paths, new NullAppLogger());
            ActiveGameState state = JsonSerializer.Deserialize<ActiveGameState>(File.ReadAllText(activity.GetStatePath("main")))!;
            Assert.Equal(3, state.SchemaVersion);
            Assert.Equal(WindowsGameJob.WitnessContractVersion, state.WitnessContractVersion);
            Assert.Equal(WindowsGameJob.GetName(token), state.JobName);
            Assert.True(activity.IsActive("main")); // Recovery cannot race a paused live creator.
            launcher.Dispose(); // Java must survive the Launcher closing and guardian dying.
            Assert.Equal(StorageRootError.Busy, Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(root)).Error);
            guardian.Kill(); // Not entireProcessTree: simulate just the guardian crashing.
            await guardian.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (phase != "intent")
            {
                int pid = phase == "created" ? int.Parse(File.ReadAllText(marker)) : state.ProcessId;
                child = Process.GetProcessById(pid);
                _ = child.Handle;
                Assert.False(child.HasExited);
                Assert.True(WindowsGameJob.GetActiveProcesses(state.JobName!) > 0);
                Assert.True(activity.IsActive("main"));
                Assert.False(new FileProfileOperationLock(paths, new NullAppLogger()).TryAcquire("main", out _));
                RecordingPipeline pipeline = new(paths);
                LauncherOperationCoordinator coordinator = new(pipeline, pipeline, new FileProfileOperationLock(paths, new NullAppLogger()));
                foreach (GameLaunchMode mode in Enum.GetValues<GameLaunchMode>())
                {
                    PackSyncException blocked = await Assert.ThrowsAsync<PackSyncException>(() => coordinator.PlayAsync(
                        Profile(), LaunchRequest(mode), new Progress<PackSyncProgress>(), new Progress<GameLaunchProgress>(), CancellationToken.None));
                    Assert.Equal(PackSyncError.AlreadyRunning, blocked.Error);
                }
                Assert.Equal(PackSyncError.AlreadyRunning, (await Assert.ThrowsAsync<PackSyncException>(() => coordinator.RepairAsync(
                    Profile(), new Progress<PackSyncProgress>(), CancellationToken.None))).Error);
                Assert.Equal(PackSyncError.AlreadyRunning, (await Assert.ThrowsAsync<PackSyncException>(() => coordinator.RemoveManagedFilesAsync(
                    Profile(), CancellationToken.None))).Error);
                Assert.Empty(pipeline.Calls);
                Assert.False(coordinator.IsActive);
                Assert.False(activity.TryAcquireOwnership("main", out _));
                Assert.Equal(StorageRootError.Busy, Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(root)).Error);
                Assert.True(File.Exists(activity.GetStatePath("main")));
                // A restart is allowed, but its conflicting Play/Repair/Delete remain denied.
                using (StorageRootSession reopened = resolver.OpenSession())
                {
                    Assert.False(new FileProfileOperationLock(reopened.Paths, new NullAppLogger()).TryAcquire("main", out _));
                }

                await File.WriteAllTextAsync(release, "finish");
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(37, child.ExitCode);
                await WaitUntilAsync(() => WindowsGameJob.GetActiveProcesses(state.JobName!) is null or 0, null);
            }
            else
            {
                Assert.Equal(0, state.ProcessId);
                await WaitUntilAsync(() => WindowsGameJob.GetActiveProcesses(state.JobName!) is null, null);
            }

            Assert.False(activity.IsActive("main"));
            Assert.False(File.Exists(activity.GetStatePath("main")));
            Assert.True(new FileProfileOperationLock(paths, new NullAppLogger()).TryAcquire("main", out IDisposable? recovered));
            recovered!.Dispose();
            RecordingPipeline restoredPipeline = new(paths);
            LauncherOperationCoordinator restored = new(restoredPipeline, restoredPipeline, new FileProfileOperationLock(paths, new NullAppLogger()));
            foreach (GameLaunchMode mode in Enum.GetValues<GameLaunchMode>())
            {
                var result = await restored.PlayAsync(Profile(), LaunchRequest(mode), new Progress<PackSyncProgress>(),
                    new Progress<GameLaunchProgress>(), CancellationToken.None);
                Assert.Equal(instance, result.Game.InstanceDirectory);
                Assert.False(restored.IsActive);
            }
            Assert.Equal(["sync", "Server", "sync", "Singleplayer"], restoredPipeline.Calls);
            Assert.Equal("user-world-sentinel", await File.ReadAllTextAsync(world));
            resolver.ConfigureNewRoot(root);
            using StorageRootSession final = resolver.OpenSession();
            Assert.Equal(root, final.Paths.RootDirectory);
        }
        finally
        {
            launcher.Dispose();
            if (!guardian.HasExited)
            {
                guardian.Kill();
                await guardian.WaitForExitAsync();
            }

            if (child is not null)
            {
                if (!child.HasExited)
                {
                    await File.WriteAllTextAsync(release, "finish");
                    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }

                child.Dispose();
            }
        }
    }

    private static GameProfile Profile() => new("main", "Main", "1.20.1", "fabric", "0.16.14", "1.0.0",
        new Uri("https://example.test/manifest.json"), "play.example.test", 25567);

    private static GameLaunchRequest LaunchRequest(GameLaunchMode mode) => new("main", "1.20.1", "fabric", "0.16.14",
        "Player", 4096, "play.example.test", 25567, mode);

    // Real cross-process guardian/activity/locks, with test services only for coordinator
    // dispatch. This is not a Minecraft launch or a pack filesystem transaction.
    private sealed class RecordingPipeline(LauncherDataPaths paths) : IPackSyncService, IGameLaunchService
    {
        public List<string> Calls { get; } = [];

        public Task<PackSyncResult> SyncAsync(GameProfile profile, IProgress<PackSyncProgress> progress, CancellationToken cancellationToken)
        {
            Calls.Add("sync");
            return Task.FromResult(new PackSyncResult(0, 0, 0, 0, 0, "1.0.0"));
        }

        public Task<ManagedPackRemovalResult> RemoveManagedFilesAsync(string profileId, CancellationToken cancellationToken)
        {
            Calls.Add("delete");
            return Task.FromResult(new ManagedPackRemovalResult(0, 0, false));
        }

        public Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request, IProgress<GameLaunchProgress> progress, CancellationToken cancellationToken)
        {
            Calls.Add(request.Mode.ToString());
            return Task.FromResult(new GameLaunchResult(123, 0, "fabric", "javaw.exe", paths.GetInstanceDirectory(request.ProfileId)));
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, Process? owner)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        while (!condition())
        {
            Assert.False(owner?.HasExited ?? false, "Test guardian exited before reaching its gate.");
            await Task.Delay(20, timeout.Token);
        }
    }
}

internal sealed class TemporaryStorage : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LauncherStorageSafety", Guid.NewGuid().ToString("N"));

    public TemporaryStorage() => Directory.CreateDirectory(Path);

    public void Dispose() => Directory.Delete(Path, true);
}
