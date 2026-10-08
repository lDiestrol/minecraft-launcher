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
                _logger.Error($"Ignoring malformed active game state for profile={profileId}.", exception);
                DeleteStateFile(statePath, profileId);
                return false;
            }
            catch (IOException exception)
            {
                throw new PackSyncException(
                    PackSyncError.Unknown,
                    "Не удалось проверить состояние запущенного Minecraft.",
                    $"Could not read active game state '{statePath}'.",
                    exception);
            }

            if (!IsValid(state, profileId) || !MatchesLiveProcess(state!))
            {
                _logger.Info($"Removing stale active game state: profile={profileId}.");
                DeleteStateFile(statePath, profileId);
                return false;
            }

            _logger.Info(
                $"Recovered active game ownership from state: profile={profileId}, " +
                $"PID={state!.ProcessId}, startTicks={state.ProcessStartTimeUtcTicks}.");
            return true;
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

    private static bool MatchesLiveProcess(ActiveGameState state)
    {
        try
        {
            using Process process = Process.GetProcessById(state.ProcessId);
            return !process.HasExited &&
                process.StartTime.ToUniversalTime().Ticks == state.ProcessStartTimeUtcTicks;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (global::System.ComponentModel.Win32Exception)
        {
            // If the PID still exists but Windows denies access to its start time,
            // fail closed rather than risk modifying a live game profile.
            return true;
        }
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
}
