using Velopack;

namespace Launcher.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        App application = new();
        application.InitializeComponent();
        application.Run();
    }
}
