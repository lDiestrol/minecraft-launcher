namespace Launcher.Core.Models;

public enum GameLaunchStage
{
    Preparing,
    CheckingMinecraft,
    DownloadingMinecraft,
    PreparingJava,
    InstallingFabric,
    CheckingFiles,
    PreparingLaunch,
    StartingMinecraft,
    MinecraftStarted,
    MinecraftExited,
    Error,
}
