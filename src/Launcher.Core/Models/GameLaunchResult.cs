namespace Launcher.Core.Models;

public sealed record GameLaunchResult(
    int ProcessId,
    int ExitCode,
    string MinecraftVersionId,
    string JavaExecutable,
    string InstanceDirectory);
