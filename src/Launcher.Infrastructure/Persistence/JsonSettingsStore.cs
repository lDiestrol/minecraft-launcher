using System.Text.Json;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Infrastructure.Persistence;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly LauncherDataPaths _paths;
    private readonly IAppLogger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public JsonSettingsStore(LauncherDataPaths paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureRootAvailable();
        StoragePathPolicy.EnsureNoReparsePoints(_paths.SettingsFile);
        if (!File.Exists(_paths.SettingsFile))
        {
            return new LauncherSettings();
        }

        try
        {
            using StreamReader reader = new(SafeLeafFile.OpenRead(_paths.SettingsFile));
            string json = await reader.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.Error("Settings file is empty; safe defaults will be used.");
                return new LauncherSettings();
            }

            return JsonSerializer.Deserialize<LauncherSettings>(json, _jsonOptions) ?? new LauncherSettings();
        }
        catch (JsonException exception)
        {
            _logger.Error("Settings JSON is corrupted; safe defaults will be used.", exception);
            return new LauncherSettings();
        }
        catch (IOException exception)
        {
            _logger.Error("Settings could not be read; safe defaults will be used.", exception);
            return new LauncherSettings();
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.Error("Settings could not be accessed; safe defaults will be used.", exception);
            return new LauncherSettings();
        }
    }

    public async Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        _paths.EnsureRootAvailable();
        StoragePathPolicy.EnsureNoReparsePoints(_paths.SettingsFile);
        string temporaryFile = _paths.SettingsFile + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = new(temporaryFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            StoragePathPolicy.EnsureNoReparsePoints(_paths.SettingsFile);
            File.Move(temporaryFile, _paths.SettingsFile, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryFile);
        }
    }
}
