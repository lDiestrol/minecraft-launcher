using Launcher.App.ViewModels;
using Launcher.Core;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.App.Tests;

public sealed class MainViewModelStartupTests
{
    private static readonly Uri BootstrapUri = new("http://localhost:18080/launcher/bootstrap.json");
    private static readonly Uri ProfilesUri = new("http://localhost:18080/launcher/profiles.json");

    [Fact]
    public async Task InitializeAsync_WithoutSavedServer_StaysOnOnboardingWithoutNetworkAccess()
    {
        StubServerClient serverClient = new();
        MainViewModel viewModel = CreateViewModel(new LauncherSettings(), serverClient);

        await viewModel.InitializeAsync();
        await viewModel.AutoConnectAsync();

        Assert.Equal(LauncherScreen.Onboarding, viewModel.Screen);
        Assert.Equal("Ожидание", viewModel.StatusText);
        Assert.Equal(0, serverClient.BootstrapCalls);
    }

    [Fact]
    public async Task InitializeAsync_WithSavedServer_CompletesBeforeNetworkAccess()
    {
        StubServerClient serverClient = new();
        MainViewModel viewModel = CreateViewModel(SavedSettings(), serverClient);

        await viewModel.InitializeAsync();

        Assert.Equal(LauncherScreen.Onboarding, viewModel.Screen);
        Assert.Equal(BootstrapUri.AbsoluteUri, viewModel.ServerUrl);
        Assert.Equal(0, serverClient.BootstrapCalls);
    }

    [Fact]
    public async Task AutoConnectAsync_WithAvailableServer_TransitionsToMain()
    {
        StubServerClient serverClient = new()
        {
            BootstrapResult = Bootstrap(),
            ProfilesResult = [Profile()],
        };
        MainViewModel viewModel = CreateViewModel(SavedSettings(), serverClient);
        await viewModel.InitializeAsync();

        await viewModel.AutoConnectAsync();

        Assert.Equal(LauncherScreen.Main, viewModel.Screen);
        Assert.Equal("Готово", viewModel.StatusText);
        Assert.Equal(1, serverClient.BootstrapCalls);
    }

    [Fact]
    public async Task AutoConnectAsync_WithPendingThenUnavailableServer_ShowsProgressAndRecoverableError()
    {
        TaskCompletionSource<BootstrapConfiguration> pendingBootstrap = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        StubServerClient serverClient = new()
        {
            BootstrapTask = pendingBootstrap.Task,
        };
        MainViewModel viewModel = CreateViewModel(SavedSettings(), serverClient);
        await viewModel.InitializeAsync();

        Task autoConnect = viewModel.AutoConnectAsync();

        Assert.False(autoConnect.IsCompleted);
        Assert.Equal(LauncherScreen.Onboarding, viewModel.Screen);
        Assert.Equal("Подключение...", viewModel.StatusText);
        Assert.True(viewModel.IsBusy);

        pendingBootstrap.SetException(new ServerConnectionException(
            "Не удалось подключиться к серверу.",
            "Test server is unavailable."));
        await autoConnect;

        Assert.Equal(LauncherScreen.Onboarding, viewModel.Screen);
        Assert.Equal("Ошибка", viewModel.StatusText);
        Assert.True(viewModel.HasConnectionFailure);
        Assert.True(viewModel.ConnectCommand.CanExecute(null));
        Assert.True(viewModel.ChangeServerCommand.CanExecute(null));
        Assert.True(viewModel.OpenSettingsCommand.CanExecute(null));
    }

    private static MainViewModel CreateViewModel(LauncherSettings settings, StubServerClient serverClient)
    {
        LauncherOperationCoordinator operationCoordinator = new(
            new UnusedPackSyncService(),
            new UnusedGameLaunchService());
        LauncherUpdateCoordinator updateCoordinator = new(new StubUpdateService(), operationCoordinator);
        return new MainViewModel(
            serverClient,
            new StubSettingsStore(settings),
            new StubMemoryProvider(),
            operationCoordinator,
            updateCoordinator,
            new StubLogger());
    }

    private static LauncherSettings SavedSettings() => new()
    {
        ServerUrl = BootstrapUri.AbsoluteUri,
        Nickname = "Player",
        RamMb = 4096,
    };

    private static BootstrapConfiguration Bootstrap() => new(
        1,
        "Test Server",
        BootstrapUri,
        ProfilesUri,
        "default");

    private static GameProfile Profile() => new(
        "default",
        "Default",
        "1.21.1",
        "vanilla",
        "1.21.1",
        "1",
        new Uri("http://localhost:18080/launcher/manifest.json"),
        "localhost",
        25565);

    private sealed class StubServerClient : ILauncherServerClient
    {
        public int BootstrapCalls { get; private set; }

        public BootstrapConfiguration? BootstrapResult { get; init; }

        public Task<BootstrapConfiguration>? BootstrapTask { get; init; }

        public IReadOnlyList<GameProfile> ProfilesResult { get; init; } = [];

        public Task<BootstrapConfiguration> GetBootstrapAsync(
            Uri bootstrapUri,
            CancellationToken cancellationToken)
        {
            BootstrapCalls++;
            return BootstrapTask
                ?? Task.FromResult(BootstrapResult
                    ?? throw new InvalidOperationException("No bootstrap response was configured."));
        }

        public Task<IReadOnlyList<GameProfile>> GetProfilesAsync(
            BootstrapConfiguration bootstrap,
            CancellationToken cancellationToken) => Task.FromResult(ProfilesResult);
    }

    private sealed class StubSettingsStore(LauncherSettings settings) : ISettingsStore
    {
        public Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveAsync(LauncherSettings updatedSettings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubMemoryProvider : ISystemMemoryProvider
    {
        public long GetTotalPhysicalMemoryMb() => 8 * 1024;
    }

    private sealed class StubUpdateService : ILauncherUpdateService
    {
        public string CurrentVersion => LauncherVersion.Current;

        public bool IsInstalled => true;

        public bool IsPortable => false;

        public Task<LauncherUpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<LauncherUpdateInfo?>(null);

        public Task DownloadUpdateAsync(
            LauncherUpdateInfo update,
            IProgress<int>? progress,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public void ApplyUpdateAndRestart(LauncherUpdateInfo update)
        {
        }
    }

    private sealed class UnusedPackSyncService : IPackSyncService
    {
        public Task<PackSyncResult> SyncAsync(
            GameProfile profile,
            IProgress<PackSyncProgress> progress,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class UnusedGameLaunchService : IGameLaunchService
    {
        public Task<GameLaunchResult> LaunchAsync(
            GameLaunchRequest request,
            IProgress<GameLaunchProgress> progress,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubLogger : IAppLogger
    {
        public void Info(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
