namespace Launcher.Infrastructure.Installation;

// Product binding is chosen by compiled helper code, never by IPC, server JSON or a user catalog.
internal sealed record InstallationProduct(string PackId, string MainExe)
{
    internal static InstallationProduct Launcher { get; } = new(WindowsInstallationBackend.PackId, WindowsInstallationBackend.MainExe);
    internal static InstallationProduct Diagnostic { get; } = new("MinecraftLauncher.BootstrapperDiagnostic", "VendorProbe.exe");
}
