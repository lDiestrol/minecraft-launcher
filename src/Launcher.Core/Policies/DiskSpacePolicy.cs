namespace Launcher.Core.Policies;

public static class DiskSpacePolicy
{
    public const long MinimumFreeBytes = 4L * 1024 * 1024 * 1024;

    public static bool HasEnoughSpace(long availableBytes) => availableBytes >= MinimumFreeBytes;
}
