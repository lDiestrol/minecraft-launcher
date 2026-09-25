namespace Launcher.Core.Policies;

public static class RamPolicy
{
    public const int MinimumRamMb = 2 * 1024;
    public const int StepMb = 1024;

    public static int GetMaximumRamMb(long totalPhysicalMemoryMb)
    {
        if (totalPhysicalMemoryMb <= 0)
        {
            return MinimumRamMb;
        }

        long reserveMb = totalPhysicalMemoryMb switch
        {
            <= 8 * 1024 => 3 * 1024,
            <= 16 * 1024 => 4 * 1024,
            _ => 6 * 1024,
        };

        long available = Math.Max(MinimumRamMb, totalPhysicalMemoryMb - reserveMb);
        return FloorToStep((int)Math.Min(available, 16L * 1024));
    }

    public static int GetDefaultRamMb(long totalPhysicalMemoryMb)
    {
        int desired = totalPhysicalMemoryMb switch
        {
            < 12 * 1024 => 4 * 1024,
            < 24 * 1024 => 6 * 1024,
            _ => 8 * 1024,
        };

        return Math.Clamp(desired, MinimumRamMb, GetMaximumRamMb(totalPhysicalMemoryMb));
    }

    public static int Normalize(int requestedMb, long totalPhysicalMemoryMb)
    {
        int rounded = FloorToStep(requestedMb);
        return Math.Clamp(rounded, MinimumRamMb, GetMaximumRamMb(totalPhysicalMemoryMb));
    }

    public static bool IsValid(int ramMb, long totalPhysicalMemoryMb) =>
        ramMb >= MinimumRamMb &&
        ramMb <= GetMaximumRamMb(totalPhysicalMemoryMb) &&
        ramMb % StepMb == 0;

    private static int FloorToStep(int value) => Math.Max(0, value / StepMb * StepMb);
}
