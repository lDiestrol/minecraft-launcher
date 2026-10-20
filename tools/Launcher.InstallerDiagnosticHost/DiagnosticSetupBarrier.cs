using System.Diagnostics;
using System.Text.Json;

namespace Launcher.InstallerDiagnosticHost;

// Diagnostic-only gate. It runs INSIDE the real Velopack install hook, not in the parent helper.
// It must finish before Velopack's 30s fast-hook watchdog.
internal static class DiagnosticSetupBarrier
{
    internal const string RootVariable = "LAUNCHER_DIAGNOSTIC_SETUP_BARRIER_ROOT";
    internal const string RunIdVariable = "LAUNCHER_DIAGNOSTIC_SETUP_BARRIER_ID";
    internal static readonly TimeSpan MaximumWait = TimeSpan.FromSeconds(20);

    internal static bool IsRequested(string? root, string? runId) => root is not null || runId is not null;

    internal static (string Root, string Id) Validate(string? root, string? runId)
    {
        if (root is null || runId is null || !Guid.TryParseExact(runId, "N", out _) ||
            runId != runId.ToLowerInvariant())
            throw new InvalidDataException("Invalid diagnostic setup barrier configuration.");
        if (root.StartsWith(@"\\", StringComparison.Ordinal) || !Path.IsPathFullyQualified(root) ||
            root.Length > 1024 || Path.GetFullPath(root) != root.TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidDataException("Diagnostic setup barrier requires canonical local directory.");
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Diagnostic setup barrier directory must exist.");
        for (DirectoryInfo? entry = new(root); entry is not null; entry = entry.Parent)
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Diagnostic setup barrier reparse point rejected.");
        return (root, runId);
    }

    internal static void RunIfRequested()
    {
        string? root = Environment.GetEnvironmentVariable(RootVariable);
        string? id = Environment.GetEnvironmentVariable(RunIdVariable);
        if (!IsRequested(root, id)) return;
        var validated = Validate(root, id);
        Run(validated.Root, validated.Id, MaximumWait);
    }

    internal static void Run(string root, string id, TimeSpan timeout)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (timeout <= TimeSpan.Zero || timeout > MaximumWait) throw new ArgumentOutOfRangeException(nameof(timeout));
        string ready = Path.Combine(root, "setup-hook-ready-" + id + ".json");
        string continued = Path.Combine(root, "setup-hook-continued-" + id + ".json");
        string timedOut = Path.Combine(root, "setup-hook-timeout-" + id + ".json");
        if (File.Exists(ready) || File.Exists(continued) || File.Exists(timedOut))
            throw new IOException("Existing diagnostic barrier evidence rejected.");

        using EventWaitHandle gate = new(false, EventResetMode.ManualReset,
            @"Local\MinecraftLauncher.SetupHook." + id, out bool createdNew);
        if (!createdNew) throw new IOException("Existing diagnostic barrier event rejected.");
        using Process current = Process.GetCurrentProcess();
        var identity = new { schemaVersion = 1, runId = id, pid = current.Id, startUtcTicks = current.StartTime.ToUniversalTime().Ticks };
        WriteOnce(ready, new
        {
            identity.schemaVersion,
            identity.runId,
            hookPid = identity.pid,
            hookStartUtcTicks = identity.startUtcTicks,
            eventName = @"Local\MinecraftLauncher.SetupHook." + id,
            state = "hook_entered",
            timeoutMs = (int)timeout.TotalMilliseconds
        });
        bool signaled = gate.WaitOne(timeout);
        WriteOnce(signaled ? continued : timedOut, new
        {
            identity.schemaVersion,
            identity.runId,
            hookPid = identity.pid,
            hookStartUtcTicks = identity.startUtcTicks,
            state = signaled ? "continued" : "timeout"
        });
        if (!signaled) throw new TimeoutException("Diagnostic Setup hook did not receive Continue.");
    }

    private static void WriteOnce<T>(string path, T data)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        JsonSerializer.Serialize(stream, data);
        stream.Flush(true);
    }
}
