using System.Diagnostics;
using Launcher.Infrastructure.Installation;
using Launcher.Infrastructure.Persistence;

internal static class InstallationCrashHost
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args[0] == "install-writer")
        {
            bool hook = InstallationStartupGuard.TryInitializeVelopack(["--veloapp-install", "0.6.0"],
                new StorageRootResolver(args[2], args[3]), () => File.WriteAllText(args[1] + ".hook", "authenticated"));
            if (!hook) return 18;
            File.WriteAllText(args[1] + ".ready", Environment.ProcessId.ToString());
            while (!File.Exists(args[1] + ".release")) await Task.Delay(10);
            return 17;
        }
        StorageRootResolver resolver = new(args[1], args[2]);
        using IDisposable lease = resolver.AcquireInstallationLease();
        using Process owner = Process.GetCurrentProcess();
        InstallationIntent intent = new(1, 1, owner.Id, owner.StartTime.ToUniversalTime().Ticks,
            Guid.NewGuid().ToString("N"), args[2], args[3], args[4]);
        InstallationIntentStore.Write(resolver.ControlDirectory, intent);
        resolver.ConfigureNewRootUnderLease(args[3]);
        string marker = args[6];
        if (args[5] == "intent") await Gate("0");
        using WindowsInstallationJob job = new(intent.Token);
        ProcessStartInfo start = new(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = args[1] };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(InstallationCrashHost).Assembly.Location);
        start.ArgumentList.Add("install-writer"); start.ArgumentList.Add(marker);
        start.ArgumentList.Add(args[1]); start.ArgumentList.Add(args[2]);
        using WindowsInstallationProcess child = job.Start(start);
        if (args[5] == "created") await Gate(child.Id.ToString());
        File.WriteAllText(marker + ".pid", child.Id.ToString());
        if (args[5] == "state") await Gate(child.Id.ToString());
        await child.WaitForExitAsync(default);
        return child.ExitCode;

        async Task Gate(string value)
        {
            File.WriteAllText(marker + ".tmp", value);
            File.Move(marker + ".tmp", marker);
            await Task.Delay(Timeout.Infinite);
        }
    }
}
