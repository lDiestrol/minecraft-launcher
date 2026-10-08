using System.Diagnostics;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class FileSharedInstallLockTests
{
    [Fact]
    public async Task IndependentProviders_UseOneCrossProcessHandleAndRecoverAfterRelease()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        FileSharedInstallLock first = new(paths, new NullAppLogger());
        FileSharedInstallLock second = new(paths, new NullAppLogger());
        using IDisposable firstLease = Assert.IsAssignableFrom<IDisposable>(
            await first.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None));

        Stopwatch wait = Stopwatch.StartNew();
        IDisposable? blockedLease = await second.TryAcquireAsync(
            TimeSpan.FromMilliseconds(150),
            CancellationToken.None);

        Assert.Null(blockedLease);
        Assert.True(wait.Elapsed >= TimeSpan.FromMilliseconds(100));
        firstLease.Dispose();

        using IDisposable recoveredLease = Assert.IsAssignableFrom<IDisposable>(
            await second.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None));
    }

    [Fact]
    public async Task WaitingProvider_AcquiresAfterCurrentWriterReleases()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        FileSharedInstallLock first = new(paths, new NullAppLogger());
        FileSharedInstallLock second = new(paths, new NullAppLogger());
        IDisposable firstLease = Assert.IsAssignableFrom<IDisposable>(
            await first.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None));

        Task<IDisposable?> waiting = second.TryAcquireAsync(
            TimeSpan.FromSeconds(2),
            CancellationToken.None);
        await Task.Delay(150);
        Assert.False(waiting.IsCompleted);

        firstLease.Dispose();
        using IDisposable recoveredLease = Assert.IsAssignableFrom<IDisposable>(await waiting);
    }

    [Fact]
    public async Task CancelledWait_DoesNotLeakSharedLock()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        FileSharedInstallLock first = new(paths, new NullAppLogger());
        FileSharedInstallLock second = new(paths, new NullAppLogger());
        IDisposable firstLease = Assert.IsAssignableFrom<IDisposable>(
            await first.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None));
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            second.TryAcquireAsync(TimeSpan.FromSeconds(5), cancellation.Token));
        firstLease.Dispose();

        using IDisposable recoveredLease = Assert.IsAssignableFrom<IDisposable>(
            await second.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None));
    }

    [Fact]
    public async Task UnlockedFileLeftByCrashedProcess_IsRecoverable()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        string locksDirectory = Path.Combine(paths.RootDirectory, ".locks");
        Directory.CreateDirectory(locksDirectory);
        File.WriteAllText(Path.Combine(locksDirectory, "shared-install.lock"), "abandoned");
        FileSharedInstallLock installLock = new(paths, new NullAppLogger());

        using IDisposable lease = Assert.IsAssignableFrom<IDisposable>(
            await installLock.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None));
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MinecraftLauncherSharedLockTests",
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
