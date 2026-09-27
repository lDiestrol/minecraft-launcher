using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Core.Validation;

namespace Launcher.Infrastructure.Http;

public sealed class LauncherServerClient : ILauncherServerClient
{
    private const int SupportedSchemaVersion = 1;
    private const int MaximumBootstrapBytes = 256 * 1024;
    private const int MaximumProfilesBytes = 1024 * 1024;
    private const int MaximumRedirects = 5;
    private readonly HttpClient _httpClient;
    private readonly IAppLogger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public LauncherServerClient(HttpClient httpClient, IAppLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BootstrapConfiguration> GetBootstrapAsync(
        Uri bootstrapUri,
        CancellationToken cancellationToken)
    {
        DownloadedDocument downloaded = await DownloadStringAsync(
            bootstrapUri,
            "bootstrap.json",
            MaximumBootstrapBytes,
            cancellationToken);

        try
        {
            BootstrapDto? document = JsonSerializer.Deserialize<BootstrapDto>(downloaded.Content, _jsonOptions);
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

            if (!Uri.TryCreate(downloaded.FinalUri, document.ProfilesUrl, out Uri? profilesUri) ||
                !profilesUri.IsAbsoluteUri ||
                !IsAllowedEndpoint(profilesUri))
            {
                throw InvalidConfiguration("Поле profilesUrl содержит некорректный URL.");
            }

            return new BootstrapConfiguration(
                document.SchemaVersion,
                document.ServerName.Trim(),
                downloaded.FinalUri,
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
        DownloadedDocument downloaded = await DownloadStringAsync(
            bootstrap.ProfilesUri,
            "profiles.json",
            MaximumProfilesBytes,
            cancellationToken);

        try
        {
            ProfilesDto? document = JsonSerializer.Deserialize<ProfilesDto>(downloaded.Content, _jsonOptions);
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
                GameProfile mapped = MapProfile(profile, downloaded.FinalUri);
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

    private async Task<DownloadedDocument> DownloadStringAsync(
        Uri uri,
        string documentName,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        Uri currentUri = uri;
        try
        {
            for (int redirectCount = 0; ;)
            {
                if (!currentUri.IsAbsoluteUri || !IsAllowedEndpoint(currentUri))
                {
                    throw InvalidConfiguration($"URL для {documentName} использует запрещённую схему.");
                }

                using HttpRequestMessage request = new(HttpMethod.Get, currentUri);
                using HttpResponseMessage response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (IsRedirectStatusCode(response.StatusCode))
                {
                    if (redirectCount >= MaximumRedirects)
                    {
                        throw new ServerConnectionException(
                            $"Сервер выполнил слишком много перенаправлений для {documentName}.",
                            $"More than {MaximumRedirects} redirects while requesting {uri}.");
                    }

                    currentUri = GetRedirectTarget(response, currentUri, documentName);
                    redirectCount++;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Error($"HTTP {(int)response.StatusCode} ({response.StatusCode}) for {currentUri}.");
                    throw new ServerConnectionException(
                        GetStatusMessage(response.StatusCode, documentName),
                        $"HTTP {(int)response.StatusCode} while requesting {currentUri}.");
                }

                string content = await ReadLimitedContentAsync(
                    response.Content,
                    currentUri,
                    documentName,
                    maximumBytes,
                    cancellationToken);
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new ServerConnectionException(
                        $"Сервер вернул пустой {documentName}.",
                        $"Empty response for {currentUri}.");
                }

                return new DownloadedDocument(content, currentUri);
            }
        }
        catch (ServerConnectionException)
        {
            throw;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.Error($"HTTP timeout for {currentUri}.", exception);
            throw new ServerConnectionException(
                "Сервер не ответил вовремя. Проверьте адрес и повторите попытку.",
                $"HTTP timeout for {currentUri}.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.Error($"HTTP request failed for {currentUri}.", exception);
            throw new ServerConnectionException(
                "Не удалось подключиться к серверу. Проверьте адрес, сеть и сертификат.",
                $"HTTP request failed for {currentUri}.",
                exception);
        }
    }

    private static Uri GetRedirectTarget(
        HttpResponseMessage response,
        Uri currentUri,
        string documentName)
    {
        string[] locationValues = response.Headers.TryGetValues("Location", out IEnumerable<string>? values)
            ? values.Take(2).ToArray()
            : [];
        if (locationValues.Length != 1 ||
            string.IsNullOrWhiteSpace(locationValues[0]) ||
            !Uri.TryCreate(locationValues[0], UriKind.RelativeOrAbsolute, out Uri? location) ||
            !Uri.TryCreate(currentUri, location, out Uri? targetUri) ||
            !targetUri.IsAbsoluteUri)
        {
            throw new ServerConnectionException(
                $"Сервер вернул некорректное перенаправление для {documentName}.",
                $"Missing or invalid redirect Location while requesting {currentUri}.");
        }

        if (!IsAllowedEndpoint(targetUri))
        {
            throw InvalidConfiguration(
                $"Перенаправление для {documentName} ведёт на запрещённый URL '{targetUri}'.");
        }

        return targetUri;
    }

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode) =>
        (int)statusCode is 301 or 302 or 303 or 307 or 308;

    private async Task<string> ReadLimitedContentAsync(
        HttpContent content,
        Uri uri,
        string documentName,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > maximumBytes)
        {
            throw ConfigurationTooLarge(uri, documentName, maximumBytes, content.Headers.ContentLength.Value);
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
            if (totalBytes > maximumBytes)
            {
                throw ConfigurationTooLarge(uri, documentName, maximumBytes, totalBytes);
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return Encoding.UTF8.GetString(destination.GetBuffer(), 0, totalBytes);
    }

    private ServerConnectionException ConfigurationTooLarge(
        Uri uri,
        string documentName,
        int maximumBytes,
        long receivedBytes)
    {
        _logger.Error(
            $"Response for {uri} exceeds the {maximumBytes}-byte limit " +
            $"(reported or received: {receivedBytes} bytes).");
        return new ServerConnectionException(
            $"Файл {documentName} слишком большой.",
            $"Response for {uri} exceeds the {maximumBytes}-byte limit.");
    }

    private static GameProfile MapProfile(ProfileDto profile, Uri profilesUri)
    {
        if (!ProfileValueValidator.IsValidProfileId(profile.Id) ||
            string.IsNullOrWhiteSpace(profile.Name) ||
            !ProfileValueValidator.IsValidVersion(profile.MinecraftVersion) ||
            profile.Loader is null ||
            string.IsNullOrWhiteSpace(profile.Loader.Type) ||
            !ProfileValueValidator.IsValidVersion(profile.Loader.Version) ||
            string.IsNullOrWhiteSpace(profile.PackVersion) ||
            string.IsNullOrWhiteSpace(profile.ManifestUrl) ||
            !ProfileValueValidator.IsValidServerAddress(profile.ServerAddress) ||
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

    private sealed record DownloadedDocument(string Content, Uri FinalUri);

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
