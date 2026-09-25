using Launcher.Core.Policies;

namespace Launcher.Core.Tests;

public sealed class RamPolicyTests
{
    [Fact]
    public void Minimum_IsTwoGigabytes()
    {
        Assert.Equal(2048, RamPolicy.MinimumRamMb);
    }

    [Theory]
    [InlineData(8 * 1024, 5 * 1024)]
    [InlineData(16 * 1024, 12 * 1024)]
    [InlineData(32 * 1024, 16 * 1024)]
    [InlineData(64 * 1024, 16 * 1024)]
    public void Maximum_LeavesMemoryForOperatingSystem(long totalMb, int expectedMaximum)
    {
        Assert.Equal(expectedMaximum, RamPolicy.GetMaximumRamMb(totalMb));
    }

    [Theory]
    [InlineData(8 * 1024, 4 * 1024)]
    [InlineData(16 * 1024, 6 * 1024)]
    [InlineData(32 * 1024, 8 * 1024)]
    public void Default_FollowsMachineSize(long totalMb, int expectedDefault)
    {
        Assert.Equal(expectedDefault, RamPolicy.GetDefaultRamMb(totalMb));
    }

    [Fact]
    public void Normalize_ClampsAndRoundsSelection()
    {
        Assert.Equal(2048, RamPolicy.Normalize(1000, 16 * 1024));
        Assert.Equal(5120, RamPolicy.Normalize(5900, 16 * 1024));
        Assert.Equal(12 * 1024, RamPolicy.Normalize(15 * 1024, 16 * 1024));
    }

    [Theory]
    [InlineData(2048, true)]
    [InlineData(4096, true)]
    [InlineData(1536, false)]
    [InlineData(2500, false)]
    [InlineData(13 * 1024, false)]
    public void IsValid_EnforcesRangeAndOneGigabyteSteps(int value, bool expected)
    {
        Assert.Equal(expected, RamPolicy.IsValid(value, 16 * 1024));
    }
}
