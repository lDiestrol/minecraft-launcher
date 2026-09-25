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
    private readonly GameLaunchCoordinator _gameLaunchCoordinator;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private LauncherSettings _settings = new();
    private BootstrapConfiguration? _bootstrap;
    private LauncherScreen _screen = LauncherScreen.Onboarding;
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
    private bool _isGamePreparing;
    private bool _isGameRunning;
    private bool _isProgressIndeterminate = true;
    private double _progressValue;
    private string _progressDetails = string.Empty;
    private string _playButtonText = "ИГРАТЬ";
    private CancellationTokenSource? _gameLaunchCancellation;

    public MainViewModel(
        ILauncherServerClient serverClient,
        ISettingsStore settingsStore,
        ISystemMemoryProvider memoryProvider,
        GameLaunchCoordinator gameLaunchCoordinator,
        IAppLogger logger)
    {
        _serverClient = serverClient;
        _settingsStore = settingsStore;
        _memoryProvider = memoryProvider;
        _gameLaunchCoordinator = gameLaunchCoordinator;
        _logger = logger;

        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !IsBusy);
        PlayCommand = new AsyncRelayCommand(PlayAsync, () => !IsBusy);
        CancelGameCommand = new RelayCommand(CancelGame, () => IsGamePreparing);
        OpenSettingsCommand = new RelayCommand(OpenSettings, () => !IsBusy);
        CancelSettingsCommand = new RelayCommand(CancelSettings, () => !IsBusy);
    }

    public string Version => "0.2.0-dev";

    public ObservableCollection<GameProfile> Profiles { get; } = [];

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand PlayCommand { get; }

    public RelayCommand CancelGameCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand CancelSettingsCommand { get; }

    public LauncherScreen Screen
    {
        get => _screen;
        private set => SetProperty(ref _screen, value);
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set => SetProperty(ref _serverUrl, value);
    }

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
                OpenSettingsCommand.RaiseCanExecuteChanged();
                CancelSettingsCommand.RaiseCanExecuteChanged();
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

        IsBusy = true;
        _logger.Info($"Connecting to launcher server: {bootstrapUri}");

        try
        {
            StatusText = "Получение конфигурации";
            BootstrapConfiguration bootstrap = await _serverClient.GetBootstrapAsync(
                bootstrapUri!,
                CancellationToken.None);

            StatusText = "Получение профилей";
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
            Screen = LauncherScreen.Onboarding;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Could not persist launcher settings.", exception);
            ErrorText = "Подключение выполнено, но настройки не удалось сохранить.";
            StatusText = "Ошибка";
            Screen = LauncherScreen.Onboarding;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenSettings()
    {
        IsChangingServer = true;
        ErrorText = null;
        ServerUrl = _bootstrap?.BootstrapUri.AbsoluteUri ?? _settings.ServerUrl ?? string.Empty;
        Screen = LauncherScreen.Onboarding;
        StatusText = "Ожидание";
    }

    private void CancelSettings()
    {
        if (_bootstrap is null)
        {
            return;
        }

        IsChangingServer = false;
        ErrorText = null;
        ServerUrl = _bootstrap.BootstrapUri.AbsoluteUri;
        Screen = LauncherScreen.Main;
        StatusText = "Готово";
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
            GameLaunchRequest request;
            try
            {
                request = GameLaunchRequestFactory.Create(SelectedProfile, Nickname, RamMb);
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
            Progress<GameLaunchProgress> progress = new(UpdateGameProgress);

            try
            {
                GameLaunchResult result = await _gameLaunchCoordinator.LaunchAsync(
                    request,
                    progress,
                    _gameLaunchCancellation.Token);

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
                StatusText = "Подготовка Minecraft отменена";
                ProgressDetails = string.Empty;
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

    private void CancelGame()
    {
        if (!IsGamePreparing || _gameLaunchCancellation is null)
        {
            return;
        }

        StatusText = "Отмена подготовки...";
        CancelGameCommand.RaiseCanExecuteChanged();
        _gameLaunchCancellation.Cancel();
    }

    private void UpdateGameProgress(GameLaunchProgress progress)
    {
        StatusText = progress.Message;
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

        return progress.ProcessId is int processId ? $"PID: {processId}" : string.Empty;
    }

    private static string FormatBytes(long bytes) => $"{bytes / 1024d / 1024d:0.#} MB";

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
