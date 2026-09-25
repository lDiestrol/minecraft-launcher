using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Game;

namespace Launcher.Core.Tests;

public sealed class GameLaunchErrorMapperTests
{
    [Fact]
    public void Map_MapsNetworkFailure()
    {
        GameLaunchException result = GameLaunchErrorMapper.Map(
            new HttpRequestException("network"),
            GameLaunchStage.DownloadingMinecraft);

        Assert.Equal(GameLaunchError.NetworkUnavailable, result.Error);
    }

    [Fact]
    public void Map_MapsFabricStageFailure()
    {
        GameLaunchException result = GameLaunchErrorMapper.Map(
            new InvalidOperationException("fabric"),
            GameLaunchStage.InstallingFabric);

        Assert.Equal(GameLaunchError.FabricInstallationFailed, result.Error);
    }

    [Fact]
    public void Map_MapsJavaStageFailure()
    {
        GameLaunchException result = GameLaunchErrorMapper.Map(
            new InvalidOperationException("java"),
            GameLaunchStage.PreparingJava);

        Assert.Equal(GameLaunchError.JavaPreparationFailed, result.Error);
    }

    [Fact]
    public void Map_MapsProcessStageFailure()
    {
        GameLaunchException result = GameLaunchErrorMapper.Map(
            new InvalidOperationException("process"),
            GameLaunchStage.PreparingLaunch);

        Assert.Equal(GameLaunchError.ProcessCreationFailed, result.Error);
    }
}
