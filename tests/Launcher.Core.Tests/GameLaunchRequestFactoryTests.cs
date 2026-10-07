using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Core.Tests;

public sealed class GameLaunchRequestFactoryTests
{
    [Fact]
    public void Create_PropagatesProfileNicknameRamAndServer()
    {
        GameProfile profile = Profile();

        GameLaunchRequest request = GameLaunchRequestFactory.Create(profile, "Player_1", 6144);

        Assert.Equal("main", request.ProfileId);
        Assert.Equal("1.20.1", request.MinecraftVersion);
        Assert.Equal("fabric", request.LoaderType);
        Assert.Equal("0.16.14", request.LoaderVersion);
        Assert.Equal("Player_1", request.Nickname);
        Assert.Equal(6144, request.RamMb);
        Assert.Equal("localhost", request.ServerAddress);
        Assert.Equal((ushort)25565, request.ServerPort);
    }

    [Theory]
    [InlineData("forge")]
    [InlineData("neoforge")]
    [InlineData("quilt")]
    [InlineData("unknown")]
    public void Create_RejectsUnsupportedLoader(string loaderType)
    {
        GameProfile profile = Profile() with { LoaderType = loaderType };

        GameLaunchException exception = Assert.Throws<GameLaunchException>(
            () => GameLaunchRequestFactory.Create(profile, "Player_1", 4096));

        Assert.Equal(GameLaunchError.UnsupportedLoader, exception.Error);
        Assert.Contains(loaderType, exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_RejectsUnsafeProfileId()
    {
        GameProfile profile = Profile() with { Id = "../escape" };

        GameLaunchException exception = Assert.Throws<GameLaunchException>(
            () => GameLaunchRequestFactory.Create(profile, "Player_1", 4096));

        Assert.Equal(GameLaunchError.InvalidProfile, exception.Error);
    }

    [Fact]
    public void Create_RejectsRamBelowLauncherMinimum()
    {
        GameLaunchException exception = Assert.Throws<GameLaunchException>(
            () => GameLaunchRequestFactory.Create(Profile(), "Player_1", 1024));

        Assert.Equal(GameLaunchError.InvalidProfile, exception.Error);
    }

    private static GameProfile Profile() => new(
        "main",
        "Main",
        "1.20.1",
        "fabric",
        "0.16.14",
        "1.0.0",
        new Uri("https://example.test/manifest.json"),
        "localhost",
        25565);
}
