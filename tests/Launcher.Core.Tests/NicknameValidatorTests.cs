using Launcher.Core.Validation;

namespace Launcher.Core.Tests;

public sealed class NicknameValidatorTests
{
    [Theory]
    [InlineData("Steve")]
    [InlineData("Alex_123")]
    [InlineData("abc")]
    [InlineData("Name123456789012")]
    public void IsValid_AcceptsMinecraftStyleNickname(string nickname)
    {
        Assert.True(NicknameValidator.IsValid(nickname));
        Assert.Null(NicknameValidator.GetError(nickname));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("Name1234567890123")]
    [InlineData("two words")]
    [InlineData("Игрок")]
    [InlineData("name-dash")]
    public void IsValid_RejectsInvalidNickname(string nickname)
    {
        Assert.False(NicknameValidator.IsValid(nickname));
        Assert.NotNull(NicknameValidator.GetError(nickname));
    }
}
