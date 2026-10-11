using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Infrastructure.Logging;

namespace Launcher.Infrastructure.Persistence;

/// <summary>Local per-user storage authority; never accepts a root from server metadata or guardian requests.</summary>
public sealed class StorageRootResolver
{
    private const int SchemaVersion = 1;
    private const int MaximumConfigurationBytes = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
        WriteIndented = true,
    };
    private readonly string _applicationDirectory;
    private readonly string _defaultInstallDirectory;

    public StorageRootResolver()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppContext.BaseDirectory)
    {
    }

    // Explicit local directories allow isolated tests and a future installer to share the same contract.
    public StorageRootResolver(string localApplicationDataDirectory, string applicationDirectory)
    {
        string localDirectory = StoragePathPolicy.NormalizeDirectory(localApplicationDataDirectory);
        ControlDirectory = Path.Combine(localDirectory, "MinecraftLauncherControl");
        LegacyRootDirectory = Path.Combine(localDirectory, "MinecraftLauncher");
        _defaultInstallDirectory = Path.Combine(localDirectory, "lDiestrol.MinecraftLauncher");
        _applicationDirectory = StoragePathPolicy.NormalizeDirectory(applicationDirectory);
        string? parent = Path.GetDirectoryName(_applicationDirectory);
        if (Path.GetFileName(_applicationDirectory).Equals("current", StringComparison.OrdinalIgnoreCase) &&
            parent is not null && File.Exists(Path.Combine(parent, "Update.exe")))
        {
            _applicationDirectory = parent;
        }
    }

    public string ControlDirectory { get; }

    public string ConfigurationFile => Path.Combine(ControlDirectory, "storage-root.json");

    public string LegacyRootDirectory { get; }

    /// <summary>Hold the session for the entire Launcher/guardian lifetime, including Minecraft ownership.</summary>
    public StorageRootSession OpenSession()
    {
        FileStream? lease = null;
        try
        {
            lease = AcquireLease(exclusive: false);
            string? configuredRoot = ReadConfiguredRoot();
            string root = configuredRoot ?? LegacyRootDirectory;
            ValidateRoot(root);
            if (configuredRoot is null)
            {
                Directory.CreateDirectory(root);
            }

            EnsureAvailable(root);
            StorageRootSession session = new(new LauncherDataPaths(root, requireExistingRoot: true),
                configuredRoot is null, lease);
            lease = null;
            return session;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            throw MapFailure(exception);
        }
        finally
        {
            lease?.Dispose();
        }
    }

    /// <summary>Initial placement only. Moving an existing installation belongs to the future Storage Manager.</summary>
    public void ConfigureNewRoot(string gameDataRoot)
    {
        try
        {
            using FileStream lease = AcquireLease(exclusive: true);
            string root = StoragePathPolicy.NormalizeDirectory(gameDataRoot);
            ValidateRoot(root);
            string? configuredRoot = ReadConfiguredRoot();
            EnsureNoActiveGames(configuredRoot ?? LegacyRootDirectory);
            if (configuredRoot is not null)
            {
                if (!root.Equals(configuredRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw ExistingData();
                }

                EnsureAvailable(root);
                return;
            }

            StoragePathPolicy.EnsureNoReparsePoints(LegacyRootDirectory);
            if (!root.Equals(LegacyRootDirectory, StringComparison.OrdinalIgnoreCase) &&
                (File.Exists(LegacyRootDirectory) || (Directory.Exists(LegacyRootDirectory) &&
                 Directory.EnumerateFileSystemEntries(LegacyRootDirectory).Any())))
            {
                throw ExistingData();
            }

            // Do not adopt arbitrary directories or replace data belonging to another installation.
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any() &&
                !root.Equals(LegacyRootDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw ExistingData();
            }

            Directory.CreateDirectory(root);
            EnsureAvailable(root);
            WriteConfiguration(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            throw MapFailure(exception);
        }
    }

    private FileStream AcquireLease(bool exclusive)
    {
        StoragePathPolicy.EnsureNoReparsePoints(ControlDirectory);
        Directory.CreateDirectory(ControlDirectory);
        string leaseFile = Path.Combine(ControlDirectory, "storage-root.lock");
        StoragePathPolicy.EnsureNoReparsePoints(leaseFile);
        return new FileStream(leaseFile, FileMode.OpenOrCreate, FileAccess.ReadWrite,
            exclusive ? FileShare.None : FileShare.ReadWrite, bufferSize: 1, FileOptions.None);
    }

    private static void EnsureNoActiveGames(string root)
    {
        string locks = Path.Combine(root, ".locks");
        StoragePathPolicy.EnsureNoReparsePoints(locks);
        if (!Directory.Exists(locks))
        {
            return;
        }

        LauncherDataPaths paths = new(root, requireExistingRoot: true);
        ProfileGameActivity activity = new(paths, new NullAppLogger());
        foreach (string file in Directory.EnumerateFiles(locks, "*.game.json"))
        {
            string profileId = Path.GetFileName(file)[..^".game.json".Length];
            if (activity.IsActive(profileId))
            {
                throw new StorageRootException(StorageRootError.Busy,
                    "Minecraft использует игровые данные. Завершите игру перед выбором каталога.");
            }
        }
    }

    private string? ReadConfiguredRoot()
    {
        StoragePathPolicy.EnsureNoReparsePoints(ConfigurationFile);
        FileStream stream;
        try
        {
            stream = new FileStream(ConfigurationFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        using (stream)
        {
            if (stream.Length is 0 or > MaximumConfigurationBytes)
            {
                throw InvalidConfiguration();
            }

            StorageRootConfiguration? configuration = JsonSerializer.Deserialize<StorageRootConfiguration>(stream, JsonOptions);
            if (configuration is null || configuration.SchemaVersion != SchemaVersion ||
                string.IsNullOrWhiteSpace(configuration.GameDataRoot))
            {
                throw InvalidConfiguration();
            }

            return StoragePathPolicy.NormalizeDirectory(configuration.GameDataRoot);
        }
    }

    private void ValidateRoot(string root)
    {
        if (root.Equals(Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase) ||
            StoragePathPolicy.IsWithinOrEqual(root, ControlDirectory) ||
            StoragePathPolicy.IsWithinOrEqual(ControlDirectory, root) ||
            StoragePathPolicy.IsWithinOrEqual(root, _applicationDirectory) ||
            StoragePathPolicy.IsWithinOrEqual(_applicationDirectory, root) ||
            StoragePathPolicy.IsWithinOrEqual(root, _defaultInstallDirectory) ||
            StoragePathPolicy.IsWithinOrEqual(_defaultInstallDirectory, root))
        {
            throw new StorageRootException(StorageRootError.UnsafePath,
                "Игровые данные должны храниться отдельно от приложения, служебных настроек и корня диска.");
        }

        StoragePathPolicy.EnsureNoReparsePoints(root);
        string volume = Path.GetPathRoot(root)!;
        if (!Directory.Exists(volume))
        {
            throw Unavailable();
        }
    }

    private static void EnsureAvailable(string root)
    {
        StoragePathPolicy.EnsureNoReparsePoints(root);
        if (!Directory.Exists(root))
        {
            throw Unavailable();
        }

        // Check actual create/write access, not just Directory.Exists (which hides access failures).
        string probe = Path.Combine(root, $".storage-probe-{Guid.NewGuid():N}");
        using FileStream stream = new(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 1, FileOptions.DeleteOnClose);
        stream.WriteByte(0);
        stream.Flush(flushToDisk: true);
    }

    private void WriteConfiguration(string root)
    {
        string temporaryFile = Path.Combine(ControlDirectory, $"storage-root-{Guid.NewGuid():N}.tmp");
        try
        {
            StoragePathPolicy.EnsureNoReparsePoints(ConfigurationFile);
            using (FileStream stream = new(temporaryFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new StorageRootConfiguration(SchemaVersion, root), JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            StoragePathPolicy.EnsureNoReparsePoints(ConfigurationFile);
            File.Move(temporaryFile, ConfigurationFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    private static StorageRootException MapFailure(Exception exception) => exception switch
    {
        IOException io when ProfileLockFiles.IsContention(io) => new(StorageRootError.Busy,
            "Launcher или Minecraft использует игровые данные. Закройте их перед выбором каталога.", exception),
        UnauthorizedAccessException => new(StorageRootError.AccessDenied,
            "Нет доступа к каталогу игровых данных или служебной конфигурации Launcher. Проверьте права доступа.", exception),
        JsonException or ArgumentException => InvalidConfiguration(exception),
        _ => Unavailable(exception),
    };

    private static StorageRootException InvalidConfiguration(Exception? exception = null) => new(
        StorageRootError.InvalidConfiguration,
        "Конфигурация каталога игровых данных повреждена или имеет неподдерживаемую версию. Восстановите указатель; другой каталог не выбран.", exception);

    private static StorageRootException Unavailable(Exception? exception = null) => new(
        StorageRootError.RootUnavailable,
        "Каталог игровых данных недоступен. Подключите выбранный диск и проверьте каталог и права; другой каталог не выбран.", exception);

    private static StorageRootException ExistingData() => new(StorageRootError.ExistingData,
        "Установка уже содержит данные. Выбор нового каталога не переносит их; перенос требует отдельного Storage Manager.");

    private sealed record StorageRootConfiguration(int SchemaVersion, string GameDataRoot);
}

public sealed class StorageRootSession : IDisposable
{
    private FileStream? _lease;

    internal StorageRootSession(LauncherDataPaths paths, bool isLegacy, FileStream lease)
    {
        Paths = paths;
        IsLegacy = isLegacy;
        _lease = lease;
    }

    public LauncherDataPaths Paths { get; }

    public bool IsLegacy { get; }

    public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();
}
