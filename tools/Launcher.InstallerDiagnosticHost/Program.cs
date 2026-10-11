using Velopack;

namespace Launcher.InstallerDiagnosticHost;

internal static class Program
{
    private static void Main()
    {
        // Diagnostic product only. Never packaged as the real Launcher.
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "launch.marker"), "Diagnostic application launched.");
    }
}
