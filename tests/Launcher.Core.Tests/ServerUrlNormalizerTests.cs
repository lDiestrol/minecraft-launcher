using Launcher.Core.Validation;

namespace Launcher.Core.Tests;

public sealed class ServerUrlNormalizerTests
{
    [Theory]
    [InlineData("https://example.ru", "https://example.ru/launcher/bootstrap.json")]
    [InlineData("https://example.ru/", "https://example.ru/launcher/bootstrap.json")]
    [InlineData("https://example.ru/community/", "https://example.ru/community/")]
    [InlineData("https://example.ru/custom/bootstrap.json", "https://example.ru/custom/bootstrap.json")]
    [InlineData("https://example.ru/custom/config", "https://example.ru/custom/config")]
    [InlineData("https://example.ru/custom/config.json?channel=dev", "https://example.ru/custom/config.json?channel=dev")]
    [InlineData("http://localhost:8080", "http://localhost:8080/launcher/bootstrap.json")]
    [InlineData("http://127.0.0.1:9000/", "http://127.0.0.1:9000/launcher/bootstrap.json")]
    [InlineData("http://[::1]:8080/", "http://[::1]:8080/launcher/bootstrap.json")]
    public void TryNormalize_AcceptsAndNormalizesSupportedUrls(string input, string expected)
    {
        bool result = ServerUrlNormalizer.TryNormalize(input, out Uri? uri, out string? error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(expected, uri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.ru/bootstrap.json")]
    [InlineData("http://example.ru")]
    [InlineData("https://user:password@example.ru")]
    [InlineData("https://example.ru?channel=dev")]
    [InlineData("https://example.ru/#section")]
    [InlineData("https://example.ru/custom/config.json#section")]
    public void TryNormalize_RejectsUnsafeOrInvalidUrls(string input)
    {
        bool result = ServerUrlNormalizer.TryNormalize(input, out Uri? uri, out string? error);

        Assert.False(result);
        Assert.Null(uri);
        Assert.NotNull(error);
    }
}
