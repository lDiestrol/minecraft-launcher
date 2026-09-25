using System.Net;
using System.Text;
using System.Text.Json;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Infrastructure.Http;

public sealed class LauncherServerClient : ILauncherServerClient
{
    private const int SupportedSchemaVersion = 1;
    private const int MaximumConfigurationBytes = 1024 * 1024;
    private readonly HttpClient _httpClient;
    private readonly IAppLogger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public LauncherServerClient(HttpClient httpClient, IAppLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BootstrapConfiguration> GetBootstrapAsync(
        Uri bootstrapUri,
        CancellationToken cancellationToken)
    {
        string json = await DownloadStringAsync(bootstrapUri, "bootstrap.json", cancellationToken);

        try
        {
            BootstrapDto? document = JsonSerializer.Deserialize<BootstrapDto>(json, _jsonOptions);
            if (document is null)
            {
                throw InvalidConfiguration("bootstrap.json пуст или не содержит объект.");
            }

            EnsureSupportedSchema(document.SchemaVersion, "bootstrap.json");

            if (string.IsNullOrWhiteSpace(document.ServerName) ||
                string.IsNullOrWhiteSpace(document.ProfilesUrl))
            {
                throw InvalidConfiguration(
                    "В bootstrap.json отсутствуют обязательные поля serverName или profilesUrl.");
            }

            if (!Uri.TryCreate(bootstrapUri, document.ProfilesUrl, out Uri? profilesUri) ||
                !profilesUri.IsAbsoluteUri ||
                !IsAllowedEndpoint(profilesUri))
            {
                throw InvalidConfiguration("Поле profilesUrl содержит некорректный URL.");
            }

            return new BootstrapConfiguration(
                document.SchemaVersion,
                document.ServerName.Trim(),
                bootstrapUri,
                profilesUri,
                NullIfWhiteSpace(document.DefaultProfileId));
        }
        catch (JsonException exception)
        {
            _logger.Error("Failed to parse bootstrap JSON.", exception);
            throw new ServerConnectionException(
                "Сервер вернул некорректный bootstrap.json.",
                "Invalid bootstrap JSON.",
                exception);
        }
    }

    public async Task<IReadOnlyList<GameProfile>> GetProfilesAsync(
        BootstrapConfiguration bootstrap,
        CancellationToken cancellationToken)
    {
        string json = await DownloadStringAsync(bootstrap.ProfilesUri, "profiles.json", cancellationToken);

        try
        {
            ProfilesDto? document = JsonSerializer.Deserialize<ProfilesDto>(json, _jsonOptions);
            if (document is null)
            {
                throw InvalidConfiguration("profiles.json пуст или не содержит объект.");
            }

            EnsureSupportedSchema(document.SchemaVersion, "profiles.json");
            if (document.Profiles is null || document.Profiles.Count == 0)
            {
                throw InvalidConfiguration("Сервер не опубликовал ни одной игровой сборки.");
            }

            List<GameProfile> profiles = new(document.Profiles.Count);
            HashSet<string> profileIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (ProfileDto profile in document.Profiles)
            {
                GameProfile mapped = MapProfile(profile, bootstrap.ProfilesUri);
                if (!profileIds.Add(mapped.Id))
                {
                    throw InvalidConfiguration($"В profiles.json повторяется id '{mapped.Id}'.");
                }

                profiles.Add(mapped);
            }

            return profiles;
        }
        catch (JsonException exception)
        {
            _logger.Error("Failed to parse profiles JSON.", exception);
            throw new ServerConnectionException(
                "Сервер вернул некорректный profiles.json.",
                "Invalid profiles JSON.",
                exception);
        }
    }

    private async Task<string> DownloadStringAsync(
        Uri uri,
        string documentName,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"HTTP {(int)response.StatusCode} ({response.StatusCode}) for {uri}.");
                throw new ServerConnectionException(
                    GetStatusMessage(response.StatusCode, documentName),
                    $"HTTP {(int)response.StatusCode} while requesting {uri}.");
            }

            string content = await ReadLimitedContentAsync(
                response.Content,
                uri,
                documentName,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ServerConnectionException(
                    $"Сервер вернул пустой {documentName}.",
                    $"Empty response for {uri}.");
            }

            return content;
        }
        catch (ServerConnectionException)
        {
            throw;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.Error($"HTTP timeout for {uri}.", exception);
            throw new ServerConnectionException(
                "Сервер не ответил вовремя. Проверьте адрес и повторите попытку.",
                $"HTTP timeout for {uri}.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.Error($"HTTP request failed for {uri}.", exception);
            throw new ServerConnectionException(
                "Не удалось подключиться к серверу. Проверьте адрес, сеть и сертификат.",
                $"HTTP request failed for {uri}.",
                exception);
        }
    }

    private async Task<string> ReadLimitedContentAsync(
        HttpContent content,
        Uri uri,
        string documentName,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumConfigurationBytes)
        {
            throw ConfigurationTooLarge(uri, documentName, content.Headers.ContentLength.Value);
        }

        await using Stream source = await content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream destination = new();
        byte[] buffer = new byte[16 * 1024];
        int totalBytes = 0;

        while (true)
        {
            int bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
            if (totalBytes > MaximumConfigurationBytes)
            {
                throw ConfigurationTooLarge(uri, documentName, totalBytes);
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return Encoding.UTF8.GetString(destination.GetBuffer(), 0, totalBytes);
    }

    private ServerConnectionException ConfigurationTooLarge(Uri uri, string documentName, long receivedBytes)
    {
        _logger.Error(
            $"Response for {uri} exceeds the {MaximumConfigurationBytes}-byte limit " +
            $"(reported or received: {receivedBytes} bytes).");
        return new ServerConnectionException(
            $"Файл {documentName} слишком большой.",
            $"Response for {uri} exceeds the {MaximumConfigurationBytes}-byte limit.");
    }

    private static GameProfile MapProfile(ProfileDto profile, Uri profilesUri)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) ||
            string.IsNullOrWhiteSpace(profile.Name) ||
            string.IsNullOrWhiteSpace(profile.MinecraftVersion) ||
            profile.Loader is null ||
            string.IsNullOrWhiteSpace(profile.Loader.Type) ||
            string.IsNullOrWhiteSpace(profile.Loader.Version) ||
            string.IsNullOrWhiteSpace(profile.PackVersion) ||
            string.IsNullOrWhiteSpace(profile.ManifestUrl) ||
            string.IsNullOrWhiteSpace(profile.ServerAddress) ||
            profile.ServerPort is < 1 or > 65535)
        {
            throw InvalidConfiguration("profiles.json содержит неполное или некорректное описание сборки.");
        }

        if (!Uri.TryCreate(profilesUri, profile.ManifestUrl, out Uri? manifestUri) ||
            !manifestUri.IsAbsoluteUri ||
            !IsAllowedEndpoint(manifestUri))
        {
            throw InvalidConfiguration($"Сборка '{profile.Id}' содержит некорректный manifestUrl.");
        }

        return new GameProfile(
            profile.Id.Trim(),
            profile.Name.Trim(),
            profile.MinecraftVersion.Trim(),
            profile.Loader.Type.Trim(),
            profile.Loader.Version.Trim(),
            profile.PackVersion.Trim(),
            manifestUri,
            profile.ServerAddress.Trim(),
            (ushort)profile.ServerPort);
    }

    private static void EnsureSupportedSchema(int schemaVersion, string documentName)
    {
        if (schemaVersion <= 0)
        {
            throw InvalidConfiguration($"В {documentName} отсутствует корректный schemaVersion.");
        }

        if (schemaVersion != SupportedSchemaVersion)
        {
            throw new ServerConnectionException(
                schemaVersion > SupportedSchemaVersion
                    ? "Эта конфигурация сервера требует более новой версии Launcher."
                    : "Версия конфигурации сервера не поддерживается.",
                $"Unsupported schemaVersion {schemaVersion} in {documentName}.");
        }
    }

    private static ServerConnectionException InvalidConfiguration(string technicalMessage) =>
        new("Конфигурация сервера некорректна.", technicalMessage);

    private static string GetStatusMessage(HttpStatusCode statusCode, string documentName) => statusCode switch
    {
        HttpStatusCode.NotFound => $"Сервер не содержит {documentName} (HTTP 404).",
        >= HttpStatusCode.InternalServerError => "Сервер временно недоступен. Повторите попытку позже.",
        _ => $"Сервер отклонил запрос (HTTP {(int)statusCode}).",
    };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsAllowedEndpoint(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback);

    private sealed class BootstrapDto
    {
        public int SchemaVersion { get; init; }

        public string? ServerName { get; init; }

        public string? ProfilesUrl { get; init; }

        public string? DefaultProfileId { get; init; }
    }

    private sealed class ProfilesDto
    {
        public int SchemaVersion { get; init; }

        public List<ProfileDto>? Profiles { get; init; }
    }

    private sealed class ProfileDto
    {
        public string? Id { get; init; }

        public string? Name { get; init; }

        public string? MinecraftVersion { get; init; }

        public LoaderDto? Loader { get; init; }

        public string? PackVersion { get; init; }

        public string? ManifestUrl { get; init; }

        public string? ServerAddress { get; init; }

        public int ServerPort { get; init; }
    }

    private sealed class LoaderDto
    {
        public string? Type { get; init; }

        public string? Version { get; init; }
    }
}
