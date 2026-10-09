using System.Text;
using System.Text.Json;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Launcher.Core.Tests;

public sealed class GithubReleaseFeedTests
{
    private const string AppId = "lDiestrol.MinecraftLauncher";
    private const string Channel = "stable";
    private const string RepositoryUrl = "https://github.com/example/minecraft-launcher";

    [Fact]
    public async Task SeparatePerReleaseFeeds_ResolveEveryAssetFromOwningRelease()
    {
        FeedScenario scenario = FeedScenario.Create(includeHistoricalEntryInNewFeed: false);
        GithubSource source = new(RepositoryUrl, accessToken: null, prerelease: true, scenario.Downloader);

        VelopackAssetFeed feed = await source.GetReleaseFeed(
            NullVelopackLogger.Instance,
            AppId,
            Channel);

        Assert.Equal(3, feed.Assets.Length);
        Assert.Equal(
            3,
            feed.Assets
                .Select(asset => $"{asset.Version}|{asset.Type}|{asset.FileName}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        string testDirectory = Path.Combine(Path.GetTempPath(), $"launcher-github-feed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);

        try
        {
            foreach (VelopackAsset asset in feed.Assets)
            {
                string destination = Path.Combine(testDirectory, asset.FileName);
                await source.DownloadReleaseEntry(
                    NullVelopackLogger.Instance,
                    asset,
                    destination,
                    _ => { },
                    CancellationToken.None);
            }

            Assert.Contains(FeedScenario.V060FullUrl, scenario.Downloader.DownloadedUrls);
            Assert.Contains(FeedScenario.V060DeltaUrl, scenario.Downloader.DownloadedUrls);
            Assert.Contains(FeedScenario.V050FullUrl, scenario.Downloader.DownloadedUrls);
            Assert.Equal(3, scenario.Downloader.DownloadedUrls.Count);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HistoricalEntryInNewReleaseFeed_IsBoundToNewReleaseAndCannotResolveMissingAsset()
    {
        FeedScenario scenario = FeedScenario.Create(includeHistoricalEntryInNewFeed: true);
        GithubSource source = new(RepositoryUrl, accessToken: null, prerelease: true, scenario.Downloader);

        VelopackAssetFeed feed = await source.GetReleaseFeed(
            NullVelopackLogger.Instance,
            AppId,
            Channel);

        VelopackAsset[] historicalEntries = feed.Assets
            .Where(asset => asset.Version.ToString() == "0.5.0" && asset.Type == VelopackAssetType.Full)
            .ToArray();

        Assert.Equal(2, historicalEntries.Length);

        string destination = Path.Combine(Path.GetTempPath(), $"launcher-feed-invalid-{Guid.NewGuid():N}.nupkg");
        try
        {
            ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(() =>
                source.DownloadReleaseEntry(
                    NullVelopackLogger.Instance,
                    historicalEntries[0],
                    destination,
                    _ => { },
                    CancellationToken.None));

            Assert.Contains("Could not find asset", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(destination));
        }
        finally
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
    }

    private sealed class FeedScenario
    {
        public const string V060FeedUrl = "https://downloads.test/v0.6.0/releases.stable.json";
        public const string V060FullUrl = "https://downloads.test/v0.6.0/Launcher-0.6.0-full.nupkg";
        public const string V060DeltaUrl = "https://downloads.test/v0.6.0/Launcher-0.6.0-delta.nupkg";
        public const string V050FeedUrl = "https://downloads.test/v0.5.0/releases.stable.json";
        public const string V050FullUrl = "https://downloads.test/v0.5.0/Launcher-0.5.0-full.nupkg";

        private FeedScenario(FakeFileDownloader downloader)
        {
            Downloader = downloader;
        }

        public FakeFileDownloader Downloader { get; }

        public static FeedScenario Create(bool includeHistoricalEntryInNewFeed)
        {
            object full060 = FeedAsset("0.6.0", "Full", "Launcher-0.6.0-full.nupkg", 600, 'a');
            object delta060 = FeedAsset("0.6.0", "Delta", "Launcher-0.6.0-delta.nupkg", 60, 'b');
            object full050 = FeedAsset("0.5.0", "Full", "Launcher-0.5.0-full.nupkg", 500, 'c');

            object[] newFeedAssets = includeHistoricalEntryInNewFeed
                ? [full060, delta060, full050]
                : [full060, delta060];

            string releasesJson = JsonSerializer.Serialize(new object[]
            {
                Release(
                    "v0.6.0",
                    "2026-10-09T10:00:00Z",
                    Asset("releases.stable.json", V060FeedUrl),
                    Asset("Launcher-0.6.0-full.nupkg", V060FullUrl),
                    Asset("Launcher-0.6.0-delta.nupkg", V060DeltaUrl)),
                Release(
                    "v0.5.0",
                    "2026-09-01T10:00:00Z",
                    Asset("releases.stable.json", V050FeedUrl),
                    Asset("Launcher-0.5.0-full.nupkg", V050FullUrl)),
            });

            Dictionary<string, byte[]> responses = new(StringComparer.OrdinalIgnoreCase)
            {
                [V060FeedUrl] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Assets = newFeedAssets })),
                [V050FeedUrl] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Assets = new[] { full050 } })),
                [V060FullUrl] = Encoding.UTF8.GetBytes("v0.6.0-full"),
                [V060DeltaUrl] = Encoding.UTF8.GetBytes("v0.6.0-delta"),
                [V050FullUrl] = Encoding.UTF8.GetBytes("v0.5.0-full"),
            };

            return new FeedScenario(new FakeFileDownloader(releasesJson, responses));
        }

        private static Dictionary<string, object?> Release(
            string name,
            string publishedAt,
            params Dictionary<string, object?>[] assets) =>
            new()
            {
                ["name"] = name,
                ["prerelease"] = false,
                ["published_at"] = publishedAt,
                ["assets"] = assets,
            };

        private static Dictionary<string, object?> Asset(string name, string url) =>
            new()
            {
                ["name"] = name,
                ["browser_download_url"] = url,
            };

        private static object FeedAsset(string version, string type, string fileName, long size, char hashCharacter) =>
            new
            {
                PackageId = AppId,
                Version = version,
                Type = type,
                FileName = fileName,
                SHA1 = new string(hashCharacter, 40),
                SHA256 = new string(hashCharacter, 64),
                Size = size,
            };
    }

    private sealed class FakeFileDownloader : IFileDownloader
    {
        private readonly string _releasesJson;
        private readonly IReadOnlyDictionary<string, byte[]> _responses;

        public FakeFileDownloader(string releasesJson, IReadOnlyDictionary<string, byte[]> responses)
        {
            _releasesJson = releasesJson;
            _responses = responses;
        }

        public List<string> DownloadedUrls { get; } = [];

        public Task<string> DownloadString(
            string url,
            IDictionary<string, string>? headers = null,
            double timeout = 30)
        {
            if (url.Contains("/releases?", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(_releasesJson);
            }

            if (!_responses.TryGetValue(url, out byte[]? bytes))
            {
                throw new InvalidOperationException($"Unexpected URL: {url}");
            }

            return Task.FromResult(Encoding.UTF8.GetString(bytes));
        }

        public Task<byte[]> DownloadBytes(
            string url,
            IDictionary<string, string>? headers = null,
            double timeout = 30)
        {
            if (!_responses.TryGetValue(url, out byte[]? bytes))
            {
                throw new InvalidOperationException($"Unexpected URL: {url}");
            }

            return Task.FromResult(bytes);
        }

        public async Task DownloadFile(
            string url,
            string localFile,
            Action<int> progress,
            IDictionary<string, string>? headers = null,
            double timeout = 30,
            CancellationToken cancelToken = default)
        {
            if (!_responses.TryGetValue(url, out byte[]? bytes))
            {
                throw new InvalidOperationException($"Unexpected URL: {url}");
            }

            DownloadedUrls.Add(url);
            await File.WriteAllBytesAsync(localFile, bytes, cancelToken);
            progress(100);
        }
    }
}
