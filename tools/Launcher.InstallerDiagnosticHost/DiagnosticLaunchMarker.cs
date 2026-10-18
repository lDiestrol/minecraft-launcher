using System.Text.Json;

namespace Launcher.InstallerDiagnosticHost;

// Only linked into the diagnostic host and tests; never part of the Launcher or production helper.
internal sealed record DiagnosticLaunchMarker(int SchemaVersion, string LaunchId, int Pid, long StartUtcTicks)
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal static string FileName(string launchId)
    {
        if (!Guid.TryParseExact(launchId, "N", out _)) throw new InvalidDataException("Invalid diagnostic launch id.");
        return "launch-" + launchId + ".marker.json";
    }

    internal void WriteAtomic(string path)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, this, JsonOptions);
            file.Flush(true);
        }
        // Each launch publishes an immutable, unique file. Never replace a marker another process is reading.
        File.Move(temporary, path);
    }

    internal bool Matches(DiagnosticLaunchMarker expected) => SchemaVersion == 1 &&
        Guid.TryParseExact(LaunchId, "N", out _) && LaunchId == expected.LaunchId && Pid > 0 &&
        Pid == expected.Pid && StartUtcTicks > 0 && StartUtcTicks == expected.StartUtcTicks;

    internal static async Task WaitAsync(string path, DiagnosticLaunchMarker expected, TimeSpan timeout)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        do
        {
            try
            {
                using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                DiagnosticLaunchMarker? actual = JsonSerializer.Deserialize<DiagnosticLaunchMarker>(file, JsonOptions);
                if (actual?.Matches(expected) == true) return;
            }
            catch (FileNotFoundException) { }
            catch (JsonException) { } // An old/invalid marker is never confirmation.
            if (System.Diagnostics.Stopwatch.GetElapsedTime(start) >= timeout) break;
            await Task.Delay(20).ConfigureAwait(false);
        } while (true);
        throw new TimeoutException("Main of the expected diagnostic process was not confirmed; no intent cleanup authorized.");
    }
}
