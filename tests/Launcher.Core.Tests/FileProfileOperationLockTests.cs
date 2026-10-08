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

    [Theory]
    [InlineData("../outside")]
    [InlineData("main/other")]
    [InlineData("C:/outside")]
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
}
