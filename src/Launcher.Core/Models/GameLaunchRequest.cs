namespace Launcher.Core.Models;

public sealed record GameLaunchRequest(
    string ProfileId,
    string MinecraftVersion,
    string LoaderType,
    string LoaderVersion,
    string Nickname,
    int RamMb,
    string ServerAddress,
    ushort ServerPort);
