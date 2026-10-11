using Velopack;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Persistence;
using Launcher.Infrastructure.Installation;

namespace Launcher.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (GameProcessGuardian.TryGetRequestPath(args, out string? guardianRequestPath))
        {
            try
            {
                Environment.ExitCode = GameProcessGuardian.RunAsync(guardianRequestPath!).GetAwaiter().GetResult();
            }
            catch (StorageRootException)
            {
                // Fail closed before reading a request from an untrusted/unavailable data root.
                Environment.ExitCode = 6;
            }
            return;
        }

        if (InstallationStartupGuard.TryAcquireVelopackStartup(args, out IDisposable? startupLease))
        {
            using (startupLease)
            {
                VelopackApp.Build()
                    .SetAutoApplyOnStartup(false)
                    .Run();
            }
        }

        App application = new();
        application.InitializeComponent();
        application.Run();
    }
}
