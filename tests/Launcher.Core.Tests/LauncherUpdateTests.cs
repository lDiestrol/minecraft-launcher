using System.Reflection;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Updates;
using Velopack;
using Velopack.Locators;

namespace Launcher.Core.Tests;

public sealed class LauncherUpdateTests
{
    [Fact]
    public void CurrentVersion_ComesFromAssemblyInformationalVersion()
    {
        string expected = typeof(LauncherVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        Assert.Equal(expected, LauncherVersion.Current);
        Assert.Equal("0.5.0", LauncherVersion.Current);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsNoUpdate()
    {
        StubUpdateManager manager = new() { CheckResult = null };
        VelopackLauncherUpdateService service = new(manager, NullAppLogger.Instance);

        LauncherUpdateInfo? result = await service.CheckForUpdatesAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsAvailableVersion()
    {
        StubUpdateManager manager = new() { CheckResult = Update("0.4.0-rc.2") };
        VelopackLauncherUpdateService service = new(manager, NullAppLogger.Instance);

        LauncherUpdateInfo? result = await service.CheckForUpdatesAsync(CancellationToken.None);

        Assert.Equal("0.4.0-rc.2", result!.Version);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_MapsFailureToFriendlyError()
    {
        StubUpdateManager manager = new() { CheckException = new HttpRequestException("offline") };
        VelopackLauncherUpdateService service = new(manager, NullAppLogger.Instance);

        LauncherUpdateException exception = await Assert.ThrowsAsync<LauncherUpdateException>(
            () => service.CheckForUpdatesAsync(CancellationToken.None));

        Assert.Equal(LauncherUpdateError.CheckFailed, exception.Error);
        Assert.DoesNotContain("offline", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NotInstalledIsFriendly()
    {
        StubUpdateManager manager = new() { Installed = false };
        VelopackLauncherUpdateService service = new(manager, NullAppLogger.Instance);

        LauncherUpdateException exception = await Assert.ThrowsAsync<LauncherUpdateException>(
            () => service.CheckForUpdatesAsync(CancellationToken.None));

        Assert.Equal(LauncherUpdateError.NotInstalled, exception.Error);
        Assert.Contains("установленной версии", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcurrentUpdateOperation_IsRejected()
    {
        TaskCompletionSource<UpdateInfo?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubUpdateManager manager = new() { PendingCheck = pending.Task };
        VelopackLauncherUpdateService service = new(manager, NullAppLogger.Instance);
        Task<LauncherUpdateInfo?> first = service.CheckForUpdatesAsync(CancellationToken.None);

        LauncherUpdateException exception = await Assert.ThrowsAsync<LauncherUpdateException>(
            () => service.CheckForUpdatesAsync(CancellationToken.None));

        Assert.Equal(LauncherUpdateError.AlreadyRunning, exception.Error);
        pending.SetResult(null);
        await first;
    }

    [Fact]
    public async Task DownloadUpdateAsync_ReportsRealProgressAndSucceeds()
    {
        StubUpdateManager manager = new() { CheckResult = Update("0.4.0-rc.2") };
        VelopackLauncherUpdateService service = new(manager, NullAppLogger.Instance);
        LauncherUpdateInfo update = (await service.CheckForUpdatesAsync(CancellationToken.None))!;
        List<int> values = [];

        await service.DownloadUpdateAsync(
            update,
            new ImmediateProgress<int>(values.Add),
            CancellationToken.None);

        Assert.Equal([0, 50, 100], values);
        Assert.Equal(1, manager.DownloadCalls);
    }

    [Fact]
    public async Task DownloadUpdateAsync_MapsCorruptPackageToFriendlyError()
    {
        StubUpdateManager manager = new()
        {
            CheckResult = Update("0.4.0-rc.2"),
            DownloadException = new InvalidDataException("sha256 mismatch"),
        };
        VelopackLauncherUpdateService service = new(manager, NullAppLogger.Instance);
        LauncherUpdateInfo update = (await service.CheckForUpdatesAsync(CancellationToken.None))!;

        LauncherUpdateException exception = await Assert.ThrowsAsync<LauncherUpdateException>(
            () => service.DownloadUpdateAsync(update, null, CancellationToken.None));

        Assert.Equal(LauncherUpdateError.DownloadFailed, exception.Error);
        Assert.Contains("проверить пакет", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadUpdateAsync_MapsNetworkFailureToFriendlyError()
    {
        StubUpdateManager manager = new()
        {
            CheckResult = Update("0.4.0-rc.2"),
            DownloadException = new HttpRequestException("connection reset"),
        };
        VelopackLauncherUpdateService service = new(manager, NullAppLogger.Instance);
        LauncherUpdateInfo update = (await service.CheckForUpdatesAsync(CancellationToken.None))!;

        LauncherUpdateException exception = await Assert.ThrowsAsync<LauncherUpdateException>(
            () => service.DownloadUpdateAsync(update, null, CancellationToken.None));

        Assert.Equal(LauncherUpdateError.DownloadFailed, exception.Error);
        Assert.DoesNotContain("connection reset", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyUpdateAndRestart_IsUnavailableBeforeDownload()
    {
        StubUpdateManager manager = new();
        VelopackLauncherUpdateService service = new(manager, NullAppLogger.Instance);

        LauncherUpdateException exception = Assert.Throws<LauncherUpdateException>(
            () => service.ApplyUpdateAndRestart(new LauncherUpdateInfo("0.4.0-rc.2")));

        Assert.Equal(LauncherUpdateError.UpdateNotDownloaded, exception.Error);
    }

    [Fact]
    public async Task ApplyUpdateAndRestart_IsInvokedOnlyByExplicitCall()
    {
        StubLauncherUpdateService service = new();
        LauncherUpdateCoordinator coordinator = new(service, CreateGameCoordinator(Task.FromResult(GameResult())));
        LauncherUpdateInfo update = new("0.4.0-rc.2");

        await coordinator.CheckForUpdatesAsync(CancellationToken.None);
        await coordinator.DownloadUpdateAsync(update, null, CancellationToken.None);
        Assert.Equal(0, service.ApplyCalls);

        coordinator.ApplyUpdateAndRestart(update);
        Assert.Equal(1, service.ApplyCalls);
    }

    [Fact]
    public void ProductionUpdateSource_IsFixedPublicRepositoryWithoutToken()
    {
        Assert.Equal(
            "https://github.com/lDiestrol/minecraft-launcher",
            VelopackLauncherUpdateService.UpdateRepositoryUrl);
        Assert.DoesNotContain("token", VelopackLauncherUpdateService.UpdateRepositoryUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadIsBlockedWhilePlayIsActive()
    {
        TaskCompletionSource<GameLaunchResult> gameCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        LauncherOperationCoordinator game = CreateGameCoordinator(gameCompletion.Task);
        StubLauncherUpdateService updates = new();
        LauncherUpdateCoordinator coordinator = new(updates, game);
        Task play = StartPlay(game);

        LauncherUpdateException exception = await Assert.ThrowsAsync<LauncherUpdateException>(() =>
            coordinator.DownloadUpdateAsync(new LauncherUpdateInfo("0.4.0-rc.2"), null, CancellationToken.None));

        Assert.Equal(LauncherUpdateError.GameOperationActive, exception.Error);
        gameCompletion.SetResult(GameResult());
        await play;
    }

    [Fact]
    public async Task ApplyIsBlockedWhileRepairIsActive()
    {
        TaskCompletionSource<PackSyncResult> packCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        LauncherOperationCoordinator game = CreateGameCoordinator(Task.FromResult(GameResult()), packCompletion.Task);
        LauncherUpdateCoordinator coordinator = new(new StubLauncherUpdateService(), game);
        Task repair = game.RepairAsync(Profile(), null!, CancellationToken.None);

        LauncherUpdateException exception = Assert.Throws<LauncherUpdateException>(() =>
            coordinator.ApplyUpdateAndRestart(new LauncherUpdateInfo("0.4.0-rc.2")));

        Assert.Equal(LauncherUpdateError.GameOperationActive, exception.Error);
        packCompletion.SetResult(PackResult());
        await repair;
    }

    [Theory]
    [InlineData(typeof(GameProfile))]
    [InlineData(typeof(BootstrapConfiguration))]
    [InlineData(typeof(PackManifest))]
    public void ServerModels_CannotConfigureLauncherUpdateSource(Type modelType)
    {
        string[] forbidden = ["update", "github", "installer", "executable", "channel", "command", "environment"];
        string[] properties = modelType.GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain(properties, property =>
            forbidden.Any(word => property.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    private static UpdateInfo Update(string version)
    {
        VelopackAsset asset = new()
        {
            PackageId = "lDiestrol.MinecraftLauncher",
            Version = SemanticVersion.Parse(version),
            Type = VelopackAssetType.Full,
            FileName = $"lDiestrol.MinecraftLauncher-{version}-full.nupkg",
            SHA256 = new string('a', 64),
            Size = 1,
        };
        return new UpdateInfo(asset, false, null, []);
    }

    private static LauncherOperationCoordinator CreateGameCoordinator(
        Task<GameLaunchResult> gameResult,
        Task<PackSyncResult>? packResult = null) =>
        new(
            new StubPackSyncService(packResult ?? Task.FromResult(PackResult())),
            new StubGameLaunchService(gameResult));

    private static PackSyncResult PackResult() => new(0, 0, 0, 0, 0, "1.0.0");

    private static GameLaunchResult GameResult() =>
        new(1, 0, "fabric-loader-0.16.14-1.20.1", "javaw.exe", "instance");

    private static Task StartPlay(LauncherOperationCoordinator coordinator) =>
        coordinator.PlayAsync(Profile(), Request(), null!, null!, CancellationToken.None);

    private static GameProfile Profile() =>
        new("main", "Main", "1.20.1", "fabric", "0.16.14", "1.0.0", new Uri("https://example.test/manifest.json"), "mc.example.test", 25565);

    private static GameLaunchRequest Request() =>
        new("main", "1.20.1", "fabric", "0.16.14", "Player", 4096, "mc.example.test", 25565);

    private sealed class StubUpdateManager : UpdateManager
    {
        public StubUpdateManager()
            : base(
                Path.GetTempPath(),
                options: null,
                locator: new TestVelopackLocator(
                    "lDiestrol.MinecraftLauncher",
                    "0.4.0-rc.1",
                    Path.GetTempPath()))
        {
        }

        public bool Installed { get; init; } = true;

        public UpdateInfo? CheckResult { get; init; }

        public Exception? CheckException { get; init; }

        public Task<UpdateInfo?>? PendingCheck { get; init; }

        public Exception? DownloadException { get; init; }

        public int DownloadCalls { get; private set; }

        public override bool IsInstalled => Installed;

        public override bool IsPortable => false;

        public override Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            if (CheckException is not null)
            {
                return Task.FromException<UpdateInfo?>(CheckException);
            }

            return PendingCheck ?? Task.FromResult(CheckResult);
        }

        public override Task DownloadUpdatesAsync(
            UpdateInfo updates,
            Action<int>? progress = null,
            CancellationToken cancelToken = default)
        {
            DownloadCalls++;
            if (DownloadException is not null)
            {
                return Task.FromException(DownloadException);
            }

            progress?.Invoke(0);
            progress?.Invoke(50);
            progress?.Invoke(100);
            return Task.CompletedTask;
        }
    }

    private sealed class StubLauncherUpdateService : ILauncherUpdateService
    {
        public int ApplyCalls { get; private set; }

        public string CurrentVersion => LauncherVersion.Current;

        public bool IsInstalled => true;

        public bool IsPortable => false;

        public Task<LauncherUpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<LauncherUpdateInfo?>(null);

        public Task DownloadUpdateAsync(LauncherUpdateInfo update, IProgress<int>? progress, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void ApplyUpdateAndRestart(LauncherUpdateInfo update)
        {
            ApplyCalls++;
        }
    }

    private sealed class StubPackSyncService(Task<PackSyncResult> result) : IPackSyncService
    {
        public Task<PackSyncResult> SyncAsync(GameProfile profile, IProgress<PackSyncProgress> progress, CancellationToken cancellationToken) =>
            result;
    }

    private sealed class StubGameLaunchService(Task<GameLaunchResult> result) : IGameLaunchService
    {
        public Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request, IProgress<GameLaunchProgress> progress, CancellationToken cancellationToken) =>
            result;
    }

    private sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
