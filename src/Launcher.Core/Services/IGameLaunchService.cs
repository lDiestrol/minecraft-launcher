using Launcher.Core.Models;

namespace Launcher.Core.Services;

public interface IGameLaunchService
{
    Task<GameLaunchResult> LaunchAsync(
        GameLaunchRequest request,
        IProgress<GameLaunchProgress> progress,
        CancellationToken cancellationToken);
}
