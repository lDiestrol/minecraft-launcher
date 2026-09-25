using Launcher.Core.Validation;

namespace Launcher.Core.Tests;

public sealed class ProfileValueValidatorTests
{
    [Theory]
    [InlineData("main")]
    [InlineData("fabric-1201")]
    [InlineData("profile_01")]
    [InlineData("A")]
    public void ProfileId_AcceptsSafeDirectoryNames(string profileId)
    {
        Assert.True(ProfileValueValidator.IsValidProfileId(profileId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../main")]
    [InlineData("folder/main")]
    [InlineData("folder\\main")]
    [InlineData("C:drive")]
    [InlineData("profile name")]
    [InlineData("профиль")]
    [InlineData("CON")]
    [InlineData("com1")]
    [InlineData("LPT9")]
    public void ProfileId_RejectsTraversalAndUnsafeCharacters(string profileId)
    {
        Assert.False(ProfileValueValidator.IsValidProfileId(profileId));
    }

    [Theory]
    [InlineData("1.20.1")]
    [InlineData("0.16.14")]
    [InlineData("24w14a")]
    [InlineData("1.0.0-beta+build.1")]
    public void Version_AcceptsExpectedVersionFormats(string version)
    {
        Assert.True(ProfileValueValidator.IsValidVersion(version));
    }

    [Theory]
    [InlineData("../1.20.1")]
    [InlineData("1.20.1/path")]
    [InlineData("1.20.1\\path")]
    [InlineData(" version")]
    public void Version_RejectsPathValues(string version)
    {
        Assert.False(ProfileValueValidator.IsValidVersion(version));
    }
}
