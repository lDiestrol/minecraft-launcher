namespace Launcher.Core.Models;

public sealed record ManagedPackRemovalResult(
    int DeletedFiles,
    int MissingFiles,
    bool RemovedState);
