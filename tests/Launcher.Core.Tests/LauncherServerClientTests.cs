using System.Net;
using System.Text;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Http;
using Launcher.Infrastructure.Logging;

namespace Launcher.Core.Tests;

public sealed class LauncherServerClientTests
{
    private static readonly Uri BootstrapUri = new("https://example.test/launcher/bootstrap.json");

    [Fact]
    public async Task GetBootstrapAsync_ParsesOrdinaryResponseBelow256KiB()
    {
        LauncherServerClient client = CreateClient(Json("""
            {
              "schemaVersion": 1,
              "serverName": "Example Server",
              "profilesUrl": "/launcher/profiles.json",
              "defaultProfileId": "main"
            }
            """));

        BootstrapConfiguration result = await client.GetBootstrapAsync(BootstrapUri, CancellationToken.None);

        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal("Example Server", result.ServerName);
        Assert.Equal("https://example.test/launcher/profiles.json", result.ProfilesUri.AbsoluteUri);
        Assert.Equal("main", result.DefaultProfileId);
    }

    [Fact]
    public async Task GetBootstrapAsync_PreservesAbsoluteProfilesUrl()
    {
        LauncherServerClient client = CreateClient(Json("""
            {"schemaVersion":1,"serverName":"Server","profilesUrl":"https://cdn.example.test/profiles.json"}
            """));

        BootstrapConfiguration result = await client.GetBootstrapAsync(BootstrapUri, CancellationToken.None);

        Assert.Equal("https://cdn.example.test/profiles.json", result.ProfilesUri.AbsoluteUri);
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsNewerSchemaWithSpecificMessage()
    {
        LauncherServerClient client = CreateClient(Json("""
            {"schemaVersion":2,"serverName":"Server","profilesUrl":"/profiles.json"}
            """));

        ServerConnectionException exception = await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));

        Assert.Contains("более новой версии", exception.UserMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"profilesUrl\":\"/profiles.json\"}")]
    [InlineData("{\"schemaVersion\":1,\"serverName\":\"Server\"}")]
    [InlineData("{\"serverName\":\"Server\",\"profilesUrl\":\"/profiles.json\"}")]
    public async Task GetBootstrapAsync_RejectsMissingRequiredFields(string json)
    {
        LauncherServerClient client = CreateClient(Json(json));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsInvalidJson()
    {
        LauncherServerClient client = CreateClient(Json("{not-json"));

        ServerConnectionException exception = await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));

        Assert.Contains("bootstrap.json", exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsRemoteHttpProfilesUrl()
    {
        LauncherServerClient client = CreateClient(Json("""
            {"schemaVersion":1,"serverName":"Server","profilesUrl":"http://cdn.example.test/profiles.json"}
            """));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsRemoteHttpRequestAtClientBoundary()
    {
        LauncherServerClient client = CreateClient(Json("{}"));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(
                new Uri("http://example.test/launcher/bootstrap.json"),
                CancellationToken.None));
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task GetBootstrapAsync_FollowsHttpsRedirectAfterValidatingTarget(int statusCode)
    {
        Uri redirectedUri = new("https://example.test/redirected/bootstrap.json");
        (LauncherServerClient client, RecordingHttpMessageHandler handler) = CreateClient(
            (_, requestNumber) => requestNumber == 1
                ? Redirect(statusCode, "/redirected/bootstrap.json")
                : Json("""
                    {"schemaVersion":1,"serverName":"Server","profilesUrl":"profiles.json"}
                    """));

        BootstrapConfiguration result = await client.GetBootstrapAsync(BootstrapUri, CancellationToken.None);

        Assert.Equal([BootstrapUri, redirectedUri], handler.RequestUris);
        Assert.Equal(redirectedUri, result.BootstrapUri);
        Assert.Equal("https://example.test/redirected/profiles.json", result.ProfilesUri.AbsoluteUri);
    }

    [Fact]
    public async Task GetBootstrapAsync_FollowsLoopbackHttpRedirect()
    {
        Uri initialUri = new("http://localhost:8080/bootstrap.json");
        Uri redirectedUri = new("http://localhost:8080/config/bootstrap.json");
        (LauncherServerClient client, RecordingHttpMessageHandler handler) = CreateClient(
            (_, requestNumber) => requestNumber == 1
                ? Redirect(302, "/config/bootstrap.json")
                : Json("""
                    {"schemaVersion":1,"serverName":"Local","profilesUrl":"profiles.json"}
                    """));

        BootstrapConfiguration result = await client.GetBootstrapAsync(initialUri, CancellationToken.None);

        Assert.Equal([initialUri, redirectedUri], handler.RequestUris);
        Assert.Equal(redirectedUri, result.BootstrapUri);
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsLoopbackToRemoteHttpBeforeSecondRequest()
    {
        Uri initialUri = new("http://localhost:8080/bootstrap.json");
        Uri forbiddenUri = new("http://remote.example.test/bootstrap.json");
        (LauncherServerClient client, RecordingHttpMessageHandler handler) = CreateClient(
            (_, _) => Redirect(302, forbiddenUri.AbsoluteUri));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(initialUri, CancellationToken.None));

        Assert.Equal(initialUri, Assert.Single(handler.RequestUris));
        Assert.DoesNotContain(forbiddenUri, handler.RequestUris);
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsHttpsToRemoteHttpBeforeSecondRequest()
    {
        Uri forbiddenUri = new("http://remote.example.test/bootstrap.json");
        (LauncherServerClient client, RecordingHttpMessageHandler handler) = CreateClient(
            (_, _) => Redirect(307, forbiddenUri.AbsoluteUri));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));

        Assert.Equal(BootstrapUri, Assert.Single(handler.RequestUris));
        Assert.DoesNotContain(forbiddenUri, handler.RequestUris);
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsRedirectWithoutLocation()
    {
        (LauncherServerClient client, RecordingHttpMessageHandler handler) = CreateClient(
            (_, _) => new HttpResponseMessage(HttpStatusCode.Found));

        ServerConnectionException exception = await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));

        Assert.Contains("перенаправление", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsRedirectWithInvalidLocation()
    {
        (LauncherServerClient client, RecordingHttpMessageHandler handler) = CreateClient(
            (_, _) =>
            {
                HttpResponseMessage response = new(HttpStatusCode.Found);
                Assert.True(response.Headers.TryAddWithoutValidation("Location", "http://[invalid"));
                return response;
            });

        ServerConnectionException exception = await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));

        Assert.Contains("перенаправление", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsRedirectLoopAfterFiveRedirects()
    {
        (LauncherServerClient client, RecordingHttpMessageHandler handler) = CreateClient(
            (_, _) => Redirect(302, "/loop/bootstrap.json"));

        ServerConnectionException exception = await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));

        Assert.Contains("слишком много", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(6, handler.RequestUris.Count);
    }

    [Fact]
    public async Task GetBootstrapAsync_MapsNotFoundToFriendlyError()
    {
        LauncherServerClient client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        ServerConnectionException exception = await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));

        Assert.Contains("404", exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBootstrapAsync_RejectsContentLengthAbove256KiB()
    {
        LauncherServerClient client = CreateClient(Json(new string('x', 256 * 1024 + 1)));

        ServerConnectionException exception = await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetBootstrapAsync(BootstrapUri, CancellationToken.None));

        Assert.Contains("слишком большой", exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProfilesAsync_ParsesOrdinaryResponseBelowOneMegabyte()
    {
        LauncherServerClient client = CreateClient(Json(ValidProfilesJson));

        IReadOnlyList<GameProfile> profiles = await client.GetProfilesAsync(Bootstrap(), CancellationToken.None);

        GameProfile profile = Assert.Single(profiles);
        Assert.Equal("main", profile.Id);
        Assert.Equal("fabric", profile.LoaderType);
        Assert.Equal("https://example.test/launcher/packs/main/manifest.json", profile.ManifestUrl.AbsoluteUri);
        Assert.Equal((ushort)25565, profile.ServerPort);
    }

    [Fact]
    public async Task GetProfilesAsync_ResolvesManifestAgainstRedirectedProfilesUri()
    {
        Uri redirectedUri = new("https://cdn.example.test/config/profiles.json");
        string redirectedProfiles = ValidProfilesJson.Replace(
            "/launcher/packs/main/manifest.json",
            "pack.json",
            StringComparison.Ordinal);
        (LauncherServerClient client, RecordingHttpMessageHandler handler) = CreateClient(
            (_, requestNumber) => requestNumber == 1
                ? Redirect(302, redirectedUri.AbsoluteUri)
                : Json(redirectedProfiles));

        IReadOnlyList<GameProfile> profiles = await client.GetProfilesAsync(
            Bootstrap(),
            CancellationToken.None);

        GameProfile profile = Assert.Single(profiles);
        Assert.Equal(
            "https://cdn.example.test/config/pack.json",
            profile.ManifestUrl.AbsoluteUri);
        Assert.Equal([Bootstrap().ProfilesUri, redirectedUri], handler.RequestUris);
    }

    [Fact]
    public async Task GetProfilesAsync_RejectsEmptyProfiles()
    {
        LauncherServerClient client = CreateClient(Json("{\"schemaVersion\":1,\"profiles\":[]}"));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetProfilesAsync(Bootstrap(), CancellationToken.None));
    }

    [Fact]
    public async Task GetProfilesAsync_RejectsMalformedProfile()
    {
        LauncherServerClient client = CreateClient(Json("""
            {"schemaVersion":1,"profiles":[{"id":"main","name":"Missing fields"}]}
            """));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetProfilesAsync(Bootstrap(), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task GetProfilesAsync_RejectsSchemaMismatch(int schemaVersion)
    {
        string json = ValidProfilesJson.Replace("\"schemaVersion\": 1", $"\"schemaVersion\": {schemaVersion}", StringComparison.Ordinal);
        LauncherServerClient client = CreateClient(Json(json));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetProfilesAsync(Bootstrap(), CancellationToken.None));
    }

    [Fact]
    public async Task GetProfilesAsync_RejectsDuplicateProfileIds()
    {
        LauncherServerClient client = CreateClient(Json(DuplicateProfilesJson));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetProfilesAsync(Bootstrap(), CancellationToken.None));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("folder/profile")]
    [InlineData("folder\\profile")]
    public async Task GetProfilesAsync_RejectsUnsafeProfileId(string profileId)
    {
        string json = ValidProfilesJson.Replace("\"id\": \"main\"", $"\"id\": \"{profileId.Replace("\\", "\\\\", StringComparison.Ordinal)}\"", StringComparison.Ordinal);
        LauncherServerClient client = CreateClient(Json(json));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetProfilesAsync(Bootstrap(), CancellationToken.None));
    }

    [Theory]
    [InlineData("\"javaPath\":\"C:\\\\Java\\\\javaw.exe\"")]
    [InlineData("\"jvmArgs\":[\"-agentlib:jdwp=transport=dt_socket\"]")]
    [InlineData("\"executable\":\"https://example.test/java.exe\"")]
    [InlineData("\"instancePath\":\"C:\\\\Users\\\\Public\"")]
    public async Task GetProfilesAsync_RejectsUnknownLaunchControlFields(string unknownField)
    {
        string json = ValidProfilesJson.Replace(
            "\"serverPort\": 25565",
            $"\"serverPort\": 25565, {unknownField}",
            StringComparison.Ordinal);
        LauncherServerClient client = CreateClient(Json(json));

        await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetProfilesAsync(Bootstrap(), CancellationToken.None));
    }

    [Fact]
    public async Task GetProfilesAsync_RejectsUnknownLengthResponseAboveOneMegabyte()
    {
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(new byte[1024 * 1024 + 1]),
        };
        LauncherServerClient client = CreateClient(response);

        ServerConnectionException exception = await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetProfilesAsync(Bootstrap(), CancellationToken.None));

        Assert.Contains("слишком большой", exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProfilesAsync_RejectsContentLengthAboveOneMegabyte()
    {
        LauncherServerClient client = CreateClient(Json(new string('x', 1024 * 1024 + 1)));

        ServerConnectionException exception = await Assert.ThrowsAsync<ServerConnectionException>(
            () => client.GetProfilesAsync(Bootstrap(), CancellationToken.None));

        Assert.Contains("слишком большой", exception.UserMessage, StringComparison.Ordinal);
    }

    private static BootstrapConfiguration Bootstrap() => new(
        1,
        "Server",
        BootstrapUri,
        new Uri("https://example.test/launcher/profiles.json"),
        "main");

    private static LauncherServerClient CreateClient(HttpResponseMessage response)
    {
        HttpClient httpClient = new(new StubHttpMessageHandler(response));
        return new LauncherServerClient(httpClient, new NullAppLogger());
    }

    private static (LauncherServerClient Client, RecordingHttpMessageHandler Handler) CreateClient(
        Func<Uri, int, HttpResponseMessage> responder)
    {
        RecordingHttpMessageHandler handler = new(responder);
        HttpClient httpClient = new(handler);
        return (new LauncherServerClient(httpClient, new NullAppLogger()), handler);
    }

    private static HttpResponseMessage Redirect(int statusCode, string location)
    {
        HttpResponseMessage response = new((HttpStatusCode)statusCode);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private const string ValidProfilesJson = """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "id": "main",
              "name": "Main Server",
              "minecraftVersion": "1.20.1",
              "loader": {"type": "fabric", "version": "0.16.14"},
              "packVersion": "1.0.0",
              "manifestUrl": "/launcher/packs/main/manifest.json",
              "serverAddress": "mc.example.test",
              "serverPort": 25565
            }
          ]
        }
        """;

    private const string DuplicateProfilesJson = """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "id": "main",
              "name": "First",
              "minecraftVersion": "1.20.1",
              "loader": {"type": "fabric", "version": "0.16.14"},
              "packVersion": "1.0.0",
              "manifestUrl": "/first.json",
              "serverAddress": "mc.example.test",
              "serverPort": 25565
            },
            {
              "id": "MAIN",
              "name": "Duplicate",
              "minecraftVersion": "1.20.1",
              "loader": {"type": "fabric", "version": "0.16.14"},
              "packVersion": "1.0.0",
              "manifestUrl": "/duplicate.json",
              "serverAddress": "mc.example.test",
              "serverPort": 25565
            }
          ]
        }
        """;

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(_response);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<Uri, int, HttpResponseMessage> _responder;

        public RecordingHttpMessageHandler(Func<Uri, int, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri requestUri = request.RequestUri ?? throw new InvalidOperationException(
                "Test request did not contain an absolute URI.");
            RequestUris.Add(requestUri);
            return Task.FromResult(_responder(requestUri, RequestUris.Count));
        }
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _content;

        public UnknownLengthContent(byte[] content)
        {
            _content = content;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_content, 0, _content.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
