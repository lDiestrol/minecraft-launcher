using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using StorageRoot.JobPrototype;

string assembly = typeof(NativeJob).Assembly.Location;
if (args[0] == "query")
{
    Console.WriteLine(JsonSerializer.Serialize(NativeJob.Inspect(args[1])));
    return 0;
}

if (args[0] == "idle")
{
    while (!File.Exists(args[1]))
    {
        await Task.Delay(20);
    }
    return 0;
}

if (args[0] == "policy")
{
    Directory.CreateDirectory(args[1]);
    using NativeJob job = new(NewName(), args[1]);
    Console.WriteLine("ACL=" + job.SecurityDescriptor());
    try
    {
        using NativeJob collision = new(job.Name, args[1]);
        throw new InvalidOperationException("Existing job was reused.");
    }
    catch (Win32Exception error) when (error.NativeErrorCode == 183)
    {
        Console.WriteLine("COLLISION=183, rejected");
    }

    Console.WriteLine("INITIAL=" + JsonSerializer.Serialize(NativeJob.Inspect(job.Name)));
    job.DenyQuery();
    try
    {
        _ = NativeJob.Inspect(job.Name);
        throw new InvalidOperationException("Denied query was accepted.");
    }
    catch (Win32Exception error) when (error.NativeErrorCode == 5)
    {
        Console.WriteLine("ACL_DENIAL=5, not missing or zero");
    }

    return 0;
}

if (args[0] == "nested")
{
    Directory.CreateDirectory(args[3]);
    using NativeJob outer = new(NewName(), args[3]);
    if (args[4] == "restricted")
    {
        outer.SetActiveProcessLimit(2); // Controller + guardian fit; atomic Java creation must fail.
    }

    NativeJob.CreatedProcess process = outer.Start("C:\\Program Files\\dotnet\\dotnet.exe",
        [assembly, "run", args[1], args[2], Path.Combine(args[3], "inner"), "main", "state"],
        args[3], Variables());
    using Process owner = Process.GetProcessById(process.Pid);
    await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(35));
    Console.WriteLine($"NESTED {args[4]} exit={owner.ExitCode}");
    Console.WriteLine(File.ReadAllText(Path.Combine(args[3], "stdout.txt")));
    string error = File.ReadAllText(Path.Combine(args[3], "stderr.txt"));
    Console.WriteLine(error);
    if (args[4] == "restricted")
    {
        Require(owner.ExitCode != 0 && error.Contains("NativeJob.Start", StringComparison.Ordinal)
            && File.Exists(Path.Combine(args[3], "inner", "intent.json"))
            && !File.Exists(Path.Combine(args[3], "inner", "observer.json"))
            && !File.Exists(Path.Combine(args[3], "inner", "ready.txt")),
            "Restricted test did not fail at atomic Java creation.");
        return 0;
    }

    return owner.ExitCode;
}

if (args[0] == "owner")
{
    string directory = args[2], phase = args[5];
    using NativeJob job = new(NewName(), directory);
    WriteState(directory, "intent.json", new
    {
        SchemaVersion = 1,
        job.Name,
        Phase = "intent",
        WitnessHandle = job.Witness.DangerousGetHandle().ToInt64(),
        job.CanaryHandle,
        job.FullJobHandle,
    });
    Console.WriteLine("OWNER ACL=" + job.SecurityDescriptor());
    if (phase == "intent")
    {
        File.WriteAllText(Path.Combine(directory, "phase.txt"), phase);
        await Task.Delay(Timeout.Infinite);
    }

    string type = args[4] == "handleless" ? "HandlelessChild" : "JobWitness";
    string[] arguments = ["--enable-native-access=ALL-UNNAMED", "-cp", args[3], type, args[4],
        Path.Combine(directory, "ready.txt"), Path.Combine(directory, "release.txt"),
        "argument with spaces", "quote\"and\\slashes", "Привет"];
    // Java 8 does not recognize the native-access flag (only HandlelessChild uses FFM).
    if (type != "HandlelessChild")
    {
        arguments = arguments[1..];
    }

    Dictionary<string, string?> environment = Variables();
    environment["JOB_PROTOTYPE_VALUE"] = "prototype-env-value";
    NativeJob.CreatedProcess child = job.Start(args[1], arguments, directory, environment);
    // Test observer only, not the durable launch state. Lets the controller kill the
    // owner immediately after native creation, without waiting for JVM initialization.
    WriteState(directory, "observer.json", child);
    if (phase == "state")
    {
        WriteState(directory, "created.json", new { SchemaVersion = 1, job.Name, Phase = phase, Child = child });
        using Process unrelated = Process.Start(new ProcessStartInfo("C:\\Program Files\\dotnet\\dotnet.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { assembly, "idle", Path.Combine(directory, "release.outsider.txt") },
        })!;
        WriteState(directory, "outsider.json", new { unrelated.Id });
    }

    File.WriteAllText(Path.Combine(directory, "phase.txt"), phase);
    await Task.Delay(Timeout.Infinite);
    return 0;
}

// Independent controller: no persistent Job handle; only short-lived query/duplicate probes.
string java = args[1], classes = args[2], working = args[3], mode = args[4], crashPhase = args[5];
Directory.CreateDirectory(working);
using Process ownerProcess = Process.Start(new ProcessStartInfo("C:\\Program Files\\dotnet\\dotnet.exe")
{
    UseShellExecute = false,
    ArgumentList = { assembly, "owner", java, working, classes, mode, crashPhase },
})!;
Process? jvm = null, descendant = null, ownerOutsider = null;
try
{
    await WaitForFile(Path.Combine(working, "phase.txt"), ownerProcess);
    using JsonDocument intent = JsonDocument.Parse(File.ReadAllText(Path.Combine(working, "intent.json")));
    string name = intent.RootElement.GetProperty("Name").GetString()!;
    Console.WriteLine($"TEST java={java}; phase={crashPhase}; mode={mode}; owner={ownerProcess.Id}");
    if (crashPhase == "intent")
    {
        ownerProcess.Kill();
        await ownerProcess.WaitForExitAsync();
        await Task.Delay(200);
        Console.WriteLine("PRE-CREATE=" + await IndependentQuery(name));
        Require(NativeJob.Inspect(name) is null && !File.Exists(Path.Combine(working, "ready.txt")),
            "Pre-create phase unexpectedly started Java.");
        return 0;
    }

    if (crashPhase == "created")
    {
        using JsonDocument observer = JsonDocument.Parse(File.ReadAllText(Path.Combine(working, "observer.json")));
        int observedPid = observer.RootElement.GetProperty("Pid").GetInt32();
        Require(!NativeJob.InheritedSameObject(ownerProcess.Id, observedPid,
            intent.RootElement.GetProperty("CanaryHandle").GetInt64()), "Java inherited canary file.");
        Require(!NativeJob.InheritedSameObject(ownerProcess.Id, observedPid,
            intent.RootElement.GetProperty("FullJobHandle").GetInt64()), "Java inherited unrestricted Job handle.");
        ownerProcess.Kill();
        await ownerProcess.WaitForExitAsync();
        Console.WriteLine("IMMEDIATE_CRASH: owner killed before waiting for Java ready; no PID launch state.");
    }

    await WaitForFile(Path.Combine(working, "ready.txt"), ownerProcess);
    int pid = int.Parse(File.ReadAllText(Path.Combine(working, "ready.txt")));
    long witness = intent.RootElement.GetProperty("WitnessHandle").GetInt64();
    jvm = Process.GetProcessById(pid);
    _ = jvm.Handle;
    Require(NativeJob.InheritedJobAccess(pid, witness, name) == 4, "Java lost QUERY-only witness.");
    if (!ownerProcess.HasExited)
    {
        Require(!NativeJob.InheritedSameObject(ownerProcess.Id, pid, intent.RootElement.GetProperty("CanaryHandle").GetInt64()),
            "Java inherited canary file.");
        Require(!NativeJob.InheritedSameObject(ownerProcess.Id, pid, intent.RootElement.GetProperty("FullJobHandle").GetInt64()),
            "Java inherited unrestricted Job handle.");
    }
    using (Process outsider = Process.Start(new ProcessStartInfo("C:\\Program Files\\dotnet\\dotnet.exe")
    {
        UseShellExecute = false,
        ArgumentList = { assembly, "idle", Path.Combine(working, "release.outsider.txt") },
    })!)
    {
        try
        {
            Require(NativeJob.InheritedJobAccess(outsider.Id, witness, name) is null, "Unrelated process inherited witness.");
        }
        finally
        {
            outsider.Kill();
            await outsider.WaitForExitAsync();
        }
    }

    if (mode != "main")
    {
        await WaitForFile(Path.Combine(working, "ready.txt.child"), ownerProcess);
        int otherPid = int.Parse(File.ReadAllText(Path.Combine(working, "ready.txt.child")));
        descendant = Process.GetProcessById(otherPid);
        _ = descendant.Handle;
        uint? access = NativeJob.InheritedJobAccess(otherPid, witness, name);
        Require(mode == "spawn" ? access == 4 : access is null, "Unexpected descendant handle inheritance.");
        Console.WriteLine($"CHILD pid={otherPid}; witness={access?.ToString() ?? "absent"}");
    }

    NativeJob.Snapshot before = NativeJob.Inspect(name)!;
    if (crashPhase == "state")
    {
        using JsonDocument outsiderState = JsonDocument.Parse(File.ReadAllText(Path.Combine(working, "outsider.json")));
        ownerOutsider = Process.GetProcessById(outsiderState.RootElement.GetProperty("Id").GetInt32());
        _ = ownerOutsider.Handle;
        Require(NativeJob.InheritedJobAccess(ownerOutsider.Id, witness, name) is null
            && before.Members.All(member => member.Pid != ownerOutsider.Id),
            "Same-creator unrelated process inherited game witness/membership.");
        Console.WriteLine($"UNRELATED_SAME_CREATOR pid={ownerOutsider.Id}; witness=absent; outside guarded Job");
    }
    Require(before.Members.Any(member => member.Pid == pid && member.InJob), "JVM outside expected Job.");
    Require((before.LimitFlags & (0x800u | 0x1000u | 0x2000u)) == 0, "Forbidden Job limits.");
    if (descendant is not null)
    {
        Require(before.Members.Any(member => member.Pid == descendant.Id && member.InJob), "Child outside expected Job.");
    }

    Console.WriteLine("BEFORE=" + JsonSerializer.Serialize(before));
    if (!ownerProcess.HasExited)
    {
        ownerProcess.Kill(); // Never kill the process tree or the JVM.
        await ownerProcess.WaitForExitAsync();
    }
    Console.WriteLine("AFTER_CRASH=" + await IndependentQuery(name));
    Require(!jvm.HasExited && NativeJob.InheritedJobAccess(pid, witness, name) == 4
        && NativeJob.Count(name) > 0, "Live main JVM protection lost after guardian crash.");
    Require(crashPhase != "created" || !File.Exists(Path.Combine(working, "created.json")), "Pre-PID phase wrote PID state.");
    File.WriteAllText(Path.Combine(working, "release.txt"), "finish");
    await jvm.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
    Require(jvm.ExitCode == 37, "Exit code lost.");
    await Task.Delay(200);
    Console.WriteLine($"JVM_EXIT={jvm.ExitCode}; childAlive={descendant is not null && !descendant.HasExited}; job={await IndependentQuery(name)}");
    if (descendant is not null)
    {
        Require(!descendant.HasExited && NativeJob.InAnyJob(descendant.Id), "Descendant did not survive inside Job.");
        if (mode == "spawn")
        {
            Require(NativeJob.Count(name) > 0, "Inheriting descendant lost named protection.");
        }
        else
        {
            Console.WriteLine("LIMITATION: handleless descendant alive; a missing name is not proof of an empty tree.");
        }

        File.WriteAllText(Path.Combine(working, "release.txt.child"), "finish");
        await descendant.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
    }

    Console.WriteLine("AFTER_ALL_EXIT=" + await IndependentQuery(name));
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    string stdout = File.ReadAllText(Path.Combine(working, "stdout.txt"), Encoding.GetEncoding(1251));
    Console.WriteLine("STDOUT: " + stdout);
    Console.WriteLine("STDERR: " + File.ReadAllText(Path.Combine(working, "stderr.txt"), Encoding.GetEncoding(1251)));
    if (mode != "handleless")
    {
        Require(stdout.Contains("argument with spaces", StringComparison.Ordinal)
            && stdout.Contains("quote\"and\\slashes", StringComparison.Ordinal)
            && stdout.Contains("Привет", StringComparison.Ordinal)
            && stdout.Contains("ENV=prototype-env-value", StringComparison.Ordinal)
            && stdout.Contains(working, StringComparison.Ordinal), "Java arguments/environment/CWD changed.");
    }

    return 0;
}
finally
{
    File.WriteAllText(Path.Combine(working, "release.txt"), "finish");
    File.WriteAllText(Path.Combine(working, "release.txt.child"), "finish");
    File.WriteAllText(Path.Combine(working, "release.outsider.txt"), "finish");
    foreach (Process? process in new[] { descendant, jvm, ownerOutsider })
    {
        if (process is not null)
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            process.Dispose();
        }
    }

    if (!ownerProcess.HasExited)
    {
        ownerProcess.Kill();
        await ownerProcess.WaitForExitAsync();
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string NewName()
{
    using WindowsIdentity identity = WindowsIdentity.GetCurrent();
    return $"Global\\Launcher.JobPrototype.{identity.User!.Value}.{Guid.NewGuid():N}";
}

static Dictionary<string, string?> Variables() => Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
    .ToDictionary(pair => (string)pair.Key, pair => (string?)pair.Value);

static void WriteState(string directory, string file, object value)
{
    string path = Path.Combine(directory, file), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        JsonSerializer.Serialize(stream, value);
        stream.Flush(true);
    }

    File.Move(temporary, path);
}

static async Task WaitForFile(string file, Process owner)
{
    using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(15));
    while (!File.Exists(file))
    {
        Require(!owner.HasExited || File.Exists(Path.Combine(Path.GetDirectoryName(file)!, "observer.json")),
            "Owner exited before atomic creation.");
        await Task.Delay(20, cancellation.Token);
    }
}

async Task<string> IndependentQuery(string name)
{
    using Process query = Process.Start(new ProcessStartInfo("C:\\Program Files\\dotnet\\dotnet.exe")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        ArgumentList = { assembly, "query", name },
    })!;
    string result = (await query.StandardOutput.ReadToEndAsync()).Trim();
    await query.WaitForExitAsync();
    Require(query.ExitCode == 0, "Independent query failed.");
    return result;
}
