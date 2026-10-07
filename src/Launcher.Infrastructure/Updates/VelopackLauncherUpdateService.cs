using Launcher.Core;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace Launcher.Infrastructure.Updates;

public sealed class VelopackLauncherUpdateService : ILauncherUpdateService
{
    public const string UpdateRepositoryUrl = "https://github.com/lDiestrol/minecraft-launcher";

    private readonly UpdateManager _updateManager;
    private readonly IAppLogger _logger;
    private UpdateInfo? _availableUpdate;
    private VelopackAsset? _downloadedUpdate;
    private int _operationActive;

    public VelopackLauncherUpdateService(IAppLogger logger)
        : this(
            new UpdateManager(new GithubSource(
                UpdateRepositoryUrl,
                accessToken: null,
                prerelease: true,
                downloader: null)),
            logger)
    {
    }

    internal VelopackLauncherUpdateService(UpdateManager updateManager, IAppLogger logger)
    {
        _updateManager = updateManager;
        _logger = logger;
    }

    public string CurrentVersion => LauncherVersion.Current;

    public bool IsInstalled => _updateManager.IsInstalled;

    public bool IsPortable => _updateManager.IsPortable;

    public async Task<LauncherUpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        EnsureInstalled();
        EnterOperation();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.Info("Update check started.");
            UpdateInfo? update = await _updateManager.CheckForUpdatesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            _availableUpdate = update;
            _downloadedUpdate = null;

            if (update is null)
            {
                _logger.Info("Update check completed: no update.");
                return null;
            }

            string version = update.TargetFullRelease.Version.ToString();
            _logger.Info($"Update available: current={CurrentVersion}, available={version}.");
            return new LauncherUpdateInfo(version);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (NotInstalledException exception)
        {
            throw NotInstalled(exception);
        }
        catch (Exception exception)
        {
            throw new LauncherUpdateException(
                LauncherUpdateError.CheckFailed,
                "Не удалось проверить обновления. Launcher и запуск Minecraft продолжат работать.",
                "Velopack update check failed.",
                exception);
        }
        finally
        {
            ExitOperation();
        }
    }

    public async Task DownloadUpdateAsync(
        LauncherUpdateInfo update,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        EnsureInstalled();
        UpdateInfo selectedUpdate = GetSelectedUpdate(update);
        EnterOperation();
        try
        {
            _logger.Info($"Downloading Launcher update {update.Version}.");
            await _updateManager.DownloadUpdatesAsync(
                selectedUpdate,
                value => progress?.Report(value),
                cancellationToken);
            _downloadedUpdate = selectedUpdate.TargetFullRelease;
            _logger.Info($"Launcher update {update.Version} downloaded and verified.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (NotInstalledException exception)
        {
            throw NotInstalled(exception);
        }
        catch (Exception exception)
        {
            _downloadedUpdate = null;
            throw new LauncherUpdateException(
                LauncherUpdateError.DownloadFailed,
                "Не удалось скачать или проверить пакет обновления.",
                "Velopack update download or package verification failed.",
                exception);
        }
        finally
        {
            ExitOperation();
        }
    }

    public void ApplyUpdateAndRestart(LauncherUpdateInfo update)
    {
        EnsureInstalled();
        VelopackAsset downloadedUpdate = GetDownloadedUpdate(update);
        EnterOperation();
        try
        {
            _logger.Info($"User requested apply/restart for Launcher update {update.Version}.");
            _updateManager.ApplyUpdatesAndRestart(downloadedUpdate);
        }
        catch (NotInstalledException exception)
        {
            ExitOperation();
            throw NotInstalled(exception);
        }
        catch (Exception exception)
        {
            ExitOperation();
            throw new LauncherUpdateException(
                LauncherUpdateError.ApplyFailed,
                "Не удалось применить обновление и перезапустить Launcher.",
                "Velopack update apply/restart failed.",
                exception);
        }
    }

    private void EnsureInstalled()
    {
        if (!_updateManager.IsInstalled)
        {
            throw NotInstalled();
        }
    }

    private UpdateInfo GetSelectedUpdate(LauncherUpdateInfo update)
    {
        if (_availableUpdate is null ||
            !_availableUpdate.TargetFullRelease.Version.ToString().Equals(update.Version, StringComparison.Ordinal))
        {
            throw new LauncherUpdateException(
                LauncherUpdateError.UpdateNotSelected,
                "Сначала проверьте наличие обновления.",
                "Download was requested without a matching checked update.");
        }

        return _availableUpdate;
    }

    private VelopackAsset GetDownloadedUpdate(LauncherUpdateInfo update)
    {
        if (_downloadedUpdate is null ||
            !_downloadedUpdate.Version.ToString().Equals(update.Version, StringComparison.Ordinal))
        {
            throw new LauncherUpdateException(
                LauncherUpdateError.UpdateNotDownloaded,
                "Сначала скачайте обновление.",
                "Apply was requested before a matching update was downloaded.");
        }

        return _downloadedUpdate;
    }

    private void EnterOperation()
    {
        if (Interlocked.CompareExchange(ref _operationActive, 1, 0) != 0)
        {
            throw new LauncherUpdateException(
                LauncherUpdateError.AlreadyRunning,
                "Другая операция обновления уже выполняется.",
                "A Launcher update operation is already active.");
        }
    }

    private void ExitOperation() => Volatile.Write(ref _operationActive, 0);

    private static LauncherUpdateException NotInstalled(Exception? innerException = null) =>
        new(
            LauncherUpdateError.NotInstalled,
            "Обновления доступны в установленной версии Launcher.",
            "Velopack reports that the application is not installed.",
            innerException);
}
