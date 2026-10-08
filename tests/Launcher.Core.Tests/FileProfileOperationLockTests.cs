using System.Diagnostics;
using Launcher.Core.Services;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class FileProfileOperationLockTests
{
    [Fact]
    public void IndependentProviders_ContendForSameProfileAndRecoverAfterRelease()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        FileProfileOperationLock first = new(paths, new NullAppLogger());
        FileProfileOperationLock second = new(paths, new NullAppLogger());

        Assert.True(first.TryAcquire("main", out IDisposable? firstLease));
        Assert.NotNull(firstLease);
        Assert.False(second.TryAcquire("main", out IDisposable? blockedLease));
        Assert.Null(blockedLease);

        firstLease!.Dispose();

        Assert.True(second.TryAcquire("main", out IDisposable? recoveredLease));
        Assert.NotNull(recoveredLease);
        recoveredLease!.Dispose();
    }

    [Fact]
    public void IndependentProviders_AllowDifferentProfilesConcurrently()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        FileProfileOperationLock first = new(paths, new NullAppLogger());
        FileProfileOperationLock second = new(paths, new NullAppLogger());

        Assert.True(first.TryAcquire("main", out IDisposable? mainLease));
        Assert.True(second.TryAcquire("secondary", out IDisposable? secondaryLease));

        Assert.NotNull(mainLease);
        Assert.NotNull(secondaryLease);
        mainLease!.Dispose();
        secondaryLease!.Dispose();
    }

    [Fact]
    public void ExistingUnlockedFile_IsRecoverableAfterPreviousProcessExit()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        string locksDirectory = Path.Combine(paths.RootDirectory, ".locks");
        Directory.CreateDirectory(locksDirectory);
        File.WriteAllText(Path.Combine(locksDirectory, "main.lock"), "stale lock marker");
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());

        Assert.True(profileLock.TryAcquire("main", out IDisposable? lease));

        Assert.NotNull(lease);
        lease!.Dispose();
    }

    [Fact]
    public void ActiveGameOwnershipHandle_BlocksProfileOperationsUntilReleased()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProfileGameActivity activity = new(paths, new NullAppLogger());
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());

        Assert.True(activity.TryAcquireOwnership("main", out IDisposable? gameOwnership));
        Assert.NotNull(gameOwnership);
        Assert.False(profileLock.TryAcquire("main", out IDisposable? blockedLease));
        Assert.Null(blockedLease);

        gameOwnership!.Dispose();

        Assert.True(profileLock.TryAcquire("main", out IDisposable? recoveredLease));
        recoveredLease!.Dispose();
    }

    [Fact]
    public void LivePidAndStartTime_BlockAfterGuardianHandleIsLost()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProfileGameActivity activity = new(paths, new NullAppLogger());
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());
        using Process current = Process.GetCurrentProcess();
        const string token = "guardian-crash-recovery";
        activity.WriteState("main", current.Id, current.StartTime.ToUniversalTime(), token);

        Assert.False(profileLock.TryAcquire("main", out IDisposable? lease));
        Assert.Null(lease);

        activity.DeleteState("main", token);
    }

    [Fact]
    public void ReusedPidWithDifferentStartTime_IsTreatedAsStale()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProfileGameActivity activity = new(paths, new NullAppLogger());
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());
        using Process current = Process.GetCurrentProcess();
        activity.WriteState(
            "main",
            current.Id,
            current.StartTime.ToUniversalTime().AddTicks(1),
            "reused-pid");

        Assert.True(profileLock.TryAcquire("main", out IDisposable? lease));
        Assert.False(File.Exists(activity.GetStatePath("main")));
        lease!.Dispose();
    }

    [Fact]
    public void MissingProcessState_IsRecoveredAsStale()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProfileGameActivity activity = new(paths, new NullAppLogger());
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());
        activity.WriteState(
            "main",
            int.MaxValue,
            DateTime.UtcNow,
            "stale-process");

        Assert.True(profileLock.TryAcquire("main", out IDisposable? lease));
        Assert.False(File.Exists(activity.GetStatePath("main")));
        lease!.Dispose();
    }

    [Fact]
    public void MalformedStateAfterGuardianHandleLoss_FailsClosedAndPreservesDiagnosticFile()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProfileGameActivity activity = new(paths, new NullAppLogger());
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());
        string statePath = activity.GetStatePath("main");
        File.WriteAllText(statePath, "{not-json");

        PackSyncException exception = Assert.Throws<PackSyncException>(() =>
            profileLock.TryAcquire("main", out _));

        Assert.Equal(PackSyncError.ManagedStateCorrupt, exception.Error);
        Assert.Contains("Закройте все окна Minecraft", exception.UserMessage, StringComparison.Ordinal);
        Assert.Contains(statePath, exception.UserMessage, StringComparison.Ordinal);
        Assert.True(File.Exists(statePath));

        File.Delete(statePath);
        Assert.True(profileLock.TryAcquire("main", out IDisposable? recoveredLease));
        recoveredLease!.Dispose();
    }

    [Fact]
    public void StructurallyInvalidState_FailsClosedWithoutNullDereference()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProfileGameActivity activity = new(paths, new NullAppLogger());
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());
        string statePath = activity.GetStatePath("main");
        File.WriteAllText(statePath, "{\"SchemaVersion\":1,\"ProcessId\":123}");

        PackSyncException exception = Assert.Throws<PackSyncException>(() =>
            profileLock.TryAcquire("main", out _));

        Assert.Equal(PackSyncError.ManagedStateCorrupt, exception.Error);
        Assert.True(File.Exists(statePath));
    }

    [Fact]
    public async Task MalformedStateWithLiveGameAndLostGuardianHandle_FailsClosed()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        string instanceDirectory = paths.GetInstanceDirectory("main");
        Directory.CreateDirectory(instanceDirectory);
        Directory.CreateDirectory(paths.RuntimeDirectory);
        string javaPath = Path.Combine(paths.RuntimeDirectory, "javaw.exe");
        File.Copy(GetCommandInterpreter(), javaPath);
        using Process game = Process.Start(new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = "/d /c \"ping 127.0.0.1 -n 3 > nul\"",
            WorkingDirectory = instanceDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        ProfileGameActivity activity = new(paths, new NullAppLogger());
        string statePath = activity.GetStatePath("main");
        File.WriteAllText(statePath, "{truncated");
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());

        PackSyncException exception = Assert.Throws<PackSyncException>(() =>
            profileLock.TryAcquire("main", out _));

        Assert.Equal(PackSyncError.ManagedStateCorrupt, exception.Error);
        Assert.False(game.HasExited);
        Assert.True(File.Exists(statePath));
        await game.WaitForExitAsync();
    }

    [Fact]
    public void ActiveGameState_IsIsolatedToItsProfile()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProfileGameActivity activity = new(paths, new NullAppLogger());
        FileProfileOperationLock profileLock = new(paths, new NullAppLogger());
        using Process current = Process.GetCurrentProcess();
        const string token = "profile-isolation";
        activity.WriteState("main", current.Id, current.StartTime.ToUniversalTime(), token);

        Assert.False(profileLock.TryAcquire("main", out _));
        Assert.True(profileLock.TryAcquire("secondary", out IDisposable? secondaryLease));

        secondaryLease!.Dispose();
        activity.DeleteState("main", token);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("main/other")]
    [InlineData("C:/outside")]
    [InlineData("..")]
    [InlineData("CON")]
    public void TryAcquire_RejectsUnsafeProfileId(string profileId)
    {
        using TestDirectory directory = new();
        FileProfileOperationLock profileLock = new(
            new LauncherDataPaths(directory.Path),
            new NullAppLogger());

        Assert.Throws<ArgumentException>(() => profileLock.TryAcquire(profileId, out _));
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MinecraftLauncherProfileLockTests",
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

    private static string GetCommandInterpreter() =>
        Environment.GetEnvironmentVariable("ComSpec") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
}
