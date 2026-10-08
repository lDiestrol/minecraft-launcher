using System.Diagnostics;
using System.Text.Json;
using Launcher.Core.Services;
using Launcher.Core.Validation;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Infrastructure.Game;

internal sealed record GameGuardianRequest(
    int SchemaVersion,
    string OwnershipToken,
    string ProfileId,
    string RootDirectory,
    string FileName,
    string Arguments,
    IReadOnlyList<string> ArgumentList,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> Environment,
    bool RedirectStandardOutput,
    bool RedirectStandardError,
    bool CreateNoWindow);

internal sealed record GameGuardianStatus(
    int SchemaVersion,
    string OwnershipToken,
    string State,
    int? ProcessId = null,
    long? ProcessStartTimeUtcTicks = null,
    int? ExitCode = null,
    string? Error = null);

public static class GameProcessGuardian
{
    public const string CommandLineSwitch = "--game-guardian";
    private const int CurrentSchemaVersion = 1;

    public static bool TryGetRequestPath(string[] args, out string? requestPath)
    {
        if (args.Length == 2 && args[0].Equals(CommandLineSwitch, StringComparison.Ordinal))
        {
            requestPath = args[1];
            return true;
        }

        requestPath = null;
        return false;
    }

    public static async Task<int> RunAsync(string requestPath)
    {
        LauncherDataPaths paths = new();
        FileAppLogger logger = new(paths);
        return await RunAsync(paths, requestPath, logger);
    }

    internal static async Task<int> RunAsync(
        LauncherDataPaths paths,
        string requestPath,
        IAppLogger logger,
        Func<Task>? beforeProcessStart = null,
        Func<Task>? afterCancellationDecision = null,
        Func<Process, Task>? afterProcessStart = null)
    {
        string fullRequestPath = ValidateRequestPath(paths, requestPath);
        string statusPath = GetStatusPath(fullRequestPath);
        string cancellationPath = GetCancellationPath(fullRequestPath);
        GameGuardianRequest request;
        try
        {
            request = JsonSerializer.Deserialize<GameGuardianRequest>(
                await File.ReadAllTextAsync(fullRequestPath)) ??
                throw new InvalidDataException("Guardian request was empty.");
            ValidateRequest(paths, fullRequestPath, request);
        }
        catch (Exception exception)
        {
            logger.Error("Game guardian rejected its launch request.", exception);
            TryWriteStatus(statusPath, new GameGuardianStatus(
                CurrentSchemaVersion,
                GetTokenFromRequestPath(fullRequestPath),
                "failed",
                Error: exception.Message), logger);
            TryDelete(fullRequestPath);
            TryDelete(cancellationPath);
            return 2;
        }

        TryDelete(fullRequestPath);
        ProfileGameActivity activity = new(paths, logger);
        if (!activity.TryAcquireOwnership(request.ProfileId, out IDisposable? ownership) || ownership is null)
        {
            TryWriteStatus(statusPath, new GameGuardianStatus(
                CurrentSchemaVersion,
                request.OwnershipToken,
                "failed",
                Error: "Another game guardian already owns this profile."), logger);
            TryDelete(cancellationPath);
            return 3;
        }

        using (ownership)
        using (Process process = new())
        {
            bool started = false;
            try
            {
                process.StartInfo = CreateStartInfo(request);
                if (request.RedirectStandardOutput)
                {
                    process.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data is not null)
                        {
                            logger.Info($"[Minecraft] {e.Data}");
                        }
                    };
                }

                if (request.RedirectStandardError)
                {
                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data is not null)
                        {
                            logger.Error($"[Minecraft] {e.Data}");
                        }
                    };
                }

                if (beforeProcessStart is not null)
                {
                    await beforeProcessStart();
                }

                ProfileLockFiles.EnsureNotReparsePoint(cancellationPath);
                if (File.Exists(cancellationPath))
                {
                    TryWriteStatus(statusPath, new GameGuardianStatus(
                        CurrentSchemaVersion,
                        request.OwnershipToken,
                        "cancelled"), logger);
                    logger.Info($"Game guardian cancelled before Java start: profile={request.ProfileId}.");
                    TryDelete(cancellationPath);
                    return 5;
                }

                if (afterCancellationDecision is not null)
                {
                    await afterCancellationDecision();
                }

                if (!process.Start())
                {
                    throw new InvalidOperationException("Minecraft process did not start.");
                }

                started = true;
                if (afterProcessStart is not null)
                {
                    await afterProcessStart(process);
                }

                DateTime startTimeUtc = process.StartTime.ToUniversalTime();
                activity.WriteState(
                    request.ProfileId,
                    process.Id,
                    startTimeUtc,
                    request.OwnershipToken);
                TryDelete(cancellationPath);
                if (request.RedirectStandardOutput)
                {
                    process.BeginOutputReadLine();
                }

                if (request.RedirectStandardError)
                {
                    process.BeginErrorReadLine();
                }

                TryWriteStatus(statusPath, new GameGuardianStatus(
                    CurrentSchemaVersion,
                    request.OwnershipToken,
                    "started",
                    process.Id,
                    startTimeUtc.Ticks), logger);
                logger.Info(
                    $"Game guardian owns profile={request.ProfileId}, PID={process.Id}, " +
                    $"startTicks={startTimeUtc.Ticks}.");

                await process.WaitForExitAsync();
                int exitCode = process.ExitCode;
                activity.DeleteState(request.ProfileId, request.OwnershipToken);
                TryWriteStatus(statusPath, new GameGuardianStatus(
                    CurrentSchemaVersion,
                    request.OwnershipToken,
                    "exited",
                    process.Id,
                    startTimeUtc.Ticks,
                    exitCode), logger);
                logger.Info($"Guarded Minecraft process PID={process.Id} exited with code {exitCode}.");
                TryDelete(cancellationPath);
                return 0;
            }
            catch (Exception exception)
            {
                logger.Error("Game guardian failed.", exception);
                if (started && !HasExited(process))
                {
                    logger.Info("Guardian will keep the profile lock until Minecraft exits after a guardian error.");
                    try
                    {
                        await process.WaitForExitAsync();
                    }
                    catch (Exception waitException)
                    {
                        logger.Error("Guardian could not observe Minecraft exit.", waitException);
                    }
                }

                activity.DeleteState(request.ProfileId, request.OwnershipToken);
                TryWriteStatus(statusPath, new GameGuardianStatus(
                    CurrentSchemaVersion,
                    request.OwnershipToken,
                    "failed",
                    started ? process.Id : null,
                    ExitCode: started && HasExited(process) ? process.ExitCode : null,
                    Error: exception.Message), logger);
                TryDelete(cancellationPath);
                return 4;
            }
        }
    }

    internal static string GetGuardianDirectory(LauncherDataPaths paths)
    {
        string locksDirectory = ProfileLockFiles.GetLocksDirectory(paths);
        string guardianDirectory = ProfileLockFiles.ResolveChildPath(locksDirectory, "guardian");
        Directory.CreateDirectory(guardianDirectory);
        ProfileLockFiles.EnsureNotReparsePoint(guardianDirectory);
        return guardianDirectory;
    }

    internal static string GetStatusPath(string requestPath)
    {
        const string requestSuffix = ".request.json";
        if (!requestPath.EndsWith(requestSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Guardian request file has an invalid name.");
        }

        return requestPath[..^requestSuffix.Length] + ".status.json";
    }

    internal static string GetCancellationPath(string requestPath)
    {
        const string requestSuffix = ".request.json";
        if (!requestPath.EndsWith(requestSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Guardian request file has an invalid name.");
        }

        return requestPath[..^requestSuffix.Length] + ".cancel";
    }

    private static string ValidateRequestPath(LauncherDataPaths paths, string requestPath)
    {
        string guardianDirectory = GetGuardianDirectory(paths);
        string fullPath = Path.GetFullPath(requestPath);
        string expectedPrefix = guardianDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Guardian request is outside the managed lock directory.");
        }

        ProfileLockFiles.EnsureNotReparsePoint(fullPath);
        _ = GetTokenFromRequestPath(fullPath);
        return fullPath;
    }

    private static void ValidateRequest(
        LauncherDataPaths paths,
        string requestPath,
        GameGuardianRequest request)
    {
        if (request.SchemaVersion != CurrentSchemaVersion ||
            !string.Equals(
                request.OwnershipToken,
                GetTokenFromRequestPath(requestPath),
                StringComparison.Ordinal) ||
            !ProfileValueValidator.IsValidProfileId(request.ProfileId) ||
            !Path.GetFullPath(request.RootDirectory).Equals(paths.RootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Guardian request identity is invalid.");
        }

        string javaPath = Path.GetFullPath(request.FileName);
        if (!paths.IsManagedRuntimePath(javaPath) || !File.Exists(javaPath))
        {
            throw new InvalidDataException("Guardian executable is outside the managed Java runtime.");
        }

        string expectedInstance = paths.GetInstanceDirectory(request.ProfileId);
        if (!Path.GetFullPath(request.WorkingDirectory).Equals(expectedInstance, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Guardian working directory does not match the selected profile.");
        }
    }

    private static ProcessStartInfo CreateStartInfo(GameGuardianRequest request)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = request.RedirectStandardOutput,
            RedirectStandardError = request.RedirectStandardError,
            CreateNoWindow = request.CreateNoWindow,
        };

        if (request.ArgumentList.Count > 0)
        {
            foreach (string argument in request.ArgumentList)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }
        else
        {
            startInfo.Arguments = request.Arguments;
        }

        startInfo.Environment.Clear();
        foreach ((string key, string? value) in request.Environment)
        {
            if (value is not null)
            {
                startInfo.Environment[key] = value;
            }
        }

        return startInfo;
    }

    private static string GetTokenFromRequestPath(string requestPath)
    {
        const string requestSuffix = ".request.json";
        string fileName = Path.GetFileName(requestPath);
        if (!fileName.EndsWith(requestSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Guardian request file has an invalid suffix.");
        }

        string token = fileName[..^requestSuffix.Length];
        if (!Guid.TryParseExact(token, "N", out _))
        {
            throw new InvalidDataException("Guardian request file has an invalid token.");
        }

        return token;
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void TryWriteStatus(
        string statusPath,
        GameGuardianStatus status,
        IAppLogger logger)
    {
        string temporaryPath = statusPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(status));
            File.Move(temporaryPath, statusPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Error("Could not write game guardian status.", exception);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal sealed class GameProcessGuardianClient
{
    private readonly LauncherDataPaths _paths;
    private readonly IAppLogger _logger;
    private readonly Func<string, Process> _guardianStarter;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _pollInterval;

    public GameProcessGuardianClient(LauncherDataPaths paths, IAppLogger logger)
        : this(
            paths,
            logger,
            StartGuardianProcess,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(100))
    {
    }

    internal GameProcessGuardianClient(
        LauncherDataPaths paths,
        IAppLogger logger,
        Func<string, Process> guardianStarter,
        TimeSpan startupTimeout,
        TimeSpan pollInterval)
    {
        _paths = paths;
        _logger = logger;
        _guardianStarter = guardianStarter;
        _startupTimeout = startupTimeout;
        _pollInterval = pollInterval;
    }

    public async Task<GuardedGameSession> StartAsync(
        string profileId,
        ProcessStartInfo gameStartInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string token = Guid.NewGuid().ToString("N");
        string guardianDirectory = GameProcessGuardian.GetGuardianDirectory(_paths);
        string requestPath = ProfileLockFiles.ResolveChildPath(
            guardianDirectory,
            $"{token}.request.json");
        string statusPath = GameProcessGuardian.GetStatusPath(requestPath);
        string cancellationPath = GameProcessGuardian.GetCancellationPath(requestPath);
        GameGuardianRequest request = CreateRequest(token, profileId, gameStartInfo);

        Process? guardian = null;
        try
        {
            WriteRequest(requestPath, request);
            cancellationToken.ThrowIfCancellationRequested();
            guardian = _guardianStarter(requestPath);
            int guardianProcessId = guardian.Id;
            long started = Stopwatch.GetTimestamp();
            bool cancellationSignalled = false;
            while (Stopwatch.GetElapsedTime(started) < _startupTimeout)
            {
                if (cancellationToken.IsCancellationRequested && !cancellationSignalled)
                {
                    cancellationSignalled = TrySignalCancellation(cancellationPath);
                    if (cancellationSignalled)
                    {
                        _logger.Info(
                            $"Cancellation requested while awaiting guardian handoff: profile={profileId}.");
                    }
                }

                GameGuardianStatus? status = TryReadStatus(statusPath, token);
                if (status is not null &&
                    string.Equals(status.State, "started", StringComparison.Ordinal) &&
                    status.ProcessId is int processId &&
                    status.ProcessStartTimeUtcTicks is long processStartTimeUtcTicks)
                {
                    _logger.Info(
                        $"Game guardian confirmed Minecraft start: guardianPID={guardianProcessId}, " +
                        $"PID={processId}.");
                    return new GuardedGameSession(
                        guardianProcessId,
                        processId,
                        processStartTimeUtcTicks,
                        statusPath,
                        token,
                        _logger);
                }

                if (status is not null &&
                    string.Equals(status.State, "exited", StringComparison.Ordinal) &&
                    status.ProcessId is int exitedProcessId &&
                    status.ProcessStartTimeUtcTicks is long exitedProcessStartTimeUtcTicks)
                {
                    return new GuardedGameSession(
                        guardianProcessId,
                        exitedProcessId,
                        exitedProcessStartTimeUtcTicks,
                        statusPath,
                        token,
                        _logger);
                }

                if (status is not null &&
                    string.Equals(status.State, "failed", StringComparison.Ordinal))
                {
                    throw CreateGuardianFailure(status.Error);
                }

                if (status is not null &&
                    string.Equals(status.State, "cancelled", StringComparison.Ordinal))
                {
                    _logger.Info($"Game guardian confirmed cancellation before Java start: profile={profileId}.");
                    throw new OperationCanceledException(
                        "Game launch was cancelled before Java started.",
                        cancellationToken);
                }

                if (guardian.HasExited)
                {
                    throw CreateGuardianFailure(status?.Error ??
                        $"Guardian exited with code {guardian.ExitCode} before Minecraft started.");
                }

                // Once guardian has been created, cancellation is resolved by its status:
                // either Java was not started (cancelled) or the protected handoff completes.
                await Task.Delay(_pollInterval);
            }

            throw CreateGuardianFailure(cancellationSignalled
                ? "Guardian did not resolve the cancellation request before the startup timeout."
                : "Guardian startup confirmation timed out.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (GameLaunchException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new GameLaunchException(
                GameLaunchError.ProcessCreationFailed,
                "Не удалось запустить Minecraft под защитой Launcher.",
                "Could not start the game guardian.",
                exception);
        }
        finally
        {
            guardian?.Dispose();
            TryDelete(requestPath);
        }
    }

    private static Process StartGuardianProcess(string requestPath)
    {
        string? launcherPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
        {
            throw new GameLaunchException(
                GameLaunchError.ProcessCreationFailed,
                "Не удалось запустить защитный процесс Minecraft.",
                "The current Launcher executable path is unavailable.");
        }

        ProcessStartInfo guardianStartInfo = new()
        {
            FileName = launcherPath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        guardianStartInfo.ArgumentList.Add(GameProcessGuardian.CommandLineSwitch);
        guardianStartInfo.ArgumentList.Add(requestPath);
        return Process.Start(guardianStartInfo) ?? throw new InvalidOperationException(
            "The game guardian process did not start.");
    }

    private GameGuardianRequest CreateRequest(
        string token,
        string profileId,
        ProcessStartInfo startInfo)
    {
        if (startInfo.UseShellExecute)
        {
            throw new InvalidOperationException("Guardian requires UseShellExecute=false.");
        }

        return new GameGuardianRequest(
            1,
            token,
            profileId,
            _paths.RootDirectory,
            Path.GetFullPath(startInfo.FileName),
            startInfo.Arguments,
            startInfo.ArgumentList.ToArray(),
            Path.GetFullPath(startInfo.WorkingDirectory),
            startInfo.Environment.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            startInfo.RedirectStandardOutput,
            startInfo.RedirectStandardError,
            startInfo.CreateNoWindow);
    }

    private static void WriteRequest(string requestPath, GameGuardianRequest request)
    {
        string temporaryPath = requestPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(request));
            File.Move(temporaryPath, requestPath, true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private bool TrySignalCancellation(string cancellationPath)
    {
        try
        {
            ProfileLockFiles.EnsureNotReparsePoint(cancellationPath);
            using FileStream marker = new(
                cancellationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 1,
                FileOptions.WriteThrough);
            marker.WriteByte(1);
            return true;
        }
        catch (IOException) when (File.Exists(cancellationPath))
        {
            ProfileLockFiles.EnsureNotReparsePoint(cancellationPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error(
                "Could not signal guardian cancellation; the protected handoff will continue.",
                exception);
            return false;
        }
    }

    private static GameGuardianStatus? TryReadStatus(string statusPath, string token)
    {
        if (!File.Exists(statusPath))
        {
            return null;
        }

        try
        {
            GameGuardianStatus? status = JsonSerializer.Deserialize<GameGuardianStatus>(
                File.ReadAllText(statusPath));
            return status is { SchemaVersion: 1 } &&
                string.Equals(status.OwnershipToken, token, StringComparison.Ordinal)
                ? status
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static GameLaunchException CreateGuardianFailure(string? technicalMessage) => new(
        GameLaunchError.ProcessCreationFailed,
        "Не удалось запустить Minecraft под защитой Launcher.",
        technicalMessage ?? "The game guardian failed without an error message.");

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal sealed class GuardedGameSession
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private readonly int _guardianProcessId;
    private readonly string _statusPath;
    private readonly string _ownershipToken;
    private readonly IAppLogger _logger;

    public GuardedGameSession(
        int guardianProcessId,
        int processId,
        long processStartTimeUtcTicks,
        string statusPath,
        string ownershipToken,
        IAppLogger logger)
    {
        _guardianProcessId = guardianProcessId;
        ProcessId = processId;
        ProcessStartTimeUtcTicks = processStartTimeUtcTicks;
        _statusPath = statusPath;
        _ownershipToken = ownershipToken;
        _logger = logger;
    }

    public int ProcessId { get; }

    public long ProcessStartTimeUtcTicks { get; }

    public async Task<int> WaitForExitAsync()
    {
        while (true)
        {
            GameGuardianStatus? status = ReadStatus();
            if (status is not null &&
                string.Equals(status.State, "exited", StringComparison.Ordinal) &&
                status.ExitCode is int exitCode)
            {
                TryDelete(_statusPath);
                return exitCode;
            }

            if (status is not null &&
                string.Equals(status.State, "failed", StringComparison.Ordinal))
            {
                throw new GameLaunchException(
                    GameLaunchError.ProcessCreationFailed,
                    "Защитный процесс Minecraft завершился с ошибкой.",
                    status.Error ?? "The game guardian failed without an error message.");
            }

            if (!IsProcessRunning(_guardianProcessId))
            {
                throw new GameLaunchException(
                    GameLaunchError.ProcessCreationFailed,
                    "Защитный процесс Minecraft неожиданно завершился.",
                    $"Guardian PID {_guardianProcessId} exited before reporting game completion.");
            }

            await Task.Delay(PollInterval);
        }
    }

    private GameGuardianStatus? ReadStatus()
    {
        if (!File.Exists(_statusPath))
        {
            return null;
        }

        try
        {
            GameGuardianStatus? status = JsonSerializer.Deserialize<GameGuardianStatus>(
                File.ReadAllText(_statusPath));
            return status is { SchemaVersion: 1 } &&
                string.Equals(status.OwnershipToken, _ownershipToken, StringComparison.Ordinal)
                ? status
                : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            _logger.Error("Could not read game guardian status; retrying.", exception);
            return null;
        }
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
