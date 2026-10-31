using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Launcher.Core.Services;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;
using Microsoft.Win32.SafeHandles;
using Xunit.Abstractions;

namespace Launcher.Core.Tests;

public sealed class GuardianJobLifecycleTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("normal")]
    [InlineData("output-fault")]
    [InlineData("output-logger-fault")]
    [InlineData("callback-fault")]
    [InlineData("query-fault")]
    [InlineData("query-unavailable")]
    [InlineData("query-logger-fault")]
    [InlineData("recovery-logger-fault")]
    public async Task Guardian_RetainsPhysicalOwnershipUntilVerifiedEmpty(string mode)
    {
        using Fixture fixture = new();
        LauncherDataPaths paths = new(fixture.Directory);
        Directory.CreateDirectory(paths.RuntimeDirectory);
        string instance = paths.GetInstanceDirectory("main");
        Directory.CreateDirectory(instance);
        // Materialize only the test host and its two managed project dependencies,
        // never a machine-specific executable or the entire test output directory.
        foreach (string name in new[] { "Launcher.Guardian.TestHost.exe", "Launcher.Guardian.TestHost.dll",
            "Launcher.Guardian.TestHost.deps.json", "Launcher.Guardian.TestHost.runtimeconfig.json", "Launcher.Core.dll", "Launcher.Infrastructure.dll" })
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, name), Path.Combine(paths.RuntimeDirectory, name));
        }

        string java = Path.Combine(paths.RuntimeDirectory, "javaw.exe");
        File.Copy(HostPath, java);
        string ready = Path.Combine(fixture.Directory, "child-ready.json");
        string release = Path.Combine(fixture.Directory, "release-child");
        string token = Guid.NewGuid().ToString("N");
        string requestPath = Path.Combine(GameProcessGuardian.GetGuardianDirectory(paths), token + ".request.json");
        Dictionary<string, string?> environment = new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
        {
            environment[(string)pair.Key] = pair.Value?.ToString();
        }

        GameGuardianRequest request = new(1, token, "main", paths.RootDirectory, java, "",
            ["guardian-lifecycle-root", ready, release], instance, environment, true, true, true);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request));
        int queries = 0;
        bool restoreQueries = false;
        WindowsGameJob? observedJob = null;
        Process? root = null;
        ConcurrentQueue<QueryObservation> observations = new();
        ProbeLogger logger = new(mode);
        uint Query(WindowsGameJob job)
        {
            observedJob = job;
            uint actual = job.ActiveProcesses;
            int call = Interlocked.Increment(ref queries);
            bool failure = mode is "query-fault" or "query-logger-fault" or "recovery-logger-fault" && call <= 2 ||
                mode == "query-unavailable" && !Volatile.Read(ref restoreQueries);
            observations.Enqueue(new(DateTime.UtcNow, Stopwatch.GetTimestamp(), actual, failure));
            if (failure)
            {
                throw new Win32Exception(5, "synthetic Job query failure");
            }

            return actual;
        }

        string Diagnostic() => JsonSerializer.Serialize(new
        {
            Mode = mode,
            fixture.Directory,
            RootPID = root?.Id,
            RootBirthFILETIME = root?.StartTime.ToUniversalTime().ToFileTimeUtc(),
            ChildReady = File.Exists(ready),
            ChildSpawn = File.Exists(ready + ".spawn.json") ? File.ReadAllText(ready + ".spawn.json") : null,
            Queries = observations.ToArray(),
            QPCFrequency = Stopwatch.Frequency,
            Messages = logger.Messages.ToArray(),
            Status = File.Exists(GameProcessGuardian.GetStatusPath(requestPath))
                ? File.ReadAllText(GameProcessGuardian.GetStatusPath(requestPath)) : null,
        });

        async Task FailAfterStateAsync()
        {
            await WaitUntilAsync(() => File.Exists(ready), TimeSpan.FromSeconds(10), Diagnostic);
            throw new IOException("synthetic after-state callback failure");
        }

        Task<int> guardian = GameProcessGuardian.RunAsync(paths, requestPath, logger,
            afterProcessStart: process =>
            {
                root = Process.GetProcessById(process.Id);
                _ = root.SafeHandle;
                return Task.CompletedTask;
            },
            afterState: mode == "callback-fault" ? FailAfterStateAsync : null,
            activeProcessQuery: Query);
        try
        {
            await WaitUntilAsync(() => File.Exists(ready), TimeSpan.FromSeconds(10), Diagnostic);
            await WaitUntilAsync(() => Volatile.Read(ref queries) >= (mode.Contains("query", StringComparison.Ordinal) || mode == "recovery-logger-fault" ? 3 : 1),
                TimeSpan.FromSeconds(10), Diagnostic);
            Assert.NotNull(root);
            Assert.True(root.HasExited);
            Assert.Equal(37, root.ExitCode);
            ChildIdentity identity = JsonSerializer.Deserialize<ChildIdentity>(await File.ReadAllTextAsync(ready))!;
            ChildIdentity spawn = JsonSerializer.Deserialize<ChildIdentity>(await File.ReadAllTextAsync(ready + ".spawn.json"))!;
            Assert.Equal(spawn, identity);
            using Process child = Process.GetProcessById(identity.PID);
            Assert.Equal(identity.BirthFILETIME, child.StartTime.ToUniversalTime().ToFileTimeUtc());
            Assert.False(child.HasExited);
            Assert.Equal(root.Id, identity.RootPID);
            Assert.Equal(java, identity.ImagePath, ignoreCase: true);
            Assert.Equal(java, child.MainModule!.FileName, ignoreCase: true);
            Assert.True(IsProcessInJob(child.SafeHandle, observedJob!.Handle, out bool inJob));
            Assert.True(inJob);
            string ownership = new ProfileGameActivity(paths, new NullAppLogger()).GetOwnershipPath("main");
            void AssertOwned()
            {
                Assert.False(guardian.IsCompleted);
                Assert.Throws<IOException>(() => { using FileStream opened = File.Open(ownership, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
                Assert.True(File.Exists(new ProfileGameActivity(paths, new NullAppLogger()).GetStatePath("main")));
            }

            AssertOwned();
            Volatile.Write(ref restoreQueries, true);
            if (mode.Contains("query", StringComparison.Ordinal) || mode == "recovery-logger-fault")
            {
                await WaitUntilAsync(() => logger.Messages.Any(x => x.StartsWith("Game Job query recovered.", StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(10), Diagnostic);
                AssertOwned(); // Successful querying of a nonempty Job must not unlock.
                Assert.Contains(observations, x => !x.Failure && x.Actual > 0);
            }

            output.WriteLine("Before cooperative child release: " + Diagnostic());
            await File.WriteAllTextAsync(release, "natural cooperative child exit");
            int result = await guardian.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(mode.StartsWith("output-", StringComparison.Ordinal) || mode == "callback-fault" ? 4 : 0, result);
            QueryObservation last = observations.Last();
            Assert.False(last.Failure);
            Assert.Equal(0u, last.Actual);
            Assert.True(observedJob.Handle.IsClosed);
            Assert.True(child.WaitForExit(0));
            Assert.Equal(0, child.ExitCode);
            Assert.False(File.Exists(new ProfileGameActivity(paths, new NullAppLogger()).GetStatePath("main")));
            using (FileStream opened = File.Open(ownership, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            GameGuardianStatus status = JsonSerializer.Deserialize<GameGuardianStatus>(await File.ReadAllTextAsync(GameProcessGuardian.GetStatusPath(requestPath)))!;
            Assert.Equal(37, status.ExitCode);
            output.WriteLine("After verified Job-empty: " + Diagnostic());
            fixture.Succeeded = true;
        }
        finally
        {
            // Even a deadline failure only requests natural exit of our own child.
            // A stalled helper gives FAIL with its directory retained, never a kill.
            Volatile.Write(ref restoreQueries, true);
            await File.WriteAllTextAsync(release, "cleanup cooperative child exit");
            try { await guardian.WaitAsync(TimeSpan.FromSeconds(15)); }
            finally
            {
                output.WriteLine("Final fixture state: " + Diagnostic());
                root?.Dispose();
            }
        }
    }

    [Fact]
    public async Task RootWithoutExplicitChildren_DrainsOutput_AndJobEmptiesWhileProcessHandleIsHeld()
    {
        using Fixture fixture = new();
        using WindowsGameJob job = new(Guid.NewGuid().ToString("N"));
        using AtomicJobProcess process = new();
        ProbeLogger logger = new("normal");
        process.Start(StartInfo(fixture.Directory), job, logger);
        SafeFileHandle handle = ProcessHandle(process);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(37, process.ExitCode);
        Assert.Contains("[Minecraft] root-output", logger.Messages);
        Assert.Contains("[Minecraft] root-error", logger.Messages);
        await WaitUntilAsync(() => job.ActiveProcesses == 0, TimeSpan.FromSeconds(2),
            () => $"PID={process.Id}; birth={process.StartTimeUtc:O}; ActiveProcesses={job.ActiveProcesses}");
        Assert.False(handle.IsClosed);
        process.Dispose();
        Assert.True(handle.IsClosed);
        fixture.Succeeded = true;
    }

    [Fact]
    public async Task ReadFailure_PropagatesAfterExit_AndResourcesAreReleased()
    {
        using Fixture fixture = new();
        using WindowsGameJob job = new(Guid.NewGuid().ToString("N"));
        using AtomicJobProcess process = new();
        process.Start(StartInfo(fixture.Directory), job, new NullAppLogger());
        using StreamReader reader = new(new FailedReadStream());
        Task failedPump = (Task)typeof(AtomicJobProcess).GetMethod("PumpAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [reader, false, new NullAppLogger()])!;
        ((List<Task>)typeof(AtomicJobProcess).GetField("_output", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(process)!).Add(failedPump);
        await Assert.ThrowsAsync<IOException>(() => process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(process.HasExited);
        Assert.Equal(37, process.ExitCode);
        SafeFileHandle handle = ProcessHandle(process);
        process.Dispose();
        Assert.True(handle.IsClosed);
        await WaitUntilAsync(() => job.ActiveProcesses == 0, TimeSpan.FromSeconds(2), () => $"ActiveProcesses={job.ActiveProcesses}");
        fixture.Succeeded = true;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HourOfFailedQueries_HasBoundedLogs_AndRecoveryCountsEveryFailure(bool changingErrors)
    {
        ProbeLogger logger = new("normal");
        GameProcessGuardian.JobQueryFailureLog log = new(logger);
        log.RecordRecovery();
        Assert.Empty(logger.Messages);
        Exception first = new Win32Exception(5), second = new Win32Exception(6);
        for (int i = 0; i < 36_000; i++)
        {
            log.RecordFailure(changingErrors && i % 2 != 0 ? second : first, i * Stopwatch.Frequency / 10);
            if (i == 0)
            {
                Assert.Single(logger.Messages); // First failure is immediate.
            }
        }

        Assert.Equal(60, logger.Messages.Count);
        log.RecordRecovery();
        Assert.Equal(61, logger.Messages.Count);
        Assert.Contains("Failed queries=36000; suppressed reports=35940", logger.Messages.Last(), StringComparison.Ordinal);
        log.RecordRecovery();
        Assert.Equal(61, logger.Messages.Count);
        log.RecordFailure(first, 36_001L * Stopwatch.Frequency / 10);
        Assert.Equal(62, logger.Messages.Count); // A new outage has its own first report.
    }

    [Theory]
    [InlineData("query-logger-fault")]
    [InlineData("recovery-logger-fault")]
    public void LoggingErrors_DoNotEscapeOrCauseHotRetryLogging(string mode)
    {
        ProbeLogger logger = new(mode);
        GameProcessGuardian.JobQueryFailureLog log = new(logger);
        for (int i = 0; i < 600; i++)
        {
            log.RecordFailure(new Win32Exception(5), i * Stopwatch.Frequency / 10);
        }

        Assert.Single(logger.Messages);
        log.RecordRecovery();
        Assert.Equal(2, logger.Messages.Count);
        Assert.Contains("Failed queries=600; suppressed reports=599", logger.Messages.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingReadiness_ProducesDeadlineFailureWithDiagnostics()
    {
        TimeoutException failure = await Assert.ThrowsAsync<TimeoutException>(() =>
            WaitUntilAsync(() => false, TimeSpan.FromMilliseconds(25), () => "ready=false; PID=123; phase=synthetic-startup"));
        Assert.Contains("deadline=25", failure.Message, StringComparison.Ordinal);
        Assert.Contains("elapsed=", failure.Message, StringComparison.Ordinal);
        Assert.Contains("ready=false; PID=123; phase=synthetic-startup", failure.Message, StringComparison.Ordinal);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan deadline, Func<string> diagnostic)
    {
        long began = Stopwatch.GetTimestamp();
        while (true)
        {
            bool ready = condition();
            TimeSpan elapsed = Stopwatch.GetElapsedTime(began);
            if (elapsed >= deadline)
            {
                throw new TimeoutException($"Synthetic helper deadline={deadline.TotalMilliseconds} ms; elapsed={elapsed.TotalMilliseconds} ms; {diagnostic()}");
            }

            if (ready)
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    private static string HostPath => Path.Combine(AppContext.BaseDirectory, "Launcher.Guardian.TestHost.exe");
    private static ProcessStartInfo StartInfo(string directory)
    {
        ProcessStartInfo start = new(HostPath)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("guardian-lifecycle-exit");
        return start;
    }

    private static SafeFileHandle ProcessHandle(AtomicJobProcess process) =>
        (SafeFileHandle)typeof(AtomicJobProcess).GetField("_process", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(process)!;

    private sealed record ChildIdentity(int PID, long BirthFILETIME, string ImagePath, int RootPID);
    private sealed record QueryObservation(DateTime UTC, long QPC, uint Actual, bool Failure);

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = System.IO.Directory.CreateTempSubdirectory("Launcher Guardian 生命周期-").FullName;
        internal bool Succeeded { get; set; }
        public void Dispose()
        {
            if (Succeeded)
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }

    private sealed class ProbeLogger(string mode) : IAppLogger
    {
        internal ConcurrentQueue<string> Messages { get; } = new();
        public void Info(string message)
        {
            Messages.Enqueue(message);
            if (mode.StartsWith("output-", StringComparison.Ordinal) && message.Contains("root-output", StringComparison.Ordinal) ||
                mode == "recovery-logger-fault" && message.StartsWith("Game Job query recovered.", StringComparison.Ordinal))
            {
                throw new IOException("synthetic output/recovery logger failure");
            }
        }

        public void Error(string message, Exception? exception = null)
        {
            Messages.Enqueue(message);
            if (mode == "output-logger-fault" || mode == "query-logger-fault" && !message.StartsWith("[Minecraft]", StringComparison.Ordinal))
            {
                throw new IOException("synthetic error logger failure");
            }
        }
    }

    private sealed class FailedReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("synthetic ReadLineAsync failure");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(new IOException("synthetic ReadLineAsync failure"));
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(SafeProcessHandle process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool inJob);
}
