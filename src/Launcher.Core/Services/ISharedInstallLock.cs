namespace Launcher.Core.Services;

public interface ISharedInstallLock
{
    Task<IDisposable?> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
