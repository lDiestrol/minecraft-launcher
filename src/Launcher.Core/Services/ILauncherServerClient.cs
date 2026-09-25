using Launcher.Core.Models;

namespace Launcher.Core.Services;

public interface ILauncherServerClient
{
    Task<BootstrapConfiguration> GetBootstrapAsync(Uri bootstrapUri, CancellationToken cancellationToken);

    Task<IReadOnlyList<GameProfile>> GetProfilesAsync(
        BootstrapConfiguration bootstrap,
        CancellationToken cancellationToken);
}
