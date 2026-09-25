using Launcher.Core.Policies;

namespace Launcher.Core.Tests;

public sealed class DiskSpacePolicyTests
{
    [Fact]
    public void HasEnoughSpace_AcceptsThreshold()
    {
        Assert.True(DiskSpacePolicy.HasEnoughSpace(DiskSpacePolicy.MinimumFreeBytes));
    }

    [Fact]
    public void HasEnoughSpace_RejectsValueBelowThreshold()
    {
        Assert.False(DiskSpacePolicy.HasEnoughSpace(DiskSpacePolicy.MinimumFreeBytes - 1));
    }
}
