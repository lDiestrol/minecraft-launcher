using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Services;
using Launcher.Infrastructure.Game;

namespace Launcher.Infrastructure.Persistence;

internal sealed record ActiveGameState(
    int SchemaVersion,
    string ProfileId,
    int ProcessId,
    long ProcessStartTimeUtcTicks,
    string OwnershipToken,
    string? JobName = null,
    int GuardianProcessId = 0,
    long GuardianStartTimeUtcTicks = 0,
    int WitnessContractVersion = 0);

internal sealed class ProfileGameActivity
{
    private const int LegacySchemaVersion = 1;
    private const int IntentSchemaVersion = 3;
    private static readonly JsonSerializerOptions StateJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
    };
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
            return IsStateActive(profileId);
        }
    }

    // Caller must own the profile's .game.lock; this serializes recovery with process creation.
    private bool IsStateActive(string profileId)
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
            using FileStream stream = SafeLeafFile.OpenRead(statePath);
            if (stream.Length > 16 * 1024)
            {
                throw new JsonException("Active game state exceeds its size budget.");
            }

            state = JsonSerializer.Deserialize<ActiveGameState>(stream, StateJson);
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

        if (state!.SchemaVersion is 2 or IntentSchemaVersion)
        {
            try
            {
                if (state.JobName != WindowsGameJob.GetName(state.OwnershipToken) ||
                    state.GuardianProcessId <= 0 || state.GuardianStartTimeUtcTicks <= 0)
                {
                    throw new InvalidDataException("Invalid job identity.");
                }

                ActiveGameState guardian = state with
                {
                    ProcessId = state.GuardianProcessId,
                    ProcessStartTimeUtcTicks = state.GuardianStartTimeUtcTicks,
                };
                ProcessIdentityStatus owner = GetProcessIdentityStatus(guardian);
                if (owner == ProcessIdentityStatus.Uncertain)
                {
                    throw new InvalidDataException("Guardian identity is uncertain.");
                }

                if (owner == ProcessIdentityStatus.Matching)
                {
                    return true;
                }

                uint? active = WindowsGameJob.GetActiveProcesses(state.JobName);
                if (active > 0)
                {
                    return true;
                }

                if (active is null && state.SchemaVersion == 2 && state.ProcessId == 0)
                {
                    throw new InvalidDataException("Legacy pre-PID intent has no witness contract.");
                }

                // Under v3/witness-v1, a missing name proves the primary JVM is no longer
                // alive, not that it never started or that every handleless descendant exited.
                // v2 can recover missing jobs ONLY by its separate recorded-PID identity rule.
                // Exclusive .game.lock excludes any concurrent creator or recovery operation.
                if (state.ProcessId > 0 && GetProcessIdentityStatus(state) is
                    ProcessIdentityStatus.Matching or ProcessIdentityStatus.Uncertain)
                {
                    throw new InvalidDataException("Live process is not accounted for by its game job.");
                }

                DeleteStateFile(statePath, profileId);
                return false;
            }
            catch (Exception exception) when (exception is global::System.ComponentModel.Win32Exception or InvalidDataException)
            {
                throw CreateUnsafeStateException(profileId, statePath, "Could not verify atomic launch intent.", exception);
            }
        }

        ProcessIdentityStatus identityStatus = GetProcessIdentityStatus(state);
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

    public bool TryAcquireOwnership(string profileId, out IDisposable? lease)
    {
        string ownershipPath = GetOwnershipPath(profileId);
        try
        {
            FileStream stream = OpenOwnershipFile(ownershipPath);
            try
            {
                if (IsStateActive(profileId))
                {
                    stream.Dispose();
                    lease = null;
                    return false;
                }

                lease = new FileLease(stream);
                return true;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
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
            LegacySchemaVersion,
            profileId,
            processId,
            processStartTimeUtc.Ticks,
            ownershipToken);
        WriteStateFile(state);
    }

    public void WriteIntent(string profileId, string token, string jobName, int processId = 0, long startTicks = 0)
    {
        using Process guardian = Process.GetCurrentProcess();
        WriteStateFile(new ActiveGameState(IntentSchemaVersion, profileId, processId, startTicks, token, jobName,
            guardian.Id, guardian.StartTime.ToUniversalTime().Ticks, WindowsGameJob.WitnessContractVersion));
    }

    private void WriteStateFile(ActiveGameState state)
    {
        string statePath = GetStatePath(state.ProfileId);
        string temporaryPath = statePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, state);
                stream.Flush(flushToDisk: true);
            }

            ProfileLockFiles.EnsureNotReparsePoint(statePath);
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
        state is not null &&
        ((state.SchemaVersion == LegacySchemaVersion && state.WitnessContractVersion == 0 &&
          state.ProcessId > 0 && state.ProcessStartTimeUtcTicks > 0) ||
         ((state.SchemaVersion == 2 && state.WitnessContractVersion == 0 ||
           state.SchemaVersion == IntentSchemaVersion && state.WitnessContractVersion == WindowsGameJob.WitnessContractVersion) &&
          state.ProcessId >= 0 && state.ProcessStartTimeUtcTicks >= 0 &&
          ((state.ProcessId == 0) == (state.ProcessStartTimeUtcTicks == 0)))) &&
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
            return ProcessIdentityStatus.Uncertain;
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
