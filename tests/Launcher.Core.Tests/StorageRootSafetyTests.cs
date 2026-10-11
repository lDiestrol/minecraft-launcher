using System.Diagnostics;
using System.Text.Json;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Game;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class StorageRootSafetyTests
{
    [Theory]
    [InlineData("//server/share/GameData")]
    [InlineData("\\/server/share/GameData")]
    [InlineData("/\\server\\share/GameData")]
    [InlineData("//?/C:/GameData")]
    [InlineData("//./C:/GameData")]
    [InlineData("\\\\?\\C:\\GameData")]
    [InlineData("\\\\.\\C:\\GameData")]
    public void NormalizedNetworkAndDevicePaths_CannotPersistPointer(string path)
    {
        using TemporaryStorage directory = new();
        StorageRootResolver resolver = new(Path.Combine(directory.Path, "local"), Path.Combine(directory.Path, "App"));
        Assert.Equal(StorageRootError.UnsafePath,
            Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(path)).Error);
        Assert.False(File.Exists(resolver.ConfigurationFile));
    }

    [Fact]
    public void NetworkDriveClassification_ReceivesCanonicalVolumeAndRejectsMappedLetters()
    {
        string? checkedVolume = null;
        Assert.Equal(StorageRootError.UnsafePath,
            Assert.Throws<StorageRootException>(() => StoragePathPolicy.NormalizeDirectory("Z:/GameData", volume =>
            {
                checkedVolume = volume;
                return DriveType.Network;
            })).Error);
        Assert.Equal("Z:\\", checkedVolume);
    }

    [Fact]
    public void MixedLocalSeparators_PersistAndReopenIdentically()
    {
        using TemporaryStorage directory = new();
        StorageRootResolver resolver = new(Path.Combine(directory.Path, "local"), Path.Combine(directory.Path, "App"));
        string root = Path.Combine(directory.Path, "GameData");
        resolver.ConfigureNewRoot(root.Replace('\\', '/'));
        using StorageRootSession session = resolver.OpenSession();
        Assert.Equal(root, session.Paths.RootDirectory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettingsLeafLink_IsNotReadOrOverwritten(bool dangling)
    {
        using TemporaryStorage directory = new();
        LauncherDataPaths paths = new(Path.Combine(directory.Path, "GameData"));
        paths.EnsureRootAvailable();
        string external = Path.Combine(directory.Path, "world.dat");
        if (!dangling)
        {
            await File.WriteAllTextAsync(external, "world sentinel");
        }

        File.CreateSymbolicLink(paths.SettingsFile, external);
        JsonSettingsStore store = new(paths, new NullAppLogger());
        await Assert.ThrowsAsync<StorageRootException>(() => store.LoadAsync());
        await Assert.ThrowsAsync<StorageRootException>(() => store.SaveAsync(new LauncherSettings()));
        Assert.Equal(!dangling, File.Exists(external));
        if (!dangling)
        {
            Assert.Equal("world sentinel", await File.ReadAllTextAsync(external));
        }
    }

    [Fact]
    public async Task ExistingSettingsTemporaryLink_IsNeverUsed()
    {
        using TemporaryStorage directory = new();
        LauncherDataPaths paths = new(Path.Combine(directory.Path, "GameData"));
        paths.EnsureRootAvailable();
        string external = Path.Combine(directory.Path, "world.dat");
        await File.WriteAllTextAsync(external, "world sentinel");
        File.CreateSymbolicLink(paths.SettingsFile + ".tmp", external);
        JsonSettingsStore store = new(paths, new NullAppLogger());
        await store.SaveAsync(new LauncherSettings { Nickname = "SafePlayer" });
        Assert.Equal("SafePlayer", (await store.LoadAsync()).Nickname);
        Assert.Equal("world sentinel", await File.ReadAllTextAsync(external));
        Assert.Empty(Directory.EnumerateFiles(paths.RootDirectory, "settings.json.*.tmp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogLeafLink_NeverAppendsToExternalFile(bool dangling)
    {
        using TemporaryStorage directory = new();
        LauncherDataPaths paths = new(Path.Combine(directory.Path, "GameData"));
        FileAppLogger logger = new(paths);
        string external = Path.Combine(directory.Path, "world.dat");
        if (!dangling)
        {
            await File.WriteAllTextAsync(external, "world sentinel");
        }

        File.CreateSymbolicLink(Path.Combine(paths.LogsDirectory, $"launcher-{DateTimeOffset.Now:yyyyMMdd}.log"), external);
        logger.Info("must not escape");
        Assert.Equal(!dangling, File.Exists(external));
        if (!dangling)
        {
            Assert.Equal("world sentinel", await File.ReadAllTextAsync(external));
        }
    }

    [Fact]
    public void LogAppend_PreservesBothIndependentWriters()
    {
        using TemporaryStorage directory = new();
        LauncherDataPaths paths = new(Path.Combine(directory.Path, "GameData"));
        FileAppLogger first = new(paths);
        FileAppLogger second = new(paths);
        Parallel.For(0, 100, index => (index % 2 == 0 ? first : second).Info($"entry:{index}:end"));
        string[] lines = File.ReadAllLines(Directory.GetFiles(paths.LogsDirectory).Single());
        Assert.Equal(100, lines.Length);
        for (int index = 0; index < 100; index++)
        {
            Assert.Single(lines, line => line.EndsWith($"entry:{index}:end", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void JobNameCollision_IsRejectedWithoutReusingOriginalJob()
    {
        string token = Guid.NewGuid().ToString("N");
        using WindowsGameJob first = new(token);
        Assert.Throws<System.ComponentModel.Win32Exception>(() => new WindowsGameJob(token));
        Assert.Equal(0u, first.ActiveProcesses);
        Assert.StartsWith("Global\\MinecraftLauncher.Game.", first.Name);
    }

    [Fact]
    public void UncertainIntent_IsNotRemovedOrReplaced()
    {
        using TemporaryStorage directory = new();
        StorageRootResolver resolver = new(Path.Combine(directory.Path, "local"), Path.Combine(directory.Path, "App"));
        LauncherDataPaths paths;
        using (StorageRootSession session = resolver.OpenSession())
        {
            paths = session.Paths;
        }

        ProfileGameActivity activity = new(paths, new NullAppLogger());
        string file = activity.GetStatePath("main");
        string token = Guid.NewGuid().ToString("N");
        ActiveGameState invalid = new(2, "main", 0, 0, token, "Local\\wrong-session", 123, 456);
        string original = JsonSerializer.Serialize(invalid);
        File.WriteAllText(file, original);
        Assert.Throws<PackSyncException>(() => activity.IsActive("main"));
        Assert.Throws<PackSyncException>(() => activity.TryAcquireOwnership("main", out _));
        Assert.Throws<PackSyncException>(() => resolver.ConfigureNewRoot(paths.RootDirectory));
        Assert.Equal(original, File.ReadAllText(file));
    }

    [Fact]
    public async Task AtomicLaunch_PreservesArgumentsEnvironmentDirectoryStreamsAndExitCode()
    {
        using TemporaryStorage directory = new();
        ProcessStartInfo start = new(Environment.GetEnvironmentVariable("ComSpec")!)
        {
            UseShellExecute = false,
            WorkingDirectory = directory.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Arguments = "/d /c \"echo %LAUNCHER_TEST% & cd & echo stderr-marker 1>&2 & exit /b 37\"",
        };
        start.Environment.Clear();
        start.Environment["LAUNCHER_TEST"] = "environment marker";
        CaptureLogger logger = new();
        using WindowsGameJob job = new(Guid.NewGuid().ToString("N"));
        using AtomicJobProcess process = new();
        process.Start(start, job, logger);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(37, process.ExitCode);
        Assert.Contains(logger.Lines, line => line.Contains("environment marker", StringComparison.Ordinal));
        Assert.Contains(logger.Lines, line => line.Contains(directory.Path, StringComparison.Ordinal));
        Assert.Contains(logger.Errors, line => line.Contains("stderr-marker", StringComparison.Ordinal));
        Assert.Equal(0u, job.ActiveProcesses);
    }

    private sealed class CaptureLogger : IAppLogger
    {
        public System.Collections.Concurrent.ConcurrentBag<string> Lines { get; } = [];
        public System.Collections.Concurrent.ConcurrentBag<string> Errors { get; } = [];
        public void Info(string message) => Lines.Add(message);
        public void Error(string message, Exception? exception = null) => Errors.Add(message);
    }
}
