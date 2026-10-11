using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using Launcher.Core.Services;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;
using Microsoft.Win32.SafeHandles;

namespace Launcher.Core.Tests;

public sealed class StorageRootRecoveryTests
{
    [Theory]
    [InlineData(3, 1, false)]
    [InlineData(3, 1, true)]
    [InlineData(2, 0, true)]
    public void MissingJob_RecoversSupportedWitnessOrSeparateLegacyRecordedPid(int version, int witness, bool recordedPid)
    {
        using Fixture fixture = new();
        ActiveGameState state = fixture.DeadOwner(version, witness) with
        {
            ProcessId = recordedPid ? int.MaxValue : 0,
            ProcessStartTimeUtcTicks = recordedPid ? 1 : 0,
        };
        fixture.Write(state);
        Assert.Null(WindowsGameJob.GetActiveProcesses(state.JobName!));
        Assert.False(fixture.Activity.IsActive("main"));
        Assert.False(File.Exists(fixture.StatePath));
    }

    [Theory]
    [InlineData(2, 0)] // Old pre-PID intent has no lifetime witness.
    [InlineData(3, 0)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    public void MissingJob_UnknownOrLegacyPrePidContractIsNeverCleared(int version, int witness)
    {
        using Fixture fixture = new();
        fixture.Write(fixture.DeadOwner(version, witness));
        fixture.AssertFailClosed();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyExistingJob_RecoversOnlyAfterGuardianExit(bool guardianAlive)
    {
        using Fixture fixture = new();
        ActiveGameState state = fixture.DeadOwner(3, 1);
        using WindowsGameJob job = new(state.OwnershipToken);
        if (guardianAlive)
        {
            using Process current = Process.GetCurrentProcess();
            state = state with { GuardianProcessId = current.Id, GuardianStartTimeUtcTicks = current.StartTime.ToUniversalTime().Ticks };
        }

        fixture.Write(state);
        Assert.Equal(guardianAlive, fixture.Activity.IsActive("main"));
        Assert.Equal(guardianAlive, File.Exists(fixture.StatePath));
    }

    [Fact]
    public void MissingJob_RecordedLiveMainContradictsWitnessAndFailsClosed()
    {
        using Fixture fixture = new();
        using Process current = Process.GetCurrentProcess();
        fixture.Write(fixture.DeadOwner(3, 1) with
        {
            ProcessId = current.Id,
            ProcessStartTimeUtcTicks = current.StartTime.ToUniversalTime().Ticks,
        });
        fixture.AssertFailClosed();
    }

    [Fact]
    public void JobQueryAccessDenied_IsNotTreatedAsMissingOrEmpty()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Job ACL check requires Windows.");
        }

        using Fixture fixture = new();
        ActiveGameState state = fixture.DeadOwner(3, 1);
        using WindowsGameJob job = new(state.OwnershipToken);
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        Assert.True(ConvertStringSecurityDescriptorToSecurityDescriptor(
            $"D:P(D;;0x4;;;{identity.User!.Value})(A;;GA;;;{identity.User.Value})", 1, out IntPtr descriptor, out _));
        try
        {
            Assert.True(SetKernelObjectSecurity(job.Handle, 4, descriptor));
        }
        finally
        {
            LocalFree(descriptor);
        }

        Assert.Equal(5, Assert.Throws<Win32Exception>(() => WindowsGameJob.GetActiveProcesses(job.Name)).NativeErrorCode);
        fixture.Write(state);
        fixture.AssertFailClosed();
    }

    [Fact]
    public void UnknownStateFields_FailClosedWithoutDeletingOriginal()
    {
        using Fixture fixture = new();
        fixture.Write(fixture.DeadOwner(3, 1));
        string original = File.ReadAllText(fixture.StatePath);
        File.WriteAllText(fixture.StatePath, original[..^1] + ",\"FutureLifetimeRule\":true}");
        fixture.AssertFailClosed();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(123, 0)]
    [InlineData(-1, 1)]
    public void IncompleteGuardianIdentity_IsNeverRecovered(int pid, long ticks)
    {
        using Fixture fixture = new();
        fixture.Write(fixture.DeadOwner(3, 1) with { GuardianProcessId = pid, GuardianStartTimeUtcTicks = ticks });
        fixture.AssertFailClosed();
    }

    [Fact]
    public void ForeignJobIdentity_IsNeverRecovered()
    {
        using Fixture fixture = new();
        fixture.Write(fixture.DeadOwner(3, 1) with { JobName = "Global\\MinecraftLauncher.Game.other-user.other-token" });
        fixture.AssertFailClosed();
    }

    [Fact]
    public async Task IndependentLaunchers_ConcurrentRecoveryIsSerialized()
    {
        using Fixture fixture = new();
        fixture.Write(fixture.DeadOwner(3, 1));
        string gate = Path.Combine(fixture.Directory.Path, "recover.gate");
        List<Process> observers = [];
        try
        {
            for (int index = 0; index < 4; index++)
            {
                ProcessStartInfo start = new("dotnet") { UseShellExecute = false, CreateNoWindow = true };
                foreach (string argument in new[] { Path.Combine(AppContext.BaseDirectory, "Launcher.Guardian.TestHost.dll"),
                    fixture.Local, fixture.App, "recover", gate, Path.Combine(fixture.Directory.Path, $"result-{index}.txt") })
                {
                    start.ArgumentList.Add(argument);
                }

                observers.Add(Process.Start(start)!);
            }

            File.WriteAllText(gate, "go");
            await Task.WhenAll(observers.Select(async observer =>
            {
                await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                Assert.Equal(0, observer.ExitCode);
            }));
            Assert.False(fixture.Activity.IsActive("main"));
            Assert.False(File.Exists(fixture.StatePath));
            string[] results = System.IO.Directory.GetFiles(fixture.Directory.Path, "result-*.txt").Select(File.ReadAllText).ToArray();
            Assert.Equal(4, results.Length);
            Assert.Contains("recovered", results);
            Assert.All(results, value => Assert.Contains(value, new[] { "active", "recovered" }));
        }
        finally
        {
            foreach (Process observer in observers)
            {
                if (!observer.HasExited)
                {
                    observer.Kill();
                    await observer.WaitForExitAsync();
                }

                observer.Dispose();
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        public TemporaryStorage Directory { get; } = new();
        public string Local { get; }
        public string App { get; }
        public StorageRootResolver Resolver { get; }
        public ProfileGameActivity Activity { get; }
        public string StatePath => Activity.GetStatePath("main");
        public LauncherDataPaths Paths { get; }

        public Fixture()
        {
            Local = Path.Combine(Directory.Path, "local");
            App = Path.Combine(Directory.Path, "App");
            Resolver = new(Local, App);
            using StorageRootSession session = Resolver.OpenSession();
            Paths = session.Paths;
            Activity = new(Paths, new NullAppLogger());
        }

        public ActiveGameState DeadOwner(int version, int witness)
        {
            string token = Guid.NewGuid().ToString("N");
            return new(version, "main", 0, 0, token, WindowsGameJob.GetName(token), int.MaxValue, 1, witness);
        }

        public void Write(ActiveGameState state) => File.WriteAllText(StatePath, JsonSerializer.Serialize(state));

        public void AssertFailClosed()
        {
            string original = File.ReadAllText(StatePath);
            Assert.Throws<PackSyncException>(() => Activity.IsActive("main"));
            Assert.Throws<PackSyncException>(() => Activity.TryAcquireOwnership("main", out _));
            Assert.Throws<PackSyncException>(() => Resolver.ConfigureNewRoot(Paths.RootDirectory));
            Assert.Equal(original, File.ReadAllText(StatePath));
        }

        public void Dispose() => Directory.Dispose();
    }

    [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, int revision, out IntPtr descriptor, out uint size);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKernelObjectSecurity(SafeFileHandle handle, uint securityInformation, IntPtr descriptor);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
