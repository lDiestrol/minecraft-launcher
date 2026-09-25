namespace Launcher.Core.Models;

public sealed record BootstrapConfiguration(
    int SchemaVersion,
    string ServerName,
    Uri BootstrapUri,
    Uri ProfilesUri,
    string? DefaultProfileId);
