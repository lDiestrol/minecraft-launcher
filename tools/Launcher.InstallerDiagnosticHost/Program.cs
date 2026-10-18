using Velopack;
using System.Diagnostics;

namespace Launcher.InstallerDiagnosticHost;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Diagnostic product only. Never packaged as the real Launcher.
        VelopackApp.Build().SetAutoApplyOnStartup(false)
            .OnAfterInstallFastCallback(_ => DiagnosticSetupBarrier.RunIfRequested())
            .Run();
        if (args.Length != 2 || args[0] != "--bootstrapper-launch-id" || !Guid.TryParseExact(args[1], "N", out _)) return 1;
        // A bounded delay exercises the Process.Start/Main race in isolated diagnostic E2E only.
        if (int.TryParse(Environment.GetEnvironmentVariable("LAUNCHER_DIAGNOSTIC_DELAY_MS"), out int delay))
        {
            if (delay is < 0 or > 30000) return 1;
            Thread.Sleep(delay);
        }
        using Process process = Process.GetCurrentProcess();
        new DiagnosticLaunchMarker(1, args[1], process.Id, process.StartTime.ToUniversalTime().Ticks)
            .WriteAtomic(Path.Combine(AppContext.BaseDirectory, DiagnosticLaunchMarker.FileName(args[1])));
        return 0;
    }
}
