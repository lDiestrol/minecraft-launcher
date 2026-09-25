using Launcher.Core.Models;

namespace Launcher.Core.Services;

public interface ISettingsStore
{
    Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default);
}
