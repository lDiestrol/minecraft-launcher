using Launcher.Core.Models;

namespace Launcher.Core.Services;

public interface IPackSyncService
{
    Task<PackSyncResult> SyncAsync(
        GameProfile profile,
        IProgress<PackSyncProgress> progress,
        CancellationToken cancellationToken);
}
