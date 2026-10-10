using Launcher.App.Services;
using Launcher.App.ViewModels;
using Launcher.Core;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.App.Tests;

public sealed class MainViewModelLaunchTests
{
    [Theory]
    [InlineData(GameLaunchMode.Server, 0)]
    [InlineData(GameLaunchMode.Singleplayer, 0)]
    [InlineData(GameLaunchMode.Server, 7)]
    [InlineData(GameLaunchMode.Singleplayer, 7)]
    public async Task Launch_UsesSelectedProfileAndSettings_BlocksConflictingCommandsUntilExit(GameLaunchMode mode, int exitCode)
    {
        Fixture fixture = new();
        MainViewModel viewModel = await fixture.CreateViewModelAsync();
        AsyncRelayCommand command = GetCommand(viewModel, mode);
        Task finished = ExecuteAndWaitForCompletion(viewModel, command);
        try
        {
            await fixture.PackStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(viewModel.IsGamePreparing);
            Assert.False(viewModel.IsGameRunning);
            Assert.True(viewModel.CancelGameCommand.CanExecute(null));
            AssertBlocked(viewModel);
            Assert.Equal(0, fixture.GameCalls);
            Assert.Contains(ModeLabel(mode), viewModel.StatusText, StringComparison.Ordinal);

            fixture.PackReady.TrySetResult();
            await fixture.GameStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(viewModel.IsGamePreparing);
            Assert.True(viewModel.IsGameRunning);
            Assert.False(viewModel.CancelGameCommand.CanExecute(null));
            AssertBlocked(viewModel);
            Assert.Same(fixture.Profile, fixture.SyncedProfile);
            Assert.Equal(GameLaunchRequestFactory.Create(fixture.Profile, "Player_1", 4096, mode), fixture.Request);
            Assert.NotNull(fixture.SavedSettings);
            Assert.Equal(fixture.Profile.Id, fixture.SavedSettings.SelectedProfileId);
            Assert.Equal("Player_1", fixture.SavedSettings.Nickname);
            Assert.Equal(4096, fixture.SavedSettings.RamMb);
            Assert.Contains(ModeLabel(mode), viewModel.StatusText, StringComparison.Ordinal);

            fixture.GameExit.TrySetResult(exitCode);
            await finished.WaitAsync(TimeSpan.FromSeconds(10));
            AssertReady(viewModel);
            Assert.Contains(ModeLabel(mode), viewModel.StatusText, StringComparison.Ordinal);
            if (exitCode == 0)
            {
                Assert.Null(viewModel.ErrorText);
            }
            else
            {
                Assert.Contains("7", viewModel.ErrorText, StringComparison.Ordinal);
            }
        }
        finally
        {
            fixture.PackReady.TrySetResult();
            fixture.GameExit.TrySetResult(0);
            await finished.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task CancelDuringSync_DoesNotLaunchGameAndRestoresCommands(GameLaunchMode mode)
    {
        Fixture fixture = new();
        MainViewModel viewModel = await fixture.CreateViewModelAsync();
        Task finished = ExecuteAndWaitForCompletion(viewModel, GetCommand(viewModel, mode));
        await fixture.PackStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        viewModel.CancelGameCommand.Execute(null);
        await finished.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, fixture.GameCalls);
        AssertReady(viewModel);
        Assert.Equal($"Операция отменена — {ModeLabel(mode)}", viewModel.StatusText);
        Assert.Null(viewModel.ErrorText);
    }

    [Theory]
    [InlineData(GameLaunchMode.Server)]
    [InlineData(GameLaunchMode.Singleplayer)]
    public async Task SyncFailure_DoesNotLaunchGameAndRestoresCommands(GameLaunchMode mode)
    {
        Fixture fixture = new() { FailSync = true };
        MainViewModel viewModel = await fixture.CreateViewModelAsync();
        Task finished = ExecuteAndWaitForCompletion(viewModel, GetCommand(viewModel, mode));
        fixture.PackReady.TrySetResult();
        await finished.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, fixture.GameCalls);
        AssertReady(viewModel);
        Assert.Equal("Ошибка", viewModel.StatusText);
        Assert.Equal("Не удалось проверить сборку.", viewModel.ErrorText);
    }

    private static AsyncRelayCommand GetCommand(MainViewModel viewModel, GameLaunchMode mode) =>
        mode == GameLaunchMode.Server ? viewModel.PlayCommand : viewModel.SingleplayerCommand;

    private static string ModeLabel(GameLaunchMode mode) =>
        mode == GameLaunchMode.Server ? "игра на сервере" : "одиночная игра";

    private static void AssertBlocked(MainViewModel viewModel)
    {
        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.PlayCommand.CanExecute(null));
        Assert.False(viewModel.SingleplayerCommand.CanExecute(null));
        Assert.False(viewModel.RepairCommand.CanExecute(null));
        Assert.False(viewModel.DeleteManagedPackCommand.CanExecute(null));
        Assert.False(viewModel.CheckForUpdatesCommand.CanExecute(null));
        Assert.False(viewModel.DownloadUpdateCommand.CanExecute(null));
        Assert.False(viewModel.ApplyUpdateCommand.CanExecute(null));
        Assert.False(viewModel.OpenSettingsCommand.CanExecute(null));
        Assert.False(viewModel.ChangeServerCommand.CanExecute(null));
    }

    private static void AssertReady(MainViewModel viewModel)
    {
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.IsGamePreparing);
        Assert.False(viewModel.IsGameRunning);
        Assert.True(viewModel.PlayCommand.CanExecute(null));
        Assert.True(viewModel.SingleplayerCommand.CanExecute(null));
        Assert.True(viewModel.RepairCommand.CanExecute(null));
        Assert.True(viewModel.DeleteManagedPackCommand.CanExecute(null));
        Assert.True(viewModel.CheckForUpdatesCommand.CanExecute(null));
        Assert.True(viewModel.DownloadUpdateCommand.CanExecute(null));
        Assert.False(viewModel.CancelGameCommand.CanExecute(null));
    }

    private static Task ExecuteAndWaitForCompletion(MainViewModel viewModel, AsyncRelayCommand command)
    {
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? onCommandChanged = null;
        onCommandChanged = (_, _) =>
        {
            if (!viewModel.IsBusy && command.CanExecute(null))
            {
                command.CanExecuteChanged -= onCommandChanged;
                finished.TrySetResult();
            }
        };
        command.CanExecuteChanged += onCommandChanged;
        SynchronizationContext? previous = SynchronizationContext.Current;
        try
        {
            // Deliver reported progress before releasing the explicit pack/game gates.
            SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
            command.Execute(null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        return finished.Task;
    }

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }

    internal sealed class Fixture : ILauncherServerClient, ISettingsStore, ISystemMemoryProvider,
        IPackSyncService, IGameLaunchService, IProfileOperationLock, ILauncherUpdateService,
        IProfileFileManager, IManagedPackRemovalConfirmation, IAppLogger
    {
        private static readonly Uri BootstrapUri = new("https://launcher.example.test/bootstrap.json");
        public GameProfile Profile { get; } = new("main", "Test profile", "1.20.1", "fabric", "0.16.14",
            "pack-3", new Uri("https://launcher.example.test/manifest.json"), "play.example.test", 25567);
        public TaskCompletionSource PackStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource PackReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource GameStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> GameExit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FailSync { get; init; }
        public int GameCalls { get; private set; }
        public GameProfile? SyncedProfile { get; private set; }
        public GameLaunchRequest? Request { get; private set; }
        public LauncherSettings? SavedSettings { get; private set; }
        public string CurrentVersion => LauncherVersion.Current;
        public bool IsInstalled => true;
        public bool IsPortable => false;

        public async Task<MainViewModel> CreateViewModelAsync()
        {
            LauncherOperationCoordinator operations = new(this, this, this);
            MainViewModel viewModel = new(this, this, this, operations,
                new LauncherUpdateCoordinator(this, operations), this, this, this);
            await viewModel.InitializeAsync();
            await viewModel.AutoConnectAsync();
            await viewModel.CheckForUpdatesOnStartupAsync();
            return viewModel;
        }

        public Task<BootstrapConfiguration> GetBootstrapAsync(Uri bootstrapUri, CancellationToken cancellationToken) =>
            Task.FromResult(new BootstrapConfiguration(1, "Test server", BootstrapUri,
                new Uri("https://launcher.example.test/profiles.json"), Profile.Id));
        public Task<IReadOnlyList<GameProfile>> GetProfilesAsync(BootstrapConfiguration bootstrap, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameProfile>>([Profile]);
        public Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new LauncherSettings
        {
            ServerUrl = BootstrapUri.AbsoluteUri,
            Nickname = "Player_1",
            RamMb = 4096,
            SelectedProfileId = Profile.Id,
        });
        public Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
        {
            SavedSettings = settings;
            return Task.CompletedTask;
        }

        public long GetTotalPhysicalMemoryMb() => 8192;
        public async Task<PackSyncResult> SyncAsync(GameProfile profile, IProgress<PackSyncProgress> progress, CancellationToken cancellationToken)
        {
            SyncedProfile = profile;
            PackStarted.TrySetResult();
            await PackReady.Task.WaitAsync(cancellationToken);
            if (FailSync)
            {
                throw new PackSyncException(PackSyncError.HashMismatch, "Не удалось проверить сборку.", "Test hash mismatch.");
            }

            progress.Report(new PackSyncProgress(PackSyncStage.Complete, "Complete"));
            return new PackSyncResult(1, 0, 0, 0, 0, Profile.PackVersion);
        }

        public async Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request, IProgress<GameLaunchProgress> progress, CancellationToken cancellationToken)
        {
            GameCalls++;
            Request = request;
            progress.Report(new GameLaunchProgress(GameLaunchStage.MinecraftStarted, "Started", ProcessId: 123));
            GameStarted.TrySetResult();
            int exitCode = await GameExit.Task;
            progress.Report(new GameLaunchProgress(GameLaunchStage.MinecraftExited, "Exited"));
            return new GameLaunchResult(123, exitCode, "test-fabric", "javaw.exe", "test-instance");
        }

        public Task<ManagedPackRemovalResult> RemoveManagedFilesAsync(string profileId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public bool TryAcquire(string profileId, out IDisposable? lease)
        {
            lease = new Lease();
            return true;
        }

        public Task<LauncherUpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<LauncherUpdateInfo?>(new LauncherUpdateInfo("99.0.0"));
        public Task DownloadUpdateAsync(LauncherUpdateInfo update, IProgress<int>? progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public void ApplyUpdateAndRestart(LauncherUpdateInfo update) => throw new NotSupportedException();
        public string GetOrCreateDirectory(string profileId, ProfileDirectoryKind directoryKind) => "test-instance";
        public void OpenDirectory(string profileId, ProfileDirectoryKind directoryKind) => throw new NotSupportedException();
        public bool Confirm(GameProfile profile) => false;
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }

        private sealed class Lease : IDisposable
        {
            public void Dispose() { }
        }
    }
}
