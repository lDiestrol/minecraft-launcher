namespace Launcher.Core.Models;

public sealed record PackManifest(
    int SchemaVersion,
    string ProfileId,
    string PackVersion,
    string MinecraftVersion,
    string LoaderType,
    string LoaderVersion,
    Uri ManifestUri,
    IReadOnlyList<PackFileEntry> Files);

public sealed record PackFileEntry(
    string Path,
    string Sha256,
    long Size,
    Uri DownloadUri);
