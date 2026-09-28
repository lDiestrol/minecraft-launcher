using Velopack;

namespace Launcher.UpdateE2EHarness;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        VelopackApp.Build().Run();

        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: Launcher.UpdateE2EHarness <local-feed> <expected-version> <marker-path>");
            return 2;
        }

        string feedPath = Path.GetFullPath(args[0]);
        string expectedVersion = args[1];
        string markerPath = Path.GetFullPath(args[2]);
        if (!Directory.Exists(feedPath))
        {
            Console.Error.WriteLine($"Local feed does not exist: {feedPath}");
            return 3;
        }

        UpdateManager manager = new(feedPath);
        Console.WriteLine($"Installed={manager.IsInstalled}; Current={manager.CurrentVersion}; Channel=dev");
        if (!manager.IsInstalled)
        {
            Console.Error.WriteLine("Harness must run from an installed Velopack current directory.");
            return 4;
        }

        UpdateInfo? update = await manager.CheckForUpdatesAsync();
        if (update is null)
        {
            Console.Error.WriteLine("No update was detected.");
            return 5;
        }

        string detectedVersion = update.TargetFullRelease.Version.ToString();
        Console.WriteLine($"Detected={detectedVersion}; Deltas={update.DeltasToTarget.Length}");
        if (!detectedVersion.Equals(expectedVersion, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Expected {expectedVersion}, detected {detectedVersion}.");
            return 6;
        }

        await manager.DownloadUpdatesAsync(
            update,
            progress => Console.WriteLine($"Progress={progress}"),
            CancellationToken.None);

        Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
        await File.WriteAllTextAsync(markerPath, $"before-update:{manager.CurrentVersion}");
        Console.WriteLine($"Downloaded={detectedVersion}; Marker={markerPath}; Applying=true");
        manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
        return 0;
    }
}
