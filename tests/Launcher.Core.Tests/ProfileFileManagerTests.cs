using Launcher.Core.Models;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class ProfileFileManagerTests
{
    [Fact]
    public void GetOrCreateDirectory_ResolvesDirectoriesForDifferentProfiles()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProfileFileManager manager = new(paths, new NullAppLogger());

        string first = manager.GetOrCreateDirectory("main", ProfileDirectoryKind.Game);
        string second = manager.GetOrCreateDirectory("secondary", ProfileDirectoryKind.Game);

        Assert.Equal(paths.GetInstanceDirectory("main"), first);
        Assert.Equal(paths.GetInstanceDirectory("secondary"), second);
        Assert.NotEqual(first, second);
        Assert.True(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
    }

    [Theory]
    [InlineData(ProfileDirectoryKind.Mods, "mods")]
    [InlineData(ProfileDirectoryKind.ResourcePacks, "resourcepacks")]
    [InlineData(ProfileDirectoryKind.ShaderPacks, "shaderpacks")]
    public void GetOrCreateDirectory_CreatesMissingProfileSubdirectory(
        ProfileDirectoryKind directoryKind,
        string expectedName)
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        ProfileFileManager manager = new(paths, new NullAppLogger());

        string result = manager.GetOrCreateDirectory("main", directoryKind);

        Assert.Equal(Path.Combine(paths.GetInstanceDirectory("main"), expectedName), result);
        Assert.True(Directory.Exists(result));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..")]
    [InlineData("main/../../outside")]
    [InlineData("C:/outside")]
    public void GetOrCreateDirectory_RejectsProfileTraversal(string profileId)
    {
        using TestDirectory directory = new();
        ProfileFileManager manager = new(new LauncherDataPaths(directory.Path), new NullAppLogger());

        Assert.Throws<ArgumentException>(() =>
            manager.GetOrCreateDirectory(profileId, ProfileDirectoryKind.Game));
    }

    [Fact]
    public void GetOrCreateDirectory_RejectsInstanceReparsePoint_WhenLinksAreSupported()
    {
        using TestDirectory directory = new();
        LauncherDataPaths paths = new(directory.Path);
        Directory.CreateDirectory(paths.InstancesDirectory);
        string target = Path.Combine(directory.Path, "outside");
        Directory.CreateDirectory(target);
        string link = paths.GetInstanceDirectory("main");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return;
        }

        ProfileFileManager manager = new(paths, new NullAppLogger());

        Assert.Throws<InvalidOperationException>(() =>
            manager.GetOrCreateDirectory("main", ProfileDirectoryKind.Game));
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MinecraftLauncherProfileFileTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
