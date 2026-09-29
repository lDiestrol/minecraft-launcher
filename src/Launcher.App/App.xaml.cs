using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Launcher.App.ViewModels;
using Launcher.Core;
using Launcher.Core.Services;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Http;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Pack;
using Launcher.Infrastructure.Persistence;
using Launcher.Infrastructure.System;
using Launcher.Infrastructure.Updates;

namespace Launcher.App;

public partial class App : Application
{
    private HttpClient? _httpClient;
    private HttpClient? _packHttpClient;
    private HttpClient? _gameHttpClient;
    private IAppLogger? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LauncherDataPaths paths = new();
        _logger = new FileAppLogger(paths);
        _logger.Info($"Minecraft Launcher {LauncherVersion.Current} starting.");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            _httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
            })
            {
                Timeout = TimeSpan.FromSeconds(10),
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"MinecraftLauncher/{LauncherVersion.Current}");

            _packHttpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            })
            {
                Timeout = TimeSpan.FromMinutes(10),
            };
            _packHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"MinecraftLauncher/{LauncherVersion.Current}");

            _gameHttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10),
            };
            _gameHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"MinecraftLauncher/{LauncherVersion.Current}");

            CmlLibGameLaunchService gameLaunchService = new(_gameHttpClient, paths, _logger);
            PackSyncService packSyncService = new(_packHttpClient, paths, _logger);
            LauncherOperationCoordinator operationCoordinator = new(packSyncService, gameLaunchService);
            VelopackLauncherUpdateService updateService = new(_logger);
            LauncherUpdateCoordinator updateCoordinator = new(updateService, operationCoordinator);
            string executionMode = updateService.IsPortable
                ? "portable"
                : updateService.IsInstalled
                    ? "installed"
                    : "development";
            _logger.Info($"Launcher execution mode: {executionMode}.");

            MainViewModel viewModel = new(
                new LauncherServerClient(_httpClient, _logger),
                new JsonSettingsStore(paths, _logger),
                new WindowsSystemMemoryProvider(),
                operationCoordinator,
                updateCoordinator,
                _logger);

            await viewModel.InitializeAsync();

            _logger.Info("Creating main window.");
            MainWindow window = new()
            {
                DataContext = viewModel,
            };
            _logger.Info("Main window created.");
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
            _logger.Info("Main window shown.");
        }
        catch (Exception exception)
        {
            _logger.Error("Fatal error during application startup.", exception);
            MessageBox.Show(
                "Launcher не удалось запустить. Подробности записаны в лог.",
                "Minecraft Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Info("Minecraft Launcher stopped.");
        _httpClient?.Dispose();
        _packHttpClient?.Dispose();
        _gameHttpClient?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("Unhandled UI exception.", e.Exception);
        MessageBox.Show(
            "Произошла непредвиденная ошибка. Подробности записаны в лог.",
            "Minecraft Launcher",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger?.Error("Unhandled application exception.", e.ExceptionObject as Exception);
    }
}
