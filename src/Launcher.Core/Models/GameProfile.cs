namespace Launcher.Core.Models;

public sealed record GameProfile(
    string Id,
    string Name,
    string MinecraftVersion,
    string LoaderType,
    string LoaderVersion,
    string PackVersion,
    Uri ManifestUrl,
    string ServerAddress,
    ushort ServerPort)
{
    public string DisplayName => $"{Name} — {MinecraftVersion} {LoaderType}";
}
