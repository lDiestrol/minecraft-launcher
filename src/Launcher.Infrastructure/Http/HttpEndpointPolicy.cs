namespace Launcher.Infrastructure.Http;

internal static class HttpEndpointPolicy
{
    public static bool IsAllowed(Uri uri) =>
        uri.IsAbsoluteUri &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
         (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback));
}
