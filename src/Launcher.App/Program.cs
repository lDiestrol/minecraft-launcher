using Velopack;
using Launcher.Infrastructure.Game;

namespace Launcher.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (GameProcessGuardian.TryGetRequestPath(args, out string? guardianRequestPath))
        {
            Environment.ExitCode = GameProcessGuardian.RunAsync(guardianRequestPath!).GetAwaiter().GetResult();
            return;
        }

        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        App application = new();
        application.InitializeComponent();
        application.Run();
    }
}
