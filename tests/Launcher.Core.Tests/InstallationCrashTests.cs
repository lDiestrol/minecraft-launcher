using System.Diagnostics;
using Launcher.Infrastructure.Installation;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

[Collection("InstallationProcessIsolation")]
public sealed class InstallationCrashTests
{
    [Theory]
    [InlineData("intent")]
    [InlineData("created")]
    [InlineData("state")]
    public async Task OwnerCrash_LeavesDurableGateAndRecoversOnlyAfterInstallerJobIsInactive(string phase)
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        string marker = Path.Combine(box.Directory, "crash-marker");
        ProcessStartInfo start = new("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Launcher.Guardian.TestHost.dll"));
        foreach (string argument in new[] { "install-owner", box.LocalData, plan.AppDirectory, plan.GameDataDirectory, plan.Payload.PackageSha256, phase, marker }) start.ArgumentList.Add(argument);
        using Process owner = Process.Start(start)!;
        Process? child = null;
        try
        {
            await WaitFor(() => File.Exists(marker) || owner.HasExited);
            Assert.False(owner.HasExited);
            int pid = int.Parse(File.ReadAllText(marker));
            if (pid > 0)
            {
                child = Process.GetProcessById(pid);
                child.EnableRaisingEvents = true;
                await WaitFor(() => File.Exists(marker + ".ready"));
                Assert.Equal("authenticated", File.ReadAllText(marker + ".hook"));
            }
            Assert.ThrowsAny<IOException>(() => box.Backend.AcquireLease());
            owner.Kill();
            await owner.WaitForExitAsync();
            Assert.Equal(StorageRootError.Busy, Assert.Throws<StorageRootException>(() => box.Resolver.OpenSession()).Error);
            InstallationIntent intent = InstallationIntentStore.Read(box.Resolver.ControlDirectory)!;
            if (child is not null)
            {
                Assert.False(child.HasExited);
                Assert.True(WindowsInstallationJob.Query(WindowsInstallationJob.NameFor(intent.Token)) > 0);
                using (IDisposable lease = await AcquireAfterOwnerRundown(box.Backend))
                    Assert.Equal(InstallationError.Busy, Assert.Throws<InstallationException>(() => box.Backend.Recover(plan)).Error);
                File.WriteAllText(marker + ".release", "release");
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                Assert.Equal(17, child.ExitCode);
                await WaitFor(() => WindowsInstallationJob.Query(WindowsInstallationJob.NameFor(intent.Token)) is null or 0);
            }
            else
            {
                Assert.False(File.Exists(marker + ".ready"));
            }
            using (IDisposable lease = await AcquireAfterOwnerRundown(box.Backend))
            {
                box.Backend.Recover(plan);
                Assert.True(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
                Assert.False(box.Backend.IsVerified(plan));
                Assert.False(InstallationStartupGuard.CanInitializeVelopack([], box.Resolver.ControlDirectory));
                // Simulated successful retry publication, NOT an actual vendor Setup in this crash harness.
                box.Backend.Prepare(plan);
                box.Backend.ConfigureStorage(plan);
                box.CreateInstalledLayout(plan);
                box.ConfirmSuccessfulSetup(plan);
                box.Backend.Complete(plan);
            }
            using StorageRootSession session = box.Resolver.OpenSession();
            Assert.Equal(plan.GameDataDirectory, session.Paths.RootDirectory);
            Assert.False(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
        }
        finally
        {
            File.WriteAllText(marker + ".release", "cleanup");
            if (!owner.HasExited) { owner.Kill(); await owner.WaitForExitAsync(); }
            if (child is not null) { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); child.Dispose(); }
        }
    }

    [Fact]
    public void NamedJobCollision_IsRejectedBeforeAnyChildCreation()
    {
        string token = Guid.NewGuid().ToString("N");
        using WindowsInstallationJob job = new(token);
        Assert.Throws<System.ComponentModel.Win32Exception>(() => new WindowsInstallationJob(token));
        Assert.Equal(0U, job.ActiveProcesses);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        Stopwatch deadline = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(deadline.Elapsed < TimeSpan.FromSeconds(15), "Test harness deadline exceeded; it never authorizes intent cleanup.");
            await Task.Delay(10);
        }
    }

    private static async Task<IDisposable> AcquireAfterOwnerRundown(WindowsInstallationBackend backend)
    {
        IDisposable? result = null;
        await WaitFor(() =>
        {
            try { result = backend.AcquireLease(); return true; }
            catch (IOException exception) when (ProfileLockFiles.IsContention(exception)) { return false; }
        });
        return result!;
    }
}

[CollectionDefinition("InstallationProcessIsolation", DisableParallelization = true)]
public sealed class InstallationProcessIsolationCollection { }
