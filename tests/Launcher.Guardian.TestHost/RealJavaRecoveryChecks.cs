using System.Diagnostics;
using System.Text.Json;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

// Opt-in, real-JVM integration checks. Runtime/classes are supplied explicitly; no download,
// installation or dependency on an installed Minecraft instance is hidden in the unit suite.
internal static class RealJavaRecoveryChecks
{
    public static async Task<int> RunAsync(string java, string classes, string phase, string mode)
    {
        string directory = Path.Combine(Path.GetTempPath(), "LauncherJavaRecovery", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string local = Path.Combine(directory, "local"), app = Path.Combine(directory, "App");
        StorageRootResolver resolver = new(local, app);
        string root = Path.Combine(directory, "GameData");
        resolver.ConfigureNewRoot(root);
        using StorageRootSession launcher = resolver.OpenSession();
        LauncherDataPaths paths = launcher.Paths;
        string runtime = Path.Combine(paths.RuntimeDirectory, "verified-jre");
        CopyDirectory(Path.GetDirectoryName(Path.GetDirectoryName(java))!, runtime);
        string executable = Path.Combine(runtime, "bin", Path.GetFileName(java));
        string instance = paths.GetInstanceDirectory("main");
        Directory.CreateDirectory(instance);
        string ready = Path.Combine(instance, "ready.txt"), release = Path.Combine(instance, "release.txt");
        string token = Guid.NewGuid().ToString("N");
        string request = Path.Combine(GameProcessGuardian.GetGuardianDirectory(paths), token + ".request.json");
        List<string> arguments = [];
        if (mode == "handleless")
        {
            arguments.Add("--enable-native-access=ALL-UNNAMED");
        }

        arguments.AddRange(["-cp", classes, mode == "handleless" ? "HandlelessChild" : "JobWitness",
            mode, ready, release, "argument with spaces", "quote\"and\\slashes", "Привет"]);
        Dictionary<string, string?> environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(pair => (string)pair.Key, pair => (string?)pair.Value);
        environment["JOB_PROTOTYPE_VALUE"] = "prototype-env-value";
        await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new GameGuardianRequest(1, token, "main", root,
            executable, "", arguments, instance, environment, true, true, true)));
        string marker = Path.Combine(directory, "gate.txt");
        ProcessStartInfo start = new("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        foreach (string value in new[] { typeof(RealJavaRecoveryChecks).Assembly.Location, local, app, request, phase, marker })
        {
            start.ArgumentList.Add(value);
        }

        using Process guardian = Process.Start(start)!;
        Process? jvm = null, descendant = null;
        try
        {
            await UntilAsync(() => File.Exists(marker));
            ProfileGameActivity activity = new(paths, new NullAppLogger());
            string statePath = activity.GetStatePath("main");
            ActiveGameState state = JsonSerializer.Deserialize<ActiveGameState>(File.ReadAllText(statePath))!;
            Require(state.SchemaVersion == 3 && state.WitnessContractVersion == 1, "Wrong witness contract.");
            Require(activity.IsActive("main"), "Recovery raced the live guardian.");
            launcher.Dispose();
            guardian.Kill();
            await guardian.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            if (phase != "intent")
            {
                await UntilAsync(() => File.Exists(ready));
                jvm = Process.GetProcessById(int.Parse(File.ReadAllText(ready)));
                _ = jvm.Handle;
                Require(!jvm.HasExited && WindowsGameJob.GetActiveProcesses(state.JobName!) > 0,
                    "Live main JVM lost its named Job after guardian crash.");
                Require(activity.IsActive("main"), "Live JVM intent was cleared.");
                FileProfileOperationLock operations = new(paths, new NullAppLogger());
                bool acquired = operations.TryAcquire("main", out IDisposable? lease);
                lease?.Dispose();
                Require(!acquired, "Play/Repair/Delete was allowed while main JVM was alive.");
                try
                {
                    resolver.ConfigureNewRoot(root);
                    throw new InvalidOperationException("Root was reconfigured while the main JVM was alive.");
                }
                catch (StorageRootException exception) when (exception.Error == StorageRootError.Busy)
                {
                }

                if (mode != "main")
                {
                    await UntilAsync(() => File.Exists(ready + ".child"));
                    descendant = Process.GetProcessById(int.Parse(File.ReadAllText(ready + ".child")));
                    _ = descendant.Handle;
                }

                await File.WriteAllTextAsync(release, "finish");
                await jvm.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                Require(jvm.ExitCode == 37, "Java exit code was not preserved.");
                if (descendant is not null)
                {
                    Require(!descendant.HasExited, "Descendant did not outlive the main JVM.");
                    if (mode == "spawn")
                    {
                        Require(activity.IsActive("main"), "An existing active Job did not block recovery.");
                        await File.WriteAllTextAsync(release + ".child", "finish");
                        await descendant.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                    }
                    else
                    {
                        await UntilAsync(() => WindowsGameJob.GetActiveProcesses(state.JobName!) is null);
                        Require(!descendant.HasExited, "Handleless descendant exited before missing-Job recovery.");
                    }
                }
            }

            await UntilAsync(() => WindowsGameJob.GetActiveProcesses(state.JobName!) is null or 0);
            Require(!activity.IsActive("main") && !File.Exists(statePath), "Supported inactive intent did not recover.");
            bool recovered = new FileProfileOperationLock(paths, new NullAppLogger()).TryAcquire("main", out IDisposable? recoveredLease);
            recoveredLease?.Dispose();
            Require(recovered, "Profile was not released after the main JVM exited.");
            resolver.ConfigureNewRoot(root);
            using StorageRootSession reopened = resolver.OpenSession();
            Require(reopened.Paths.RootDirectory == root, "Root failed to reopen after recovery.");
            Console.WriteLine($"PASS production guardian: Java={java}; phase={phase}; mode={mode}; " +
                $"schema=3/witness=1; recovered; handleless-child-alive={descendant is not null && !descendant.HasExited}");
            return 0;
        }
        finally
        {
            launcher.Dispose();
            await File.WriteAllTextAsync(release, "finish");
            await File.WriteAllTextAsync(release + ".child", "finish");
            if (!guardian.HasExited)
            {
                guardian.Kill();
                await guardian.WaitForExitAsync();
            }

            foreach (Process? process in new[] { descendant, jvm })
            {
                if (process is not null)
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                    process.Dispose();
                }
            }

            // Only this check's unique temporary tree is removed, after all known children exit.
            Directory.Delete(directory, true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (string child in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(child, Path.Combine(target, Path.GetFileName(child)));
        }
    }

    private static async Task UntilAsync(Func<bool> predicate)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        while (!predicate())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
