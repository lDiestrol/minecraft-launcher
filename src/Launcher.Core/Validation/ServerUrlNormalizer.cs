namespace Launcher.Core.Validation;

public static class ServerUrlNormalizer
{
    private const string DefaultBootstrapPath = "/launcher/bootstrap.json";

    public static bool TryNormalize(string? input, out Uri? bootstrapUri, out string? error)
    {
        bootstrapUri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input) ||
            !Uri.TryCreate(input.Trim(), UriKind.Absolute, out Uri? source) ||
            string.IsNullOrWhiteSpace(source.Host))
        {
            error = "Введите корректный абсолютный URL сервера.";
            return false;
        }

        bool isHttps = source.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        bool isHttp = source.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        if (!isHttps && !isHttp)
        {
            error = "Поддерживается только HTTPS (HTTP разрешён для localhost).";
            return false;
        }

        if (isHttp && !IsLoopbackHost(source))
        {
            error = "Для удалённого сервера требуется HTTPS.";
            return false;
        }

        if (!string.IsNullOrEmpty(source.UserInfo))
        {
            error = "URL не должен содержать имя пользователя или пароль.";
            return false;
        }

        UriBuilder builder = new(source) { Fragment = string.Empty };
        bool isJson = builder.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        if (!isJson)
        {
            builder.Path = DefaultBootstrapPath;
            builder.Query = string.Empty;
        }

        bootstrapUri = builder.Uri;
        return true;
    }

    private static bool IsLoopbackHost(Uri uri) =>
        uri.IsLoopback ||
        uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase);
}
