using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Infrastructure.Installation;

internal sealed record InstallationIntent(int SchemaVersion, int JobContractVersion, int OwnerId,
    long OwnerStartTicks, string Token, string AppDirectory, string GameDataDirectory, string PackageSha256);

internal static class InstallationIntentStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
    };

    internal static string PathFor(string control) => Path.Combine(control, "installation-intent.json");

    public static void EnsureNoPending(string control)
    {
        if (Read(control) is not null)
        {
            throw new StorageRootException(StorageRootError.Busy,
                "Установка Launcher ещё не завершена. Повторите установку для безопасной проверки и восстановления; игровые данные не изменены.");
        }
    }

    internal static InstallationIntent? Read(string control)
    {
        string file = PathFor(control);
        StoragePathPolicy.EnsureNoReparsePoints(file);
        try
        {
            using FileStream stream = SafeLeafFile.OpenRead(file);
            if (stream.Length is 0 or > 16384) throw new InvalidDataException("Invalid installation intent size.");
            InstallationIntent? state = JsonSerializer.Deserialize<InstallationIntent>(stream, Options);
            if (state is null || state.SchemaVersion != 1 || state.JobContractVersion != 1 ||
                state.OwnerId <= 0 || state.OwnerStartTicks <= 0 || !Guid.TryParseExact(state.Token, "N", out _) ||
                string.IsNullOrEmpty(state.AppDirectory) || string.IsNullOrEmpty(state.GameDataDirectory) ||
                state.PackageSha256 is null || state.PackageSha256.Length != 64 || !state.PackageSha256.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException("Unsupported installation intent.");
            }
            return state;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    internal static void Write(string control, InstallationIntent state)
    {
        string file = PathFor(control);
        StoragePathPolicy.EnsureNoReparsePoints(file);
        string temporary = Path.Combine(control, $"install-{Guid.NewGuid():N}.tmp");
        using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, state, Options);
            stream.Flush(true);
        }
        StoragePathPolicy.EnsureNoReparsePoints(file);
        File.Move(temporary, file, true);
    }

    internal static void Delete(string control)
    {
        StoragePathPolicy.EnsureNoReparsePoints(PathFor(control));
        File.Delete(PathFor(control));
    }

    internal static bool OwnerFinished(InstallationIntent state)
    {
        try
        {
            using Process owner = Process.GetProcessById(state.OwnerId);
            return owner.HasExited || owner.StartTime.ToUniversalTime().Ticks != state.OwnerStartTicks;
        }
        catch (ArgumentException) { return true; }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            throw new InstallationException(InstallationError.Recovery,
                "Не удалось достоверно проверить владельца установки. Восстановление запрещено.", exception);
        }
    }
}
