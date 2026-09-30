using System.Net;
using Launcher.Core.Services;

namespace Launcher.Infrastructure.Http;

internal sealed class ValidatedHttpClient
{
    private const int MaximumRedirects = 5;
    private readonly HttpClient _httpClient;

    public ValidatedHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ValidatedHttpResponse> GetAsync(
        Uri initialUri,
        string resourceName,
        CancellationToken cancellationToken)
    {
        Uri currentUri = initialUri;
        for (int redirectCount = 0; ;)
        {
            if (!HttpEndpointPolicy.IsAllowed(currentUri))
            {
                throw new PackSyncException(
                    PackSyncError.UnsafeFileUrl,
                    "Сервер сборки использует запрещённый URL.",
                    $"Forbidden endpoint for {resourceName}: {currentUri}.");
            }

            HttpRequestMessage request = new(HttpMethod.Get, currentUri);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch
            {
                request.Dispose();
                throw;
            }

            request.Dispose();
            if (!IsRedirect(response.StatusCode))
            {
                return new ValidatedHttpResponse(response, currentUri);
            }

            using (response)
            {
                if (redirectCount >= MaximumRedirects)
                {
                    throw new PackSyncException(
                        PackSyncError.DownloadFailed,
                        "Сервер выполнил слишком много перенаправлений.",
                        $"More than {MaximumRedirects} redirects for {resourceName} at {initialUri}.");
                }

                currentUri = ResolveRedirectTarget(response, currentUri, resourceName);
                redirectCount++;
            }
        }
    }

    private static Uri ResolveRedirectTarget(
        HttpResponseMessage response,
        Uri currentUri,
        string resourceName)
    {
        string[] values = response.Headers.TryGetValues("Location", out IEnumerable<string>? locations)
            ? locations.Take(2).ToArray()
            : [];
        if (values.Length != 1 ||
            string.IsNullOrWhiteSpace(values[0]) ||
            !Uri.TryCreate(values[0], UriKind.RelativeOrAbsolute, out Uri? location) ||
            !Uri.TryCreate(currentUri, location, out Uri? targetUri) ||
            !targetUri.IsAbsoluteUri)
        {
            throw new PackSyncException(
                PackSyncError.DownloadFailed,
                "Сервер вернул некорректное перенаправление.",
                $"Missing or invalid Location for {resourceName} at {currentUri}.");
        }

        if (!HttpEndpointPolicy.IsAllowed(targetUri))
        {
            throw new PackSyncException(
                PackSyncError.UnsafeFileUrl,
                "Перенаправление ведёт на запрещённый URL.",
                $"Forbidden redirect target for {resourceName}: {targetUri}.");
        }

        return targetUri;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        (int)statusCode is 301 or 302 or 303 or 307 or 308;
}

internal sealed class ValidatedHttpResponse : IDisposable
{
    public ValidatedHttpResponse(HttpResponseMessage response, Uri finalUri)
    {
        Response = response;
        FinalUri = finalUri;
    }

    public HttpResponseMessage Response { get; }

    public Uri FinalUri { get; }

    public void Dispose() => Response.Dispose();
}
