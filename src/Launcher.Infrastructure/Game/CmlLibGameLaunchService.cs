using System.Diagnostics;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Version;
using Launcher.Core.Models;
using Launcher.Core.Policies;
using Launcher.Core.Services;
using Launcher.Core.Validation;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Infrastructure.Game;

public sealed class CmlLibGameLaunchService : IGameLaunchService
{
    private readonly HttpClient _httpClient;
    private readonly LauncherDataPaths _paths;
    private readonly IAppLogger _logger;

    public CmlLibGameLaunchService(HttpClient httpClient, LauncherDataPaths paths, IAppLogger logger)
    {
        _httpClient = httpClient;
        _paths = paths;
        _logger = logger;
    }

    public async Task<GameLaunchResult> LaunchAsync(
        GameLaunchRequest request,
        IProgress<GameLaunchProgress> progress,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        GameLaunchStage currentStage = GameLaunchStage.Preparing;
        try
        {
            Report(progress, currentStage, "Подготовка игровой директории");
            string instanceDirectory = PrepareDirectories(request.ProfileId);
            EnsureFreeDiskSpace(instanceDirectory);

            _logger.Info(
                $"Game launch requested: profile={request.ProfileId}, Minecraft={request.MinecraftVersion}, " +
                $"loader=Fabric {request.LoaderVersion}, instance={instanceDirectory}.");

            MinecraftPath sharedPath = new(_paths.RootDirectory);
            MinecraftPath instancePath = CreateInstancePath(instanceDirectory, sharedPath);
            MinecraftLauncherParameters parameters = MinecraftLauncherParameters.CreateDefault(sharedPath, _httpClient);
            MinecraftLauncher launcher = new(parameters);
            DownloadProgressAdapter downloadProgress = new(progress, () => currentStage);

            currentStage = GameLaunchStage.CheckingMinecraft;
            Report(progress, currentStage, $"Проверка Minecraft {request.MinecraftVersion}");
            _logger.Info($"Installing or verifying vanilla Minecraft {request.MinecraftVersion} and its runtime.");

            currentStage = GameLaunchStage.DownloadingMinecraft;
            await launcher.InstallAsync(
                request.MinecraftVersion,
                downloadProgress.FileProgress,
                downloadProgress.ByteProgress,
                cancellationToken);
            _logger.Info($"Minecraft {request.MinecraftVersion} files and runtime verified.");

            currentStage = GameLaunchStage.PreparingJava;
            Report(progress, currentStage, "Проверка Java runtime");
            IVersion vanillaVersion = await launcher.GetVersionAsync(request.MinecraftVersion, cancellationToken);
            string vanillaJava = launcher.GetJavaPath(vanillaVersion) ?? throw new GameLaunchException(
                GameLaunchError.JavaPreparationFailed,
                "Не удалось подготовить Java для Minecraft.",
                $"CmlLib did not resolve Java for Minecraft {request.MinecraftVersion}.");
            _logger.Info($"Mojang Java runtime prepared: {vanillaJava}.");

            currentStage = GameLaunchStage.InstallingFabric;
            Report(progress, currentStage, $"Установка Fabric {request.LoaderVersion}");
            _logger.Info($"Installing exact Fabric loader {request.LoaderVersion} for {request.MinecraftVersion}.");
            FabricInstaller fabricInstaller = new(_httpClient);
            IReadOnlyCollection<FabricLoader> loaders = await fabricInstaller.GetLoaders(request.MinecraftVersion);
            bool loaderExists = loaders.Any(loader =>
                request.LoaderVersion.Equals(loader.Version, StringComparison.Ordinal));
            if (!loaderExists)
            {
                throw new GameLaunchException(
                    GameLaunchError.FabricVersionUnavailable,
                    $"Версия Fabric {request.LoaderVersion} не существует или несовместима с Minecraft {request.MinecraftVersion}.",
                    $"Fabric loader {request.LoaderVersion} was not returned for Minecraft {request.MinecraftVersion}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            string installedVersionId = await fabricInstaller.Install(
                request.MinecraftVersion,
                request.LoaderVersion,
                sharedPath);
            cancellationToken.ThrowIfCancellationRequested();
            _logger.Info($"Fabric profile installed with CmlLib version id '{installedVersionId}'.");

            currentStage = GameLaunchStage.CheckingFiles;
            Report(progress, currentStage, "Проверка файлов Fabric-профиля");
            await launcher.InstallAsync(
                installedVersionId,
                downloadProgress.FileProgress,
                downloadProgress.ByteProgress,
                cancellationToken);
            _logger.Info($"Fabric profile '{installedVersionId}' files verified.");

            currentStage = GameLaunchStage.PreparingJava;
            Report(progress, currentStage, "Подготовка Java для запуска");
            IVersion fabricVersion = await launcher.GetVersionAsync(installedVersionId, cancellationToken);
            string javaPath = launcher.GetJavaPath(fabricVersion) ?? throw new GameLaunchException(
                GameLaunchError.JavaPreparationFailed,
                "Не удалось подготовить Java для Minecraft.",
                $"CmlLib did not resolve Java for version '{installedVersionId}'.");
            if (!File.Exists(javaPath))
            {
                throw new GameLaunchException(
                    GameLaunchError.JavaPreparationFailed,
                    "Подготовленная Java runtime не найдена.",
                    $"Resolved Java executable does not exist: {javaPath}.");
            }

            _logger.Info($"Java executable selected: {javaPath}.");

            currentStage = GameLaunchStage.PreparingLaunch;
            Report(progress, currentStage, "Подготовка команды запуска");
            MLaunchOption launchOption = new()
            {
                Session = MSession.CreateOfflineSession(request.Nickname),
                MaximumRamMb = request.RamMb,
                MinimumRamMb = Math.Min(1024, request.RamMb),
                JavaPath = javaPath,
                Path = instancePath,
                ServerIp = request.ServerAddress,
                ServerPort = request.ServerPort,
                GameLauncherName = "MinecraftLauncher",
                GameLauncherVersion = "0.2.0-dev",
            };

            Process process = await launcher.BuildProcessAsync(installedVersionId, launchOption, cancellationToken);
            ProcessWrapper processWrapper = new(process);
            processWrapper.OutputReceived += (_, line) => _logger.Info($"[Minecraft] {line}");

            currentStage = GameLaunchStage.StartingMinecraft;
            Report(progress, currentStage, "Запуск Minecraft");
            processWrapper.StartWithEvents();
            int processId = processWrapper.Process.Id;
            _logger.Info($"Minecraft process started: PID={processId}, versionId={installedVersionId}.");
            Report(
                progress,
                GameLaunchStage.MinecraftStarted,
                "Minecraft запущен",
                processId);

            int exitCode = await processWrapper.WaitForExitTaskAsync();
            _logger.Info($"Minecraft process PID={processId} exited with code {exitCode}.");
            Report(
                progress,
                GameLaunchStage.MinecraftExited,
                exitCode == 0 ? "Minecraft завершён" : "Minecraft завершился с ошибкой",
                processId);

            return new GameLaunchResult(
                processId,
                exitCode,
                installedVersionId,
                javaPath,
                instanceDirectory);
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Game preparation was cancelled.");
            throw;
        }
        catch (GameLaunchException)
        {
            throw;
        }
        catch (Exception exception)
        {
            GameLaunchException mapped = GameLaunchErrorMapper.Map(exception, currentStage);
            _logger.Error(mapped.Message, exception);
            throw mapped;
        }
    }

    private static MinecraftPath CreateInstancePath(string instanceDirectory, MinecraftPath sharedPath) =>
        new(instanceDirectory)
        {
            Assets = sharedPath.Assets,
            Library = sharedPath.Library,
            Resource = sharedPath.Resource,
            Runtime = sharedPath.Runtime,
            Versions = sharedPath.Versions,
        };

    private string PrepareDirectories(string profileId)
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        Directory.CreateDirectory(_paths.InstancesDirectory);
        string instanceDirectory = _paths.GetInstanceDirectory(profileId);
        Directory.CreateDirectory(instanceDirectory);
        Directory.CreateDirectory(Path.Combine(instanceDirectory, "mods"));
        Directory.CreateDirectory(Path.Combine(instanceDirectory, "config"));
        Directory.CreateDirectory(Path.Combine(instanceDirectory, "saves"));
        Directory.CreateDirectory(Path.Combine(instanceDirectory, "screenshots"));
        return instanceDirectory;
    }

    private static void EnsureFreeDiskSpace(string instanceDirectory)
    {
        string fullPath = Path.GetFullPath(instanceDirectory);
        string root = Path.GetPathRoot(fullPath) ?? throw new IOException(
            $"Could not determine drive root for {fullPath}.");
        long availableBytes = new DriveInfo(root).AvailableFreeSpace;
        if (!DiskSpacePolicy.HasEnoughSpace(availableBytes))
        {
            throw new GameLaunchException(
                GameLaunchError.InsufficientDiskSpace,
                "Недостаточно свободного места. Для первой установки требуется не менее 4 GiB.",
                $"Only {availableBytes} bytes are available on {root}.");
        }
    }

    private static void ValidateRequest(GameLaunchRequest request)
    {
        if (!ProfileValueValidator.IsValidProfileId(request.ProfileId) ||
            !ProfileValueValidator.IsValidVersion(request.MinecraftVersion) ||
            !ProfileValueValidator.IsValidVersion(request.LoaderVersion) ||
            !ProfileValueValidator.IsValidServerAddress(request.ServerAddress) ||
            !NicknameValidator.IsValid(request.Nickname) ||
            request.RamMb < RamPolicy.MinimumRamMb)
        {
            throw new GameLaunchException(
                GameLaunchError.InvalidProfile,
                "Профиль сервера содержит недопустимые значения.",
                "Unsafe profile values reached CmlLib launch boundary.");
        }

        if (!request.LoaderType.Equals("fabric", StringComparison.OrdinalIgnoreCase))
        {
            throw new GameLaunchException(
                GameLaunchError.UnsupportedLoader,
                $"Эта версия Launcher пока не поддерживает загрузчик {request.LoaderType}.",
                $"Unsupported loader type '{request.LoaderType}'.");
        }
    }

    private static void Report(
        IProgress<GameLaunchProgress> progress,
        GameLaunchStage stage,
        string message,
        int? processId = null) =>
        progress.Report(new GameLaunchProgress(stage, message, ProcessId: processId));

    private sealed class DownloadProgressAdapter
    {
        private readonly IProgress<GameLaunchProgress> _progress;
        private readonly Func<GameLaunchStage> _stageProvider;
        private long _lastFileReportTimestamp;

        public DownloadProgressAdapter(
            IProgress<GameLaunchProgress> progress,
            Func<GameLaunchStage> stageProvider)
        {
            _progress = progress;
            _stageProvider = stageProvider;
            FileProgress = new InlineProgress<InstallerProgressChangedEventArgs>(ReportFileProgress);
            ByteProgress = new InlineProgress<ByteProgress>(ReportByteProgress);
        }

        public IProgress<InstallerProgressChangedEventArgs> FileProgress { get; }

        public IProgress<ByteProgress> ByteProgress { get; }

        private void ReportFileProgress(InstallerProgressChangedEventArgs value)
        {
            long now = Stopwatch.GetTimestamp();
            long previous = Interlocked.Read(ref _lastFileReportTimestamp);
            bool completed = value.TotalTasks > 0 && value.ProgressedTasks >= value.TotalTasks;
            if (!completed && previous != 0 && Stopwatch.GetElapsedTime(previous, now) < TimeSpan.FromMilliseconds(100))
            {
                return;
            }

            Interlocked.Exchange(ref _lastFileReportTimestamp, now);
            _progress.Report(new GameLaunchProgress(
                _stageProvider(),
                value.Name ?? "Проверка файлов Minecraft",
                value.TotalTasks,
                value.ProgressedTasks));
        }

        private void ReportByteProgress(ByteProgress value) =>
            _progress.Report(new GameLaunchProgress(
                _stageProvider(),
                "Загрузка файлов Minecraft",
                TotalBytes: value.TotalBytes,
                CompletedBytes: value.ProgressedBytes));
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public InlineProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value) => _report(value);
    }
}
