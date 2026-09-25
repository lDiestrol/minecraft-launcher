namespace Launcher.Core.Services;

public interface ISystemMemoryProvider
{
    long GetTotalPhysicalMemoryMb();
}
