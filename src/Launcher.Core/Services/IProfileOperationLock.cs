namespace Launcher.Core.Services;

public interface IProfileOperationLock
{
    bool TryAcquire(string profileId, out IDisposable? lease);
}
