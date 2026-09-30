using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Pack;
using Launcher.Infrastructure.Persistence;
using IOFile = System.IO.File;

namespace Launcher.Core.Tests;

public sealed class PackSyncServiceTests
{
    private static readonly Uri ManifestUri = new("https://packs.example.test/main/manifest.json");
    private static readonly byte[] FileA = Encoding.UTF8.GetBytes("official-a");
    private static readonly byte[] FileB = Encoding.UTF8.GetBytes("official-b");

    [Fact]
    public async Task SyncAsync_DownloadsValidManifestFilesAndWritesState()
    {
        using TestDirectory directory = new();
        TestHandler handler = HandlerForManifest(Manifest(File("mods/official-a.jar", FileA)));
        PackSyncService service = CreateService(directory.Path, handler);

        PackSyncResult result = await service.SyncAsync(Profile(), Progress(), CancellationToken.None);

        Assert.Equal(FileA, await IOFile.ReadAllBytesAsync(ManagedPath(directory, "mods/official-a.jar")));
        Assert.True(IOFile.Exists(StatePath(directory)));
        Assert.Equal(1, result.CheckedFiles);
        Assert.Equal(1, result.DownloadedFiles);
        Assert.Equal(FileA.Length, result.DownloadedBytes);
    }

    [Theory]
    [InlineData("schemaVersion", "2", PackSyncError.UnsupportedSchema)]
    [InlineData("profileId", "other", PackSyncError.ProfileMismatch)]
    [InlineData("packVersion", "2.0.0", PackSyncError.ProfileMismatch)]
    [InlineData("minecraftVersion", "1.21", PackSyncError.ProfileMismatch)]
    [InlineData("loaderVersion", "0.15.0", PackSyncError.ProfileMismatch)]
    public async Task SyncAsync_RejectsManifestProfileMismatch(
        string field,
        string value,
        PackSyncError expectedError)
    {
        using TestDirectory directory = new();
        string manifest = Manifest(
            File("mods/a.jar", FileA),
            schemaVersion: field == "schemaVersion" ? int.Parse(value) : 1,
            profileId: field == "profileId" ? value : "main",
            packVersion: field == "packVersion" ? value : "1.0.0",
            minecraftVersion: field == "minecraftVersion" ? value : "1.20.1",
            loaderVersion: field == "loaderVersion" ? value : "0.16.14");
        PackSyncService service = CreateService(directory.Path, HandlerForManifest(manifest));

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(expectedError, exception.Error);
    }

    [Theory]
    [InlineData("../escape.jar")]
    [InlineData("C:/escape.jar")]
    [InlineData("mods\\escape.jar")]
    [InlineData(".launcher/state.json")]
    [InlineData("mods/../escape.jar")]
    [InlineData("mods/NUL.txt")]
    [InlineData("mods/trailing. ")]
    public async Task SyncAsync_RejectsUnsafeManagedPath(string path)
    {
        using TestDirectory directory = new();
        PackSyncService service = CreateService(
            directory.Path,
            HandlerForManifest(Manifest(File(path, FileA))));

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.UnsafeManagedPath, exception.Error);
    }

    [Fact]
    public async Task SyncAsync_RejectsCaseInsensitiveDuplicatePaths()
    {
        using TestDirectory directory = new();
        string manifest = Manifest(
            File("mods/Test.jar", FileA),
            File("mods/test.jar", FileB));
        PackSyncService service = CreateService(directory.Path, HandlerForManifest(manifest));

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.DuplicateManagedPath, exception.Error);
    }

    [Theory]
    [InlineData("short", 1)]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz", 1)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", -1)]
    public async Task SyncAsync_RejectsInvalidHashOrSize(string sha256, long size)
    {
        using TestDirectory directory = new();
        FileDefinition file = new("mods/a.jar", sha256, size, "files/a.jar", FileA);
        PackSyncService service = CreateService(
            directory.Path,
            HandlerForManifest(Manifest(file)));

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.ManifestInvalid, exception.Error);
    }

    [Fact]
    public async Task SyncAsync_RejectsRemoteHttpFileUrl()
    {
        using TestDirectory directory = new();
        FileDefinition file = File("mods/a.jar", FileA) with
        {
            Url = "http://remote.example.test/a.jar",
        };
        PackSyncService service = CreateService(
            directory.Path,
            HandlerForManifest(Manifest(file)));

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.UnsafeFileUrl, exception.Error);
    }

    [Fact]
    public async Task SyncAsync_FollowsHttpsManifestRedirectAndUsesFinalUriForFiles()
    {
        using TestDirectory directory = new();
        Uri finalManifest = new("https://cdn.example.test/packs/main/manifest.json");
        TestHandler handler = new((request, number) => number switch
        {
            1 => Redirect(HttpStatusCode.Found, finalManifest.AbsoluteUri),
            2 => Json(Manifest(File("mods/a.jar", FileA))),
            3 => Bytes(FileA),
            _ => throw new InvalidOperationException($"Unexpected request {request}.")
        });
        PackSyncService service = CreateService(directory.Path, handler);

        await service.SyncAsync(Profile(), Progress(), CancellationToken.None);

        Assert.Equal(
            [ManifestUri, finalManifest, new Uri("https://cdn.example.test/packs/main/files/mods/a.jar")],
            handler.RequestUris);
    }

    [Fact]
    public async Task SyncAsync_RejectsUnsafeRedirectBeforeRequest()
    {
        using TestDirectory directory = new();
        Uri forbidden = new("http://remote.example.test/manifest.json");
        TestHandler handler = new((_, _) => Redirect(HttpStatusCode.Found, forbidden.AbsoluteUri));
        PackSyncService service = CreateService(directory.Path, handler);

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.UnsafeFileUrl, exception.Error);
        Assert.Equal(ManifestUri, Assert.Single(handler.RequestUris));
        Assert.DoesNotContain(forbidden, handler.RequestUris);
    }

    [Fact]
    public async Task SyncAsync_DoesNotDownloadExistingCorrectFile()
    {
        using TestDirectory directory = new();
        string destination = ManagedPath(directory, "mods/a.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await IOFile.WriteAllBytesAsync(destination, FileA);
        TestHandler handler = HandlerForManifest(Manifest(File("mods/a.jar", FileA)));
        PackSyncService service = CreateService(directory.Path, handler);

        PackSyncResult result = await service.SyncAsync(Profile(), Progress(), CancellationToken.None);

        Assert.Equal(0, result.DownloadedFiles);
        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task SyncAsync_RepairsCorruptFile()
    {
        using TestDirectory directory = new();
        string destination = ManagedPath(directory, "config/a.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await IOFile.WriteAllTextAsync(destination, "corrupt");
        PackSyncService service = CreateService(
            directory.Path,
            HandlerForManifest(Manifest(File("config/a.cfg", FileA))));

        PackSyncResult result = await service.SyncAsync(Profile(), Progress(), CancellationToken.None);

        Assert.Equal(FileA, await IOFile.ReadAllBytesAsync(destination));
        Assert.Equal(1, result.RepairedFiles);
    }

    [Fact]
    public async Task SyncAsync_RejectsWrongDownloadedSizeAndPreservesDestination()
    {
        using TestDirectory directory = new();
        byte[] original = Encoding.UTF8.GetBytes("original-good-destination");
        string destination = ManagedPath(directory, "mods/a.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await IOFile.WriteAllBytesAsync(destination, original);
        FileDefinition expected = File("mods/a.jar", FileA);
        TestHandler handler = new((request, _) => request == ManifestUri
            ? Json(Manifest(expected))
            : Bytes(Encoding.UTF8.GetBytes("definitely-wrong-size")));
        PackSyncService service = CreateService(directory.Path, handler);

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.FileSizeMismatch, exception.Error);
        Assert.Equal(original, await IOFile.ReadAllBytesAsync(destination));
        Assert.False(IOFile.Exists(StatePath(directory)));
        Assert.Empty(Directory.EnumerateDirectories(StagingPath(directory)));
    }

    [Fact]
    public async Task SyncAsync_RejectsWrongHashAndPreservesDestinationAndState()
    {
        using TestDirectory directory = new();
        PackSyncService first = CreateService(
            directory.Path,
            HandlerForManifest(Manifest(File("mods/a.jar", FileA))));
        await first.SyncAsync(Profile(), Progress(), CancellationToken.None);
        string stateBefore = await IOFile.ReadAllTextAsync(StatePath(directory));

        GameProfile versionTwo = Profile("2.0.0");
        FileDefinition expected = File("mods/a.jar", FileB);
        TestHandler handler = new((request, _) => request == ManifestUri
            ? Json(Manifest(expected, packVersion: "2.0.0"))
            : Bytes(Encoding.UTF8.GetBytes("xxxxxxxxxx")));
        PackSyncService second = CreateService(directory.Path, handler);

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => second.SyncAsync(versionTwo, Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.HashMismatch, exception.Error);
        Assert.Equal(FileA, await IOFile.ReadAllBytesAsync(ManagedPath(directory, "mods/a.jar")));
        Assert.Equal(stateBefore, await IOFile.ReadAllTextAsync(StatePath(directory)));
        Assert.Empty(Directory.EnumerateDirectories(StagingPath(directory)));
    }

    [Fact]
    public async Task SyncAsync_RemovesOnlyObsoleteManagedFileAndPreservesUserFile()
    {
        using TestDirectory directory = new();
        PackSyncService first = CreateService(
            directory.Path,
            HandlerForManifest(Manifest(
                File("mods/official-a.jar", FileA),
                File("mods/official-b.jar", FileB))));
        await first.SyncAsync(Profile(), Progress(), CancellationToken.None);
        string userFile = ManagedPath(directory, "mods/my-custom.jar");
        await IOFile.WriteAllTextAsync(userFile, "user-owned");

        PackSyncService second = CreateService(
            directory.Path,
            HandlerForManifest(Manifest(File("mods/official-a.jar", FileA))));
        PackSyncResult result = await second.SyncAsync(Profile(), Progress(), CancellationToken.None);

        Assert.True(IOFile.Exists(ManagedPath(directory, "mods/official-a.jar")));
        Assert.False(IOFile.Exists(ManagedPath(directory, "mods/official-b.jar")));
        Assert.Equal("user-owned", await IOFile.ReadAllTextAsync(userFile));
        Assert.Equal(1, result.DeletedFiles);
        Assert.Equal(0, result.DownloadedFiles);
    }

    [Fact]
    public async Task SyncAsync_RejectsCorruptStateWithoutEscapingInstance()
    {
        using TestDirectory directory = new();
        string outside = Path.Combine(directory.Path, "outside.txt");
        await IOFile.WriteAllTextAsync(outside, "keep");
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath(directory))!);
        string corruptState = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            profileId = "main",
            packVersion = "0.9.0",
            files = new[]
            {
                new { path = "../outside.txt", sha256 = Sha(FileA), size = FileA.Length },
            },
        });
        await IOFile.WriteAllTextAsync(StatePath(directory), corruptState);
        PackSyncService service = CreateService(
            directory.Path,
            HandlerForManifest(Manifest(File("mods/a.jar", FileA))));

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.ManagedStateCorrupt, exception.Error);
        Assert.Equal("keep", await IOFile.ReadAllTextAsync(outside));
    }

    [Fact]
    public async Task SyncAsync_CancellationDuringDownloadLeavesNoTargetOrStateAndCleansStaging()
    {
        using TestDirectory directory = new();
        byte[] content = Enumerable.Repeat((byte)42, 256 * 1024).ToArray();
        TestHandler handler = HandlerForManifest(Manifest(File("mods/large.bin", content)));
        PackSyncService service = CreateService(directory.Path, handler);
        using CancellationTokenSource cancellation = new();
        ImmediateProgress progress = new(value =>
        {
            if (value.Stage == PackSyncStage.DownloadingFiles && value.CompletedBytes > 0)
            {
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SyncAsync(Profile(), progress, cancellation.Token));

        Assert.False(IOFile.Exists(ManagedPath(directory, "mods/large.bin")));
        Assert.False(IOFile.Exists(StatePath(directory)));
        Assert.Empty(Directory.EnumerateDirectories(StagingPath(directory)));
    }

    [Fact]
    public async Task SyncAsync_SecondSyncDownloadsZeroFiles()
    {
        using TestDirectory directory = new();
        string manifest = Manifest(File("mods/a.jar", FileA), File("config/a.cfg", FileB));
        PackSyncService first = CreateService(directory.Path, HandlerForManifest(manifest));
        await first.SyncAsync(Profile(), Progress(), CancellationToken.None);
        TestHandler secondHandler = HandlerForManifest(manifest);
        PackSyncService second = CreateService(directory.Path, secondHandler);

        PackSyncResult result = await second.SyncAsync(Profile(), Progress(), CancellationToken.None);

        Assert.Equal(0, result.DownloadedFiles);
        Assert.Single(secondHandler.RequestUris);
    }

    [Fact]
    public async Task SyncAsync_RejectsUnsafeFileRedirectBeforeTargetRequest()
    {
        using TestDirectory directory = new();
        Uri forbidden = new("http://remote.example.test/a.jar");
        TestHandler handler = new((request, number) => number switch
        {
            1 => Json(Manifest(File("mods/a.jar", FileA))),
            2 => Redirect(HttpStatusCode.TemporaryRedirect, forbidden.AbsoluteUri),
            _ => throw new InvalidOperationException($"Forbidden request reached handler: {request}."),
        });
        PackSyncService service = CreateService(directory.Path, handler);

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.UnsafeFileUrl, exception.Error);
        Assert.Equal(2, handler.RequestUris.Count);
        Assert.DoesNotContain(forbidden, handler.RequestUris);
    }

    [Fact]
    public async Task SyncAsync_RejectsUnknownRemoteControlField()
    {
        using TestDirectory directory = new();
        string manifest = Manifest(File("mods/a.jar", FileA));
        manifest = manifest.Replace(
            "\"files\":",
            "\"executable\":\"C:\\\\evil.exe\",\"files\":",
            StringComparison.Ordinal);
        PackSyncService service = CreateService(directory.Path, HandlerForManifest(manifest));

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.ManifestInvalid, exception.Error);
    }

    [Fact]
    public async Task SyncAsync_RejectsReparsePointParentWhenSupported()
    {
        using TestDirectory directory = new();
        string instance = Path.Combine(directory.Path, "instances", "main");
        string config = Path.Combine(instance, "config");
        string outside = Path.Combine(directory.Path, "outside");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(outside);
        string link = Path.Combine(config, "linked");
        try
        {
            Directory.CreateSymbolicLink(link, outside);
        }
        catch (Exception linkException) when (linkException is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return;
        }

        PackSyncService service = CreateService(
            directory.Path,
            HandlerForManifest(Manifest(File("config/linked/escape.cfg", FileA))));

        PackSyncException exception = await Assert.ThrowsAsync<PackSyncException>(
            () => service.SyncAsync(Profile(), Progress(), CancellationToken.None));

        Assert.Equal(PackSyncError.UnsafeManagedPath, exception.Error);
        Assert.False(IOFile.Exists(Path.Combine(outside, "escape.cfg")));
    }

    [Fact]
    public async Task LoopbackE2E_CoversFirstNoChangesRepairMissingObsoleteUserAndBadHash()
    {
        using TestDirectory directory = new();
        await using LoopbackServer server = new();
        Uri manifestUri = new(server.BaseUri, "packs/main/manifest.json");
        FileDefinition officialA = File("mods/launcher-test.txt", FileA);
        FileDefinition officialB = File("config/launcher-test.cfg", FileB);
        server.Set("/packs/main/manifest.json", Encoding.UTF8.GetBytes(Manifest(officialA, officialB)));
        server.Set("/packs/main/files/mods/launcher-test.txt", FileA);
        server.Set("/packs/main/files/config/launcher-test.cfg", FileB);
        using HttpClient httpClient = new(new HttpClientHandler { AllowAutoRedirect = false });
        PackSyncService service = new(httpClient, new LauncherDataPaths(directory.Path), new NullAppLogger());
        GameProfile profile = Profile(manifestUri);

        PackSyncResult first = await service.SyncAsync(profile, Progress(), CancellationToken.None);
        Assert.Equal(2, first.DownloadedFiles);
        Assert.True(IOFile.Exists(StatePath(directory)));

        PackSyncResult unchanged = await service.SyncAsync(profile, Progress(), CancellationToken.None);
        Assert.Equal(0, unchanged.DownloadedFiles);

        string configPath = ManagedPath(directory, "config/launcher-test.cfg");
        await IOFile.WriteAllTextAsync(configPath, "corrupt");
        PackSyncResult repaired = await service.SyncAsync(profile, Progress(), CancellationToken.None);
        Assert.Equal(1, repaired.RepairedFiles);
        Assert.Equal(FileB, await IOFile.ReadAllBytesAsync(configPath));

        string modPath = ManagedPath(directory, "mods/launcher-test.txt");
        IOFile.Delete(modPath);
        PackSyncResult missing = await service.SyncAsync(profile, Progress(), CancellationToken.None);
        Assert.Equal(1, missing.DownloadedFiles);
        Assert.Equal(FileA, await IOFile.ReadAllBytesAsync(modPath));

        string userFile = ManagedPath(directory, "mods/my-user-file.txt");
        await IOFile.WriteAllTextAsync(userFile, "user");
        server.Set("/packs/main/manifest.json", Encoding.UTF8.GetBytes(Manifest(officialA)));
        PackSyncResult obsolete = await service.SyncAsync(profile, Progress(), CancellationToken.None);
        Assert.Equal(1, obsolete.DeletedFiles);
        Assert.False(IOFile.Exists(configPath));
        Assert.Equal("user", await IOFile.ReadAllTextAsync(userFile));

        string previousState = await IOFile.ReadAllTextAsync(StatePath(directory));
        FileDefinition versionTwoFile = File("mods/launcher-test.txt", FileB);
        server.Set(
            "/packs/main/manifest.json",
            Encoding.UTF8.GetBytes(Manifest(versionTwoFile, packVersion: "2.0.0")));
        server.Set("/packs/main/files/mods/launcher-test.txt", Encoding.UTF8.GetBytes("bad-hash!!"));
        PackSyncException badHash = await Assert.ThrowsAsync<PackSyncException>(() =>
            service.SyncAsync(Profile(manifestUri, "2.0.0"), Progress(), CancellationToken.None));
        Assert.Equal(PackSyncError.HashMismatch, badHash.Error);
        Assert.Equal(FileA, await IOFile.ReadAllBytesAsync(modPath));
        Assert.Equal(previousState, await IOFile.ReadAllTextAsync(StatePath(directory)));
    }

    private static PackSyncService CreateService(string root, HttpMessageHandler handler) =>
        new(new HttpClient(handler), new LauncherDataPaths(root), new NullAppLogger());

    private static GameProfile Profile(string packVersion = "1.0.0") =>
        Profile(ManifestUri, packVersion);

    private static GameProfile Profile(Uri manifestUri, string packVersion = "1.0.0") =>
        new(
            "main",
            "Main",
            "1.20.1",
            "fabric",
            "0.16.14",
            packVersion,
            manifestUri,
            "localhost",
            25565);

    private static IProgress<PackSyncProgress> Progress() => new ImmediateProgress(_ => { });

    private static FileDefinition File(string path, byte[] content) =>
        new(path, Sha(content), content.LongLength, $"files/{path}", content);

    private static string Manifest(
        FileDefinition file,
        int schemaVersion = 1,
        string profileId = "main",
        string packVersion = "1.0.0",
        string minecraftVersion = "1.20.1",
        string loaderVersion = "0.16.14") =>
        Manifest(
            [file],
            schemaVersion,
            profileId,
            packVersion,
            minecraftVersion,
            loaderVersion);

    private static string Manifest(FileDefinition first, FileDefinition second) =>
        Manifest([first, second]);

    private static string Manifest(
        IReadOnlyList<FileDefinition> files,
        int schemaVersion = 1,
        string profileId = "main",
        string packVersion = "1.0.0",
        string minecraftVersion = "1.20.1",
        string loaderVersion = "0.16.14") =>
        JsonSerializer.Serialize(new
        {
            schemaVersion,
            profileId,
            packVersion,
            minecraftVersion,
            loader = new { type = "fabric", version = loaderVersion },
            files = files.Select(file => new
            {
                path = file.Path,
                sha256 = file.Sha256,
                size = file.Size,
                url = file.Url,
            }),
        });

    private static TestHandler HandlerForManifest(string manifest)
    {
        Dictionary<Uri, byte[]> contents = new();
        using (JsonDocument document = JsonDocument.Parse(manifest))
        {
            if (document.RootElement.TryGetProperty("files", out JsonElement files))
            {
                foreach (JsonElement file in files.EnumerateArray())
                {
                    string url = file.GetProperty("url").GetString()!;
                    string sha256 = file.GetProperty("sha256").GetString()!;
                    long size = file.GetProperty("size").GetInt64();
                    byte[] content = (sha256, size) switch
                    {
                        var value when value.sha256 == Sha(FileA) => FileA,
                        var value when value.sha256 == Sha(FileB) => FileB,
                        var value when value.size == 256 * 1024 =>
                            Enumerable.Repeat((byte)42, 256 * 1024).ToArray(),
                        _ => FileA,
                    };
                    contents[new Uri(ManifestUri, url)] = content;
                }
            }
        }

        return new TestHandler((request, _) => request == ManifestUri
            ? Json(manifest)
            : contents.TryGetValue(request, out byte[]? content)
                ? Bytes(content)
                : throw new InvalidOperationException($"Unexpected request {request}."));
    }

    private static string Sha(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage Bytes(byte[] content) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(content),
    };

    private static HttpResponseMessage Redirect(HttpStatusCode status, string location)
    {
        HttpResponseMessage response = new(status);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static string ManagedPath(TestDirectory directory, string relativePath) =>
        Path.Combine(
            directory.Path,
            "instances",
            "main",
            relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string StatePath(TestDirectory directory) =>
        Path.Combine(directory.Path, "instances", "main", ".launcher", "managed-state.json");

    private static string StagingPath(TestDirectory directory) =>
        Path.Combine(directory.Path, "instances", "main", ".launcher", "staging");

    private sealed record FileDefinition(
        string Path,
        string Sha256,
        long Size,
        string Url,
        byte[] Content);

    private sealed class ImmediateProgress : IProgress<PackSyncProgress>
    {
        private readonly Action<PackSyncProgress> _callback;

        public ImmediateProgress(Action<PackSyncProgress> callback)
        {
            _callback = callback;
        }

        public void Report(PackSyncProgress value) => _callback(value);
    }

    private sealed class TestHandler : HttpMessageHandler
    {
        private readonly Func<Uri, int, HttpResponseMessage> _responder;

        public TestHandler(Func<Uri, int, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uri uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is missing.");
            RequestUris.Add(uri);
            return Task.FromResult(_responder(uri, RequestUris.Count));
        }
    }

    private sealed class LoopbackServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Dictionary<string, byte[]> _routes = new(StringComparer.Ordinal);
        private readonly Task _acceptLoop;

        public LoopbackServer()
        {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUri = new Uri($"http://127.0.0.1:{port}/");
            _acceptLoop = AcceptLoopAsync();
        }

        public Uri BaseUri { get; }

        public void Set(string path, byte[] content)
        {
            lock (_routes)
            {
                _routes[path] = content;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            _listener.Stop();
            try
            {
                await _acceptLoop;
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException or OperationCanceledException)
            {
                _ = exception;
            }

            _cancellation.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                await using NetworkStream stream = client.GetStream();
                using (client)
                using (StreamReader reader = new(
                    stream,
                    Encoding.ASCII,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true))
                {
                    string requestLine = await reader.ReadLineAsync(_cancellation.Token) ?? string.Empty;
                    string[] parts = requestLine.Split(' ');
                    string path = parts.Length >= 2 ? parts[1] : "/";
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_cancellation.Token)))
                    {
                    }

                    byte[]? body;
                    lock (_routes)
                    {
                        _routes.TryGetValue(path, out body);
                    }

                    if (body is null)
                    {
                        body = Encoding.UTF8.GetBytes("not found");
                        await WriteResponseAsync(stream, "404 Not Found", body, _cancellation.Token);
                    }
                    else
                    {
                        await WriteResponseAsync(stream, "200 OK", body, _cancellation.Token);
                    }
                }
            }
        }

        private static async Task WriteResponseAsync(
            Stream stream,
            string status,
            byte[] body,
            CancellationToken cancellationToken)
        {
            byte[] headers = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headers, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MinecraftLauncherTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
