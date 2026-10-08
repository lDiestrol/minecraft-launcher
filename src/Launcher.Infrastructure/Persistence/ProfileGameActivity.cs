using System.Diagnostics;
using System.Text.Json;
using Launcher.Core.Services;

namespace Launcher.Infrastructure.Persistence;

internal sealed record ActiveGameState(
    int SchemaVersion,
    string ProfileId,
    int ProcessId,
    long ProcessStartTimeUtcTicks,
    string OwnershipToken);

internal sealed class ProfileGameActivity
{
    private const int CurrentSchemaVersion = 1;
    private readonly LauncherDataPaths _paths;
    private readonly IAppLogger _logger;

    public ProfileGameActivity(LauncherDataPaths paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public bool IsActive(string profileId)
    {
        string ownershipPath = GetOwnershipPath(profileId);
        FileStream? probe = null;
        try
        {
            probe = OpenOwnershipFile(ownershipPath);
        }
        catch (IOException exception) when (ProfileLockFiles.IsContention(exception))
        {
            _logger.Info($"Active game ownership lock is held: profile={profileId}.");
            return true;
        }

        using (probe)
        {
            string statePath = GetStatePath(profileId);
            if (!File.Exists(statePath))
            {
                return false;
            }

            ProfileLockFiles.EnsureNotReparsePoint(statePath);
            ActiveGameState? state;
            try
            {
                state = JsonSerializer.Deserialize<ActiveGameState>(File.ReadAllText(statePath));
            }
            catch (JsonException exception)
            {
                throw CreateUnsafeStateException(
                    profileId,
                    statePath,
                    "Active game state contains malformed JSON.",
                    exception);
            }
            catch (IOException exception)
            {
                throw CreateUnsafeStateException(
                    profileId,
                    statePath,
                    "Could not read active game state.",
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw CreateUnsafeStateException(
                    profileId,
                    statePath,
                    "Access to active game state was denied.",
                    exception);
            }

            if (!IsValid(state, profileId))
            {
                throw CreateUnsafeStateException(
                    profileId,
                    statePath,
                    "Active game state is incomplete or has an invalid identity.");
            }

            ProcessIdentityStatus identityStatus = GetProcessIdentityStatus(state!);
            switch (identityStatus)
            {
                case ProcessIdentityStatus.Matching:
                    _logger.Info(
                        $"Recovered active game ownership from state: profile={profileId}, " +
                        $"PID={state!.ProcessId}, startTicks={state.ProcessStartTimeUtcTicks}.");
                    return true;
                case ProcessIdentityStatus.Exited:
                    _logger.Info($"Removing active game state for an exited process: profile={profileId}.");
                    DeleteStateFile(statePath, profileId);
                    return false;
                case ProcessIdentityStatus.Reused:
                    _logger.Info(
                        $"Removing active game state after PID reuse: profile={profileId}, PID={state!.ProcessId}.");
                    DeleteStateFile(statePath, profileId);
                    return false;
                case ProcessIdentityStatus.Uncertain:
                default:
                    throw CreateUnsafeStateException(
                        profileId,
                        statePath,
                        $"Could not verify process identity for PID {state!.ProcessId}.");
            }
        }
    }

    public bool TryAcquireOwnership(string profileId, out IDisposable? lease)
    {
        string ownershipPath = GetOwnershipPath(profileId);
        try
        {
            FileStream stream = OpenOwnershipFile(ownershipPath);
            lease = new FileLease(stream);
            return true;
        }
        catch (IOException exception) when (ProfileLockFiles.IsContention(exception))
        {
            lease = null;
            return false;
        }
    }

    public void WriteState(
        string profileId,
        int processId,
        DateTime processStartTimeUtc,
        string ownershipToken)
    {
        ActiveGameState state = new(
            CurrentSchemaVersion,
            profileId,
            processId,
            processStartTimeUtc.Ticks,
            ownershipToken);
        string statePath = GetStatePath(profileId);
        string temporaryPath = statePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state));
            File.Move(temporaryPath, statePath, true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    public void DeleteState(string profileId, string ownershipToken)
    {
        string statePath = GetStatePath(profileId);
        if (!File.Exists(statePath))
        {
            return;
        }

        try
        {
            ActiveGameState? state = JsonSerializer.Deserialize<ActiveGameState>(File.ReadAllText(statePath));
            if (string.Equals(state?.OwnershipToken, ownershipToken, StringComparison.Ordinal))
            {
                File.Delete(statePath);
            }
        }
        catch (JsonException exception)
        {
            _logger.Error($"Could not parse active game state during cleanup: profile={profileId}.", exception);
        }
        catch (IOException exception)
        {
            _logger.Error($"Could not delete active game state: profile={profileId}.", exception);
        }
    }

    internal string GetOwnershipPath(string profileId) =>
        ProfileLockFiles.GetProfilePath(_paths, profileId, ".game.lock");

    internal string GetStatePath(string profileId) =>
        ProfileLockFiles.GetProfilePath(_paths, profileId, ".game.json");

    private static FileStream OpenOwnershipFile(string path) => new(
        path,
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.None,
        bufferSize: 1,
        FileOptions.WriteThrough);

    private static bool IsValid(ActiveGameState? state, string profileId) =>
        state is
        {
            SchemaVersion: CurrentSchemaVersion,
            ProcessId: > 0,
            ProcessStartTimeUtcTicks: > 0,
        } &&
        string.Equals(state.ProfileId, profileId, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(state.OwnershipToken);

    private static ProcessIdentityStatus GetProcessIdentityStatus(ActiveGameState state)
    {
        try
        {
            using Process process = Process.GetProcessById(state.ProcessId);
            if (process.HasExited)
            {
                return ProcessIdentityStatus.Exited;
            }

            return process.StartTime.ToUniversalTime().Ticks == state.ProcessStartTimeUtcTicks
                ? ProcessIdentityStatus.Matching
                : ProcessIdentityStatus.Reused;
        }
        catch (ArgumentException)
        {
            return ProcessIdentityStatus.Exited;
        }
        catch (InvalidOperationException)
        {
            return ProcessIdentityStatus.Exited;
        }
        catch (global::System.ComponentModel.Win32Exception)
        {
            return ProcessIdentityStatus.Uncertain;
        }
    }

    private PackSyncException CreateUnsafeStateException(
        string profileId,
        string statePath,
        string technicalReason,
        Exception? innerException = null)
    {
        string userMessage =
            "Launcher не может безопасно определить, завершён ли Minecraft: файл защиты повреждён " +
            "или недоступен. Закройте все окна Minecraft и процессы Java, затем удалите файл защиты " +
            $"и повторите операцию. Файл: {statePath}";
        PackSyncException exception = new(
            PackSyncError.ManagedStateCorrupt,
            userMessage,
            $"Unsafe active game state for profile '{profileId}' at '{statePath}': {technicalReason}",
            innerException);
        _logger.Error(exception.Message, innerException);
        return exception;
    }

    private void DeleteStateFile(string statePath, string profileId)
    {
        try
        {
            File.Delete(statePath);
        }
        catch (IOException exception)
        {
            throw new PackSyncException(
                PackSyncError.Unknown,
                "Не удалось очистить устаревшее состояние Minecraft.",
                $"Could not delete stale active game state for '{profileId}'.",
                exception);
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

    private sealed class FileLease(FileStream stream) : IDisposable
    {
        private FileStream? _stream = stream;

        public void Dispose() => Interlocked.Exchange(ref _stream, null)?.Dispose();
    }

    private enum ProcessIdentityStatus
    {
        Matching,
        Exited,
        Reused,
        Uncertain,
    }
}
