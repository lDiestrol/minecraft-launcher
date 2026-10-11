using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

// Isolated test process only; never part of the Launcher CLI or release packaging.
if (args[0] == "verify-java")
{
    return await RealJavaRecoveryChecks.RunAsync(args[1], args[2], args[3], args[4]);
}

StorageRootResolver resolver = new(args[0], args[1]);
using StorageRootSession session = resolver.OpenSession();
if (args[2] == "recover")
{
    while (!File.Exists(args[3]))
    {
        await Task.Delay(10);
    }

    ProfileGameActivity activity = new(session.Paths, new NullAppLogger());
    bool active = activity.IsActive("main");
    await File.WriteAllTextAsync(args[4], active ? "active" : "recovered");
    return 0;
}

string phase = args[3];
string marker = args[4];
async Task Gate(string current, int pid = 0)
{
    if (phase == current)
    {
        string temporary = marker + ".tmp";
        await File.WriteAllTextAsync(temporary, pid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.Move(temporary, marker);
        await Task.Delay(Timeout.Infinite);
    }
}

return await GameProcessGuardian.RunAsync(session.Paths, args[2], new NullAppLogger(),
    afterIntent: () => Gate("intent"),
    afterProcessStart: process => Gate("created", process.Id),
    afterState: () => Gate("state"));
