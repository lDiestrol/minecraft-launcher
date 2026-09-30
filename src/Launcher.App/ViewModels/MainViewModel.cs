using System.Collections.ObjectModel;
using System.IO;
using Launcher.Core.Models;
using Launcher.Core.Policies;
using Launcher.Core.Services;
using Launcher.Core.Validation;

namespace Launcher.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ILauncherServerClient _serverClient;
    private readonly ISettingsStore _settingsStore;
    private readonly ISystemMemoryProvider _memoryProvider;
    private readonly LauncherOperationCoordinator _operationCoordinator;
    private readonly LauncherUpdateCoordinator _updateCoordinator;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private LauncherSettings _settings = new();
    private BootstrapConfiguration? _bootstrap;
    private LauncherScreen _screen = LauncherScreen.Onboarding;
    private LauncherScreen _settingsReturnScreen = LauncherScreen.Onboarding;
    private string _serverUrlBeforeSettings = string.Empty;
    private string _serverUrl = string.Empty;
    private string _serverName = string.Empty;
    private string _nickname = string.Empty;
    private GameProfile? _selectedProfile;
    private int _ramMb = RamPolicy.MinimumRamMb;
    private int _maximumRamMb = RamPolicy.MinimumRamMb;
    private long _totalMemoryMb = 8 * 1024;
    private string _statusText = "Ожидание";
    private string? _errorText;
    private bool _isBusy;
    private bool _isChangingServer;
    private bool _hasConnectionFailure;
    private bool _isGamePreparing;
    private bool _isGameRunning;
    private bool _isProgressIndeterminate = true;
    private double _progressValue;
    private string _progressDetails = string.Empty;
    private string _playButtonText = "ИГРАТЬ";
    private CancellationTokenSource? _gameLaunchCancellation;
    private LauncherUpdateInfo? _availableUpdate;
    private string _updateStatusText = string.Empty;
    private double _updateProgressValue;
    private bool _isUpdateProgressVisible;
    private bool _isUpdateDownloaded;

    public MainViewModel(
        ILauncherServerClient serverClient,
        ISettingsStore settingsStore,
        ISystemMemoryProvider memoryProvider,
        LauncherOperationCoordinator operationCoordinator,
        LauncherUpdateCoordinator updateCoordinator,
        IAppLogger logger)
    {
        _serverClient = serverClient;
        _settingsStore = settingsStore;
        _memoryProvider = memoryProvider;
        _operationCoordinator = operationCoordinator;
        _updateCoordinator = updateCoordinator;
        _logger = logger;

        ConnectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        PlayCommand = new AsyncRelayCommand(PlayAsync, () => !IsBusy);
        RepairCommand = new AsyncRelayCommand(
            RepairAsync,
            () => !IsBusy && _bootstrap is not null && SelectedProfile is not null);
        CancelGameCommand = new RelayCommand(CancelGame, () => IsGamePreparing);
        OpenSettingsCommand = new RelayCommand(OpenSettings, () => !IsBusy);
        CancelSettingsCommand = new RelayCommand(CancelSettings, () => !IsBusy);
        ChangeServerCommand = new RelayCommand(BeginServerChange, () => !IsBusy);
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsBusy && _updateCoordinator.IsInstalled);
        DownloadUpdateCommand = new AsyncRelayCommand(
            DownloadUpdateAsync,
            () => !IsBusy && _availableUpdate is not null && !IsUpdateDownloaded);
        ApplyUpdateCommand = new RelayCommand(
            ApplyUpdateAndRestart,
            () => !IsBusy && _availableUpdate is not null && IsUpdateDownloaded);

        UpdateStatusText = _updateCoordinator.IsPortable
            ? "Portable-режим: проверка обновлений выполняется вручную."
            : _updateCoordinator.IsInstalled
                ? "Проверка обновлений выполняется вручную."
                : "Обновления доступны в установленной версии Launcher.";
    }

    public string Version => $"v{_updateCoordinator.CurrentVersion}";

    public ObservableCollection<GameProfile> Profiles { get; } = [];

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand PlayCommand { get; }

    public AsyncRelayCommand RepairCommand { get; }

    public RelayCommand CancelGameCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand CancelSettingsCommand { get; }

    public RelayCommand ChangeServerCommand { get; }

    public AsyncRelayCommand CheckForUpdatesCommand { get; }

    public AsyncRelayCommand DownloadUpdateCommand { get; }

    public RelayCommand ApplyUpdateCommand { get; }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set => SetProperty(ref _updateStatusText, value);
    }

    public double UpdateProgressValue
    {
        get => _updateProgressValue;
        private set => SetProperty(ref _updateProgressValue, value);
    }

    public bool IsUpdateProgressVisible
    {
        get => _isUpdateProgressVisible;
        private set => SetProperty(ref _isUpdateProgressVisible, value);
    }

    public bool HasAvailableUpdate => _availableUpdate is not null;

    public bool IsUpdateDownloaded
    {
        get => _isUpdateDownloaded;
        private set
        {
            if (SetProperty(ref _isUpdateDownloaded, value))
            {
                DownloadUpdateCommand.RaiseCanExecuteChanged();
                ApplyUpdateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public LauncherScreen Screen
    {
        get => _screen;
        private set => SetProperty(ref _screen, value);
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set
        {
            if (SetProperty(ref _serverUrl, value))
            {
                OnPropertyChanged(nameof(ServerUrlValidationText));
                OnPropertyChanged(nameof(HasServerUrlValidationError));
                ConnectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? ServerUrlValidationText =>
        string.IsNullOrWhiteSpace(ServerUrl) ||
        ServerUrlNormalizer.TryNormalize(ServerUrl, out _, out string? validationError)
            ? null
            : validationError;

    public bool HasServerUrlValidationError => ServerUrlValidationText is not null;

    public string ServerName
    {
        get => _serverName;
        private set => SetProperty(ref _serverName, value);
    }

    public string Nickname
    {
        get => _nickname;
        set
        {
            if (SetProperty(ref _nickname, value))
            {
                ScheduleSettingsSave();
            }
        }
    }

    public GameProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                RepairCommand.RaiseCanExecuteChanged();
                ScheduleSettingsSave();
            }
        }
    }

    public int RamMb
    {
        get => _ramMb;
        set
        {
            int normalized = RamPolicy.Normalize(value, _totalMemoryMb);
            if (SetProperty(ref _ramMb, normalized))
            {
                OnPropertyChanged(nameof(RamDisplay));
                ScheduleSettingsSave();
            }
        }
    }

    public int MaximumRamMb
    {
        get => _maximumRamMb;
        private set => SetProperty(ref _maximumRamMb, value);
    }

    public string RamDisplay => $"{RamMb / 1024.0:0.#} GB";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ConnectCommand.RaiseCanExecuteChanged();
                PlayCommand.RaiseCanExecuteChanged();
                RepairCommand.RaiseCanExecuteChanged();
                OpenSettingsCommand.RaiseCanExecuteChanged();
                CancelSettingsCommand.RaiseCanExecuteChanged();
                ChangeServerCommand.RaiseCanExecuteChanged();
                CheckForUpdatesCommand.RaiseCanExecuteChanged();
                DownloadUpdateCommand.RaiseCanExecuteChanged();
                ApplyUpdateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsChangingServer
    {
        get => _isChangingServer;
        private set
        {
            if (SetProperty(ref _isChangingServer, value))
            {
                OnPropertyChanged(nameof(OnboardingTitle));
                OnPropertyChanged(nameof(OnboardingDescription));
            }
        }
    }

    public bool HasConnectionFailure
    {
        get => _hasConnectionFailure;
        private set
        {
            if (SetProperty(ref _hasConnectionFailure, value))
            {
                OnPropertyChanged(nameof(ConnectButtonText));
            }
        }
    }

    public string ConnectButtonText => HasConnectionFailure ? "ПОВТОРИТЬ" : "ПОДКЛЮЧИТЬСЯ";

    public bool IsGamePreparing
    {
        get => _isGamePreparing;
        private set
        {
            if (SetProperty(ref _isGamePreparing, value))
            {
                CancelGameCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsGameRunning
    {
        get => _isGameRunning;
        private set => SetProperty(ref _isGameRunning, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string ProgressDetails
    {
        get => _progressDetails;
        private set
        {
            if (SetProperty(ref _progressDetails, value))
            {
                OnPropertyChanged(nameof(HasProgressDetails));
            }
        }
    }

    public bool HasProgressDetails => !string.IsNullOrWhiteSpace(ProgressDetails);

    public string PlayButtonText
    {
        get => _playButtonText;
        private set => SetProperty(ref _playButtonText, value);
    }

    public string OnboardingTitle => IsChangingServer ? "Настройки сервера" : "Добро пожаловать";

    public string OnboardingDescription => IsChangingServer
        ? "Укажите новый URL. Текущий сервер сохранится, если подключение не удастся."
        : "Введите адрес игрового сервера";

    public async Task InitializeAsync()
    {
        try
        {
            _totalMemoryMb = _memoryProvider.GetTotalPhysicalMemoryMb();
        }
        catch (Exception exception)
        {
            _logger.Error("Physical memory detection failed; fallback value will be used.", exception);
            _totalMemoryMb = 8 * 1024;
        }

        MaximumRamMb = RamPolicy.GetMaximumRamMb(_totalMemoryMb);
        _settings = await _settingsStore.LoadAsync();
        _nickname = _settings.Nickname ?? string.Empty;
        OnPropertyChanged(nameof(Nickname));
        _ramMb = _settings.RamMb > 0
            ? RamPolicy.Normalize(_settings.RamMb, _totalMemoryMb)
            : RamPolicy.GetDefaultRamMb(_totalMemoryMb);
        OnPropertyChanged(nameof(RamMb));
        OnPropertyChanged(nameof(RamDisplay));

        if (string.IsNullOrWhiteSpace(_settings.ServerUrl))
        {
            Screen = LauncherScreen.Onboarding;
            StatusText = "Ожидание";
            return;
        }

        ServerUrl = _settings.ServerUrl;
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        ErrorText = null;
        if (!ServerUrlNormalizer.TryNormalize(ServerUrl, out Uri? bootstrapUri, out string? validationError))
        {
            ErrorText = validationError;
            StatusText = "Ошибка";
            return;
        }

        LauncherScreen connectionScreen = Screen;
        HasConnectionFailure = false;
        IsBusy = true;
        _logger.Info($"Connecting to launcher server: {bootstrapUri}");

        try
        {
            StatusText = "Подключение...";
            BootstrapConfiguration bootstrap = await _serverClient.GetBootstrapAsync(
                bootstrapUri!,
                CancellationToken.None);

            StatusText = "Подключение...";
            IReadOnlyList<GameProfile> profiles = await _serverClient.GetProfilesAsync(
                bootstrap,
                CancellationToken.None);

            GameProfile selected = SelectProfile(profiles, bootstrap.DefaultProfileId, _settings.SelectedProfileId);
            LauncherSettings newSettings = new()
            {
                ServerUrl = bootstrap.BootstrapUri.AbsoluteUri,
                Nickname = Nickname,
                SelectedProfileId = selected.Id,
                RamMb = RamMb,
            };
            await SaveSettingsAsync(newSettings);

            _settings = newSettings;
            _bootstrap = bootstrap;
            Profiles.Clear();
            foreach (GameProfile profile in profiles)
            {
                Profiles.Add(profile);
            }

            _selectedProfile = selected;
            OnPropertyChanged(nameof(SelectedProfile));
            ServerName = bootstrap.ServerName;
            ServerUrl = bootstrap.BootstrapUri.AbsoluteUri;
            IsChangingServer = false;
            Screen = LauncherScreen.Main;
            StatusText = "Готово";
            _logger.Info($"Connected to '{bootstrap.ServerName}' with {profiles.Count} profile(s).");
        }
        catch (ServerConnectionException exception)
        {
            _logger.Error(exception.Message, exception);
            ErrorText = exception.UserMessage;
            StatusText = "Ошибка";
            HasConnectionFailure = connectionScreen == LauncherScreen.Onboarding;
            Screen = connectionScreen == LauncherScreen.Settings
                ? LauncherScreen.Settings
                : LauncherScreen.Onboarding;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Could not persist launcher settings.", exception);
            ErrorText = "Подключение выполнено, но настройки не удалось сохранить.";
            StatusText = "Ошибка";
            HasConnectionFailure = connectionScreen == LauncherScreen.Onboarding;
            Screen = connectionScreen == LauncherScreen.Settings
                ? LauncherScreen.Settings
                : LauncherScreen.Onboarding;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenSettings()
    {
        _settingsReturnScreen = Screen;
        _serverUrlBeforeSettings = ServerUrl;
        IsChangingServer = true;
        ErrorText = null;
        ServerUrl = _bootstrap?.BootstrapUri.AbsoluteUri
            ?? (string.IsNullOrWhiteSpace(ServerUrl) ? _settings.ServerUrl ?? string.Empty : ServerUrl);
        Screen = LauncherScreen.Settings;
    }

    private void CancelSettings()
    {
        IsChangingServer = false;
        ErrorText = null;

        if (_bootstrap is null)
        {
            ServerUrl = _serverUrlBeforeSettings;
            Screen = _settingsReturnScreen;
            return;
        }

        ServerUrl = _bootstrap.BootstrapUri.AbsoluteUri;
        Screen = LauncherScreen.Main;
        StatusText = "Готово";
    }

    private void BeginServerChange()
    {
        HasConnectionFailure = false;
        ErrorText = null;
        ServerUrl = string.Empty;
        StatusText = "Ожидание";
    }

    private async Task CheckForUpdatesAsync()
    {
        IsBusy = true;
        UpdateStatusText = "Проверка обновлений...";
        IsUpdateProgressVisible = false;
        IsUpdateDownloaded = false;
        SetAvailableUpdate(null);

        try
        {
            LauncherUpdateInfo? update = await _updateCoordinator.CheckForUpdatesAsync(CancellationToken.None);
            SetAvailableUpdate(update);
            UpdateStatusText = update is null
                ? "Установлена актуальная версия."
                : $"Доступна версия {update.Version}.";
        }
        catch (LauncherUpdateException exception)
        {
            _logger.Error(exception.Message, exception);
            UpdateStatusText = exception.UserMessage;
        }
        catch (Exception exception)
        {
            _logger.Error("Unexpected Launcher update check failure.", exception);
            UpdateStatusText = "Ошибка проверки обновления. Launcher продолжит работать.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DownloadUpdateAsync()
    {
        if (_availableUpdate is null)
        {
            UpdateStatusText = "Сначала проверьте наличие обновления.";
            return;
        }

        IsBusy = true;
        IsUpdateProgressVisible = true;
        UpdateProgressValue = 0;
        UpdateStatusText = "Загрузка обновления...";
        Progress<int> progress = new(value => UpdateProgressValue = value);

        try
        {
            await _updateCoordinator.DownloadUpdateAsync(
                _availableUpdate,
                progress,
                CancellationToken.None);
            UpdateProgressValue = 100;
            IsUpdateDownloaded = true;
            UpdateStatusText = "Обновление загружено. Перезапустите Launcher для установки.";
        }
        catch (LauncherUpdateException exception)
        {
            _logger.Error(exception.Message, exception);
            IsUpdateProgressVisible = false;
            UpdateStatusText = exception.UserMessage;
        }
        catch (Exception exception)
        {
            _logger.Error("Unexpected Launcher update download failure.", exception);
            IsUpdateProgressVisible = false;
            UpdateStatusText = "Не удалось скачать обновление.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyUpdateAndRestart()
    {
        if (_availableUpdate is null || !IsUpdateDownloaded)
        {
            UpdateStatusText = "Сначала скачайте обновление.";
            return;
        }

        try
        {
            UpdateStatusText = "Перезапуск для установки обновления...";
            _updateCoordinator.ApplyUpdateAndRestart(_availableUpdate);
        }
        catch (LauncherUpdateException exception)
        {
            _logger.Error(exception.Message, exception);
            UpdateStatusText = exception.UserMessage;
        }
        catch (Exception exception)
        {
            _logger.Error("Unexpected Launcher update apply failure.", exception);
            UpdateStatusText = "Не удалось применить обновление и перезапустить Launcher.";
        }
    }

    private void SetAvailableUpdate(LauncherUpdateInfo? update)
    {
        _availableUpdate = update;
        OnPropertyChanged(nameof(HasAvailableUpdate));
        DownloadUpdateCommand.RaiseCanExecuteChanged();
        ApplyUpdateCommand.RaiseCanExecuteChanged();
    }

    private async Task PlayAsync()
    {
        ErrorText = null;
        if (_bootstrap is null)
        {
            ErrorText = "Сначала подключитесь к серверу.";
        }
        else if (SelectedProfile is null)
        {
            ErrorText = "Выберите игровую сборку.";
        }
        else if (NicknameValidator.GetError(Nickname) is string nicknameError)
        {
            ErrorText = nicknameError;
        }
        else if (!RamPolicy.IsValid(RamMb, _totalMemoryMb))
        {
            ErrorText = "Выберите допустимый объём RAM.";
        }
        else
        {
            GameProfile selectedProfile = SelectedProfile;
            GameLaunchRequest request;
            try
            {
                request = GameLaunchRequestFactory.Create(selectedProfile, Nickname, RamMb);
            }
            catch (GameLaunchException exception)
            {
                ErrorText = exception.UserMessage;
                StatusText = "Ошибка";
                return;
            }

            ScheduleSettingsSave();
            IsBusy = true;
            IsGamePreparing = true;
            IsGameRunning = false;
            IsProgressIndeterminate = true;
            ProgressValue = 0;
            ProgressDetails = string.Empty;
            PlayButtonText = "ПОДГОТОВКА...";
            _gameLaunchCancellation = new CancellationTokenSource();
            Progress<PackSyncProgress> packProgress = new(UpdatePackProgress);
            Progress<GameLaunchProgress> gameProgress = new(UpdateGameProgress);

            try
            {
                (PackSyncResult _, GameLaunchResult result) = await Task.Run(() =>
                    _operationCoordinator.PlayAsync(
                        selectedProfile,
                        request,
                        packProgress,
                        gameProgress,
                        _gameLaunchCancellation.Token));

                if (result.ExitCode == 0)
                {
                    StatusText = "Minecraft завершён";
                }
                else
                {
                    StatusText = "Minecraft завершился с ошибкой";
                    ErrorText = $"Код завершения: {result.ExitCode}. Подробности записаны в лог.";
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "Операция отменена";
                ProgressDetails = string.Empty;
            }
            catch (PackSyncException exception)
            {
                _logger.Error(exception.Message, exception);
                StatusText = "Ошибка";
                ErrorText = exception.UserMessage;
            }
            catch (GameLaunchException exception)
            {
                _logger.Error(exception.Message, exception);
                StatusText = "Ошибка";
                ErrorText = exception.UserMessage;
            }
            catch (Exception exception)
            {
                _logger.Error("Unexpected game launch failure.", exception);
                StatusText = "Ошибка";
                ErrorText = "Не удалось подготовить или запустить Minecraft. Подробности записаны в лог.";
            }
            finally
            {
                _gameLaunchCancellation.Dispose();
                _gameLaunchCancellation = null;
                IsGamePreparing = false;
                IsGameRunning = false;
                IsProgressIndeterminate = true;
                PlayButtonText = "ИГРАТЬ";
                IsBusy = false;
            }
        }
    }

    private async Task RepairAsync()
    {
        ErrorText = null;
        if (_bootstrap is null || SelectedProfile is null)
        {
            ErrorText = "Сначала подключитесь к серверу и выберите игровую сборку.";
            return;
        }

        GameProfile selectedProfile = SelectedProfile;
        IsBusy = true;
        IsGamePreparing = true;
        IsGameRunning = false;
        IsProgressIndeterminate = true;
        ProgressValue = 0;
        ProgressDetails = string.Empty;
        _gameLaunchCancellation = new CancellationTokenSource();
        Progress<PackSyncProgress> progress = new(UpdatePackProgress);

        try
        {
            PackSyncResult result = await Task.Run(() =>
                _operationCoordinator.RepairAsync(
                    selectedProfile,
                    progress,
                    _gameLaunchCancellation.Token));
            if (result.DownloadedFiles == 0 && result.DeletedFiles == 0)
            {
                StatusText = "Файлы сборки уже исправны.";
            }
            else
            {
                StatusText = $"Восстановлено файлов: {result.DownloadedFiles}; удалено устаревших: {result.DeletedFiles}.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Проверка файлов отменена";
            ProgressDetails = string.Empty;
        }
        catch (PackSyncException exception)
        {
            _logger.Error(exception.Message, exception);
            StatusText = "Ошибка";
            ErrorText = exception.UserMessage;
        }
        catch (Exception exception)
        {
            _logger.Error("Unexpected pack repair failure.", exception);
            StatusText = "Ошибка";
            ErrorText = "Не удалось проверить файлы сборки. Подробности записаны в лог.";
        }
        finally
        {
            _gameLaunchCancellation.Dispose();
            _gameLaunchCancellation = null;
            IsGamePreparing = false;
            IsProgressIndeterminate = true;
            IsBusy = false;
        }
    }

    private void CancelGame()
    {
        if (!IsGamePreparing || _gameLaunchCancellation is null)
        {
            return;
        }

        StatusText = "Отмена операции...";
        CancelGameCommand.RaiseCanExecuteChanged();
        _gameLaunchCancellation.Cancel();
    }

    private void UpdateGameProgress(GameLaunchProgress progress)
    {
        StatusText = GetFriendlyStatus(progress.Stage);
        if (progress.Percentage is double percentage)
        {
            IsProgressIndeterminate = false;
            ProgressValue = percentage;
        }
        else
        {
            IsProgressIndeterminate = progress.Stage is not GameLaunchStage.MinecraftStarted;
        }

        ProgressDetails = FormatProgressDetails(progress);

        if (progress.Stage == GameLaunchStage.MinecraftStarted)
        {
            IsGamePreparing = false;
            IsGameRunning = true;
            PlayButtonText = "MINECRAFT ЗАПУЩЕН";
            ProgressValue = 100;
        }
        else if (progress.Stage == GameLaunchStage.MinecraftExited)
        {
            IsGameRunning = false;
        }
    }

    private void UpdatePackProgress(PackSyncProgress progress)
    {
        StatusText = GetFriendlyStatus(progress.Stage);
        if (progress.Percentage is double percentage)
        {
            IsProgressIndeterminate = false;
            ProgressValue = percentage;
        }
        else
        {
            IsProgressIndeterminate = true;
        }

        ProgressDetails = FormatProgressDetails(progress);
    }

    private static string FormatProgressDetails(GameLaunchProgress progress)
    {
        if (progress.TotalBytes > 0 && progress.CompletedBytes is long completedBytes)
        {
            return $"{FormatBytes(completedBytes)} / {FormatBytes(progress.TotalBytes.Value)}";
        }

        if (progress.TotalFiles > 0 && progress.CompletedFiles is int completedFiles)
        {
            return $"{completedFiles} / {progress.TotalFiles.Value} файлов";
        }

        return string.Empty;
    }

    private static string FormatProgressDetails(PackSyncProgress progress)
    {
        if (progress.TotalBytes > 0 && progress.CompletedBytes is long completedBytes)
        {
            string files = progress.TotalFiles > 0 && progress.CompletedFiles is int completedFiles
                ? $"{completedFiles} / {progress.TotalFiles.Value} файлов; "
                : string.Empty;
            return $"{files}{FormatBytes(completedBytes)} / {FormatBytes(progress.TotalBytes.Value)}";
        }

        return progress.TotalFiles > 0 && progress.CompletedFiles is int completed
            ? $"{completed} / {progress.TotalFiles.Value} файлов"
            : string.Empty;
    }

    private static string GetFriendlyStatus(GameLaunchStage stage) => stage switch
    {
        GameLaunchStage.DownloadingMinecraft => "Загрузка Minecraft...",
        GameLaunchStage.StartingMinecraft => "Запуск Minecraft...",
        GameLaunchStage.MinecraftStarted => "Minecraft запущен",
        GameLaunchStage.MinecraftExited => "Minecraft завершён",
        GameLaunchStage.Error => "Ошибка",
        _ => "Подготовка Minecraft...",
    };

    private static string GetFriendlyStatus(PackSyncStage stage) => stage switch
    {
        PackSyncStage.FetchingManifest or PackSyncStage.DownloadingFiles => "Загрузка сборки...",
        PackSyncStage.ApplyingUpdate or PackSyncStage.RemovingObsoleteFiles => "Обновление файлов...",
        PackSyncStage.Complete => "Готово",
        _ => "Проверка файлов...",
    };

    private static string FormatBytes(long bytes) => $"{bytes / 1024d / 1024d:0.#} MB";

    private bool CanConnect() =>
        !IsBusy && ServerUrlNormalizer.TryNormalize(ServerUrl, out _, out _);

    private void ScheduleSettingsSave()
    {
        if (_bootstrap is null)
        {
            return;
        }

        _ = SaveCurrentSettingsSafeAsync();
    }

    private async Task SaveCurrentSettingsSafeAsync()
    {
        try
        {
            LauncherSettings settings = new()
            {
                ServerUrl = _bootstrap?.BootstrapUri.AbsoluteUri,
                Nickname = Nickname,
                SelectedProfileId = SelectedProfile?.Id,
                RamMb = RamMb,
            };
            await SaveSettingsAsync(settings);
            _settings = settings;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Could not save launcher settings.", exception);
            ErrorText = "Не удалось сохранить настройки.";
        }
    }

    private async Task SaveSettingsAsync(LauncherSettings settings)
    {
        await _saveLock.WaitAsync();
        try
        {
            await _settingsStore.SaveAsync(settings);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private static GameProfile SelectProfile(
        IReadOnlyList<GameProfile> profiles,
        string? defaultProfileId,
        string? savedProfileId) =>
        FindProfile(profiles, savedProfileId) ??
        FindProfile(profiles, defaultProfileId) ??
        profiles[0];

    private static GameProfile? FindProfile(IReadOnlyList<GameProfile> profiles, string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : profiles.FirstOrDefault(profile => profile.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
