using Launcher.Core.Models;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "MinecraftLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenFileDoesNotExist()
    {
        JsonSettingsStore store = CreateStore();

        LauncherSettings settings = await store.LoadAsync();

        Assert.Null(settings.ServerUrl);
        Assert.Equal(string.Empty, settings.Nickname);
        Assert.Equal(0, settings.RamMb);
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsSettings()
    {
        JsonSettingsStore store = CreateStore();
        LauncherSettings expected = new()
        {
            ServerUrl = "https://example.test/launcher/bootstrap.json",
            Nickname = "Steve_1",
            SelectedProfileId = "main",
            RamMb = 6144,
        };

        await store.SaveAsync(expected);
        LauncherSettings actual = await store.LoadAsync();

        Assert.Equal(expected.ServerUrl, actual.ServerUrl);
        Assert.Equal(expected.Nickname, actual.Nickname);
        Assert.Equal(expected.SelectedProfileId, actual.SelectedProfileId);
        Assert.Equal(expected.RamMb, actual.RamMb);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{broken-json")]
    public async Task LoadAsync_ReturnsDefaultsForEmptyOrCorruptedJson(string content)
    {
        Directory.CreateDirectory(_testDirectory);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "settings.json"), content);
        JsonSettingsStore store = CreateStore();

        LauncherSettings settings = await store.LoadAsync();

        Assert.Null(settings.ServerUrl);
        Assert.Equal(string.Empty, settings.Nickname);
    }

    [Fact]
    public async Task LoadAsync_AcceptsPartialOldSettings()
    {
        Directory.CreateDirectory(_testDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_testDirectory, "settings.json"),
            "{\"nickname\":\"OldPlayer\"}");
        JsonSettingsStore store = CreateStore();

        LauncherSettings settings = await store.LoadAsync();

        Assert.Equal("OldPlayer", settings.Nickname);
        Assert.Null(settings.ServerUrl);
        Assert.Null(settings.SelectedProfileId);
        Assert.Equal(0, settings.RamMb);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private JsonSettingsStore CreateStore() =>
        new(new LauncherDataPaths(_testDirectory), new NullAppLogger());
}
