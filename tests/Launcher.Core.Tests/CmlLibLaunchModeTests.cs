using System.Diagnostics;
using CmlLib.Core;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Rules;
using CmlLib.Core.Version;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class CmlLibLaunchModeTests
{
    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public void Options_PreserveSharedSettingsAndOnlyServerModeGetsEndpoint(GameLaunchMode mode)
    {
        MinecraftPath path = new(Path.Combine(Path.GetTempPath(), "launch-mode-instance"));
        MLaunchOption option = CmlLibGameLaunchService.CreateLaunchOption(Request(mode), "javaw.exe", path);

        Assert.Same(path, option.Path);
        Assert.NotNull(option.Session);
        Assert.Equal("Player_1", option.Session.Username);
        Assert.Equal(6144, option.MaximumRamMb);
        Assert.Equal(1024, option.MinimumRamMb);
        Assert.Equal("javaw.exe", option.JavaPath);
        Assert.Equal("MinecraftLauncher", option.GameLauncherName);
        Assert.Equal(LauncherVersion.Current, option.GameLauncherVersion);
        Assert.Null(option.QuickPlaySingleplayer);
        Assert.Null(option.QuickPlayRealms);
        Assert.Null(option.QuickPlayPath);
        if (mode == GameLaunchMode.Server)
        {
            Assert.Equal("play.example.test", option.ServerIp);
            Assert.Equal(25567, option.ServerPort);
        }
        else
        {
            Assert.Null(option.ServerIp);
            Assert.DoesNotContain("is_quick_play_multiplayer", option.Features);
        }
    }

    [Theory]
    [InlineData(GameLaunchMode.Server, false, false)]
    [InlineData(GameLaunchMode.Server, true, false)]
    [InlineData(GameLaunchMode.Singleplayer, false, false)]
    [InlineData(GameLaunchMode.Singleplayer, true, false)]
    [InlineData(GameLaunchMode.Server, false, true)]
    [InlineData(GameLaunchMode.Server, true, true)]
    [InlineData(GameLaunchMode.Singleplayer, false, true)]
    [InlineData(GameLaunchMode.Singleplayer, true, true)]
    public void ProcessArguments_RespectModeForLegacyAndQuickPlayVersions(GameLaunchMode mode, bool quickPlay, bool customRoot)
    {
        using TemporaryStorage directory = new();
        StorageRootResolver resolver = new(Path.Combine(directory.Path, "local"), Path.Combine(directory.Path, "App"));
        if (customRoot)
        {
            resolver.ConfigureNewRoot(Path.Combine(directory.Path, "GameData"));
        }
        using StorageRootSession session = resolver.OpenSession();
        MinecraftPath shared = new(session.Paths.RootDirectory);
        MinecraftPath path = CmlLibGameLaunchService.CreateInstancePath(session.Paths.GetInstanceDirectory("main"), shared);
        Assert.Equal(!customRoot, session.IsLegacy);
        Assert.Equal(shared.Assets, path.Assets);
        Assert.Equal(shared.Library, path.Library);
        Assert.Equal(shared.Versions, path.Versions);
        Assert.Equal(shared.Runtime, path.Runtime);
        Assert.Equal(shared.Resource, path.Resource);
        Assert.Equal(Path.Combine(session.Paths.RootDirectory, "instances", "main"), path.BasePath.TrimEnd(Path.DirectorySeparatorChar));
        string gameArguments = quickPlay
            ? """
              ["--username", "${auth_player_name}", "--gameDir", "${game_directory}",
               {"rules":[{"action":"allow","features":{"is_quick_play_multiplayer":true}}],
                "value":["--quickPlayMultiplayer","${quickPlayMultiplayer}"]}]
              """
            : """["--username", "${auth_player_name}", "--gameDir", "${game_directory}"]""";
        IVersion version = JsonVersionParser.ParseFromJsonString(
            $$$"""
              {"id":"test-fabric","type":"release","mainClass":"net.minecraft.client.main.Main",
               "libraries":[],"arguments":{"jvm":[],"game":{{{gameArguments}}}
               }}
              """, new JsonVersionParserOptions());
        using IDisposable versionLifetime = Assert.IsAssignableFrom<IDisposable>(version);
        MLaunchOption option = CmlLibGameLaunchService.CreateLaunchOption(Request(mode), "javaw.exe", path);
        option.StartVersion = version;
        MinecraftProcessBuilder builder = new(new RulesEvaluator(), new RulesEvaluatorContext(LauncherOSRule.Current), option);
        using Process process = builder.CreateProcess();
        string arguments = process.StartInfo.Arguments;

        Assert.Equal("javaw.exe", process.StartInfo.FileName);
        Assert.Equal(path.BasePath, process.StartInfo.WorkingDirectory);
        Assert.Contains("Player_1", arguments, StringComparison.Ordinal);
        Assert.Contains("-Xmx6144m", arguments, StringComparison.Ordinal);
        Assert.Contains("-Xms1024m", arguments, StringComparison.Ordinal);
        Assert.Contains(path.BasePath, arguments, StringComparison.Ordinal);
        if (mode == GameLaunchMode.Server)
        {
            Assert.Contains("play.example.test", arguments, StringComparison.Ordinal);
            Assert.Contains("25567", arguments, StringComparison.Ordinal);
            Assert.Contains(quickPlay ? "--quickPlayMultiplayer" : "--server", arguments, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("play.example.test", arguments, StringComparison.Ordinal);
            Assert.DoesNotContain("25567", arguments, StringComparison.Ordinal);
            Assert.DoesNotContain("25565", arguments, StringComparison.Ordinal);
            Assert.DoesNotContain("--server", arguments, StringComparison.Ordinal);
            Assert.DoesNotContain("--port", arguments, StringComparison.Ordinal);
            Assert.DoesNotContain("--quickPlay", arguments, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Options_RejectUnknownModeAtCmlLibBoundary()
    {
        GameLaunchException exception = Assert.Throws<GameLaunchException>(() => CmlLibGameLaunchService.CreateLaunchOption(
            Request((GameLaunchMode)42), "javaw.exe", new MinecraftPath(Path.GetTempPath())));
        Assert.Equal(GameLaunchError.InvalidProfile, exception.Error);
    }

    private static GameLaunchRequest Request(GameLaunchMode mode) => new(
        "main", "1.20.1", "fabric", "0.16.14", "Player_1", 6144, "play.example.test", 25567, mode);
}
