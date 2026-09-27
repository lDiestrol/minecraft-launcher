using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class LauncherDataPathsTests
{
    [Fact]
    public void GetInstanceDirectory_ResolvesInsideInstancesRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "MinecraftLauncherPathTests", "root");
        LauncherDataPaths paths = new(root);

        string instanceDirectory = paths.GetInstanceDirectory("main");

        Assert.Equal("main", Path.GetRelativePath(paths.InstancesDirectory, instanceDirectory));
        Assert.Equal(Path.GetFullPath(root), paths.RootDirectory);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("folder/profile")]
    [InlineData("folder\\profile")]
    [InlineData("CON")]
    public void GetInstanceDirectory_RejectsUnsafeProfileId(string profileId)
    {
        LauncherDataPaths paths = new(Path.GetTempPath());

        Assert.Throws<ArgumentException>(() => paths.GetInstanceDirectory(profileId));
    }

    [Fact]
    public void IsManagedRuntimePath_RejectsExecutableOutsideRuntimeRoot()
    {
        LauncherDataPaths paths = new(Path.Combine(Path.GetTempPath(), "MinecraftLauncherPathTests", "root"));
        string managedJava = Path.Combine(paths.RuntimeDirectory, "java-runtime-gamma", "windows-x64", "bin", "javaw.exe");
        string externalJava = Path.Combine(paths.RootDirectory, "instances", "main", "javaw.exe");

        Assert.True(paths.IsManagedRuntimePath(managedJava));
        Assert.False(paths.IsManagedRuntimePath(externalJava));
    }

    [Fact]
    public void DefaultRoot_DoesNotUseOfficialLauncherMinecraftDirectory()
    {
        LauncherDataPaths paths = new();
        string officialLauncherDirectory = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft"));

        Assert.False(
            officialLauncherDirectory.Equals(paths.RootDirectory, StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(
            Path.Combine("MinecraftLauncher"),
            paths.RootDirectory,
            StringComparison.OrdinalIgnoreCase);
    }
}
