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
        if (!File.Exists(_paths.SettingsFile))
        {
            return new LauncherSettings();
        }

        try
        {
            string json = await File.ReadAllTextAsync(_paths.SettingsFile, cancellationToken);
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
        Directory.CreateDirectory(_paths.RootDirectory);
        string temporaryFile = _paths.SettingsFile + ".tmp";
        string json = JsonSerializer.Serialize(settings, _jsonOptions);

        await File.WriteAllTextAsync(temporaryFile, json, cancellationToken);
        File.Move(temporaryFile, _paths.SettingsFile, overwrite: true);
    }
}
