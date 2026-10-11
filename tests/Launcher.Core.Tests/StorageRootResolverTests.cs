using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using CmlLib.Core;
using Launcher.Infrastructure.Game;
using Launcher.Core.Models;
using Launcher.Infrastructure.Logging;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Core.Tests;

public sealed class StorageRootResolverTests
{
    [Fact]
    public void NewRoot_PersistsVersionedPointerOutsideAppAndReloadsWithoutMigration()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        resolver.ConfigureNewRoot(directory.GameData);

        using JsonDocument config = JsonDocument.Parse(File.ReadAllText(resolver.ConfigurationFile));
        Assert.Equal(1, config.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(directory.GameData, config.RootElement.GetProperty("gameDataRoot").GetString());
        Assert.False(Directory.Exists(resolver.LegacyRootDirectory));
        Assert.Empty(Directory.EnumerateFiles(resolver.ControlDirectory, "*.tmp"));
        using StorageRootSession session = directory.Resolver("another-app-version").OpenSession();
        Assert.False(session.IsLegacy);
        Assert.Equal(directory.GameData, session.Paths.RootDirectory);
        Assert.Equal(Path.Combine(directory.GameData, "runtime"), session.Paths.RuntimeDirectory);
        Assert.Equal(Path.Combine(directory.GameData, "instances", "main"), session.Paths.GetInstanceDirectory("main"));
    }

    [Fact]
    public async Task LegacyMode_PreservesSettingsAndWorldsAndDoesNotWritePointer()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        LauncherDataPaths legacy = new(resolver.LegacyRootDirectory);
        JsonSettingsStore settings = new(legacy, new NullAppLogger());
        await settings.SaveAsync(new LauncherSettings { Nickname = "LegacyPlayer", RamMb = 4096 });
        string world = Path.Combine(legacy.GetInstanceDirectory("main"), "saves", "world", "level.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(world)!);
        await File.WriteAllTextAsync(world, "user-world");

        using StorageRootSession session = resolver.OpenSession();
        LauncherSettings loaded = await new JsonSettingsStore(session.Paths, new NullAppLogger()).LoadAsync();
        Assert.True(session.IsLegacy);
        Assert.Equal(legacy.RootDirectory, session.Paths.RootDirectory);
        Assert.Equal("LegacyPlayer", loaded.Nickname);
        Assert.Equal(4096, loaded.RamMb);
        Assert.Equal("user-world", await File.ReadAllTextAsync(world));
        Assert.False(File.Exists(resolver.ConfigurationFile));
    }

    [Fact]
    public void CleanLegacyFallback_RemainsDefaultRatherThanInferringAnAppRelativeDirectory()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver("portable-on-another-volume");
        using StorageRootSession session = resolver.OpenSession();
        Assert.True(session.IsLegacy);
        Assert.Equal(resolver.LegacyRootDirectory, session.Paths.RootDirectory);
        Assert.False(File.Exists(resolver.ConfigurationFile));
    }

    [Theory]
    [InlineData("installed/current")]
    [InlineData("portable")]
    [InlineData("development/bin")]
    public void ExecutionDirectoryDoesNotDetermineConfiguredRoot(string appDirectory)
    {
        using TestDirectory directory = new();
        directory.Resolver().ConfigureNewRoot(directory.GameData);
        using StorageRootSession session = directory.Resolver(appDirectory).OpenSession();
        Assert.Equal(directory.GameData, session.Paths.RootDirectory);
    }

    [Fact]
    public void ConfiguredRootMissing_FailsClosedEvenWhenLegacyDataExists()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        resolver.ConfigureNewRoot(directory.GameData);
        Directory.Delete(directory.GameData);
        Directory.CreateDirectory(resolver.LegacyRootDirectory);
        string legacyFile = Path.Combine(resolver.LegacyRootDirectory, "settings.json");
        File.WriteAllText(legacyFile, "legacy-unchanged");

        StorageRootException exception = Assert.Throws<StorageRootException>(() => resolver.OpenSession());
        Assert.Equal(StorageRootError.RootUnavailable, exception.Error);
        Assert.Contains("другой каталог не выбран", exception.Message);
        Assert.False(Directory.Exists(directory.GameData));
        Assert.Equal("legacy-unchanged", File.ReadAllText(legacyFile));
    }

    [Fact]
    public void MissingVolume_DoesNotPersistOrFallback()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        char unused = Enumerable.Range('D', 'Z' - 'D' + 1).Select(value => (char)value)
            .First(letter => !Directory.Exists($"{letter}:\\"));
        StorageRootException exception = Assert.Throws<StorageRootException>(() =>
            resolver.ConfigureNewRoot($"{unused}:\\MinecraftLauncher\\GameData"));
        Assert.Equal(StorageRootError.RootUnavailable, exception.Error);
        Assert.False(File.Exists(resolver.ConfigurationFile));
        Assert.False(Directory.Exists(resolver.LegacyRootDirectory));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WriteDeniedRoot_IsActionableAndNeverCreatesLegacyFallback()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        resolver.ConfigureNewRoot(directory.GameData);
        DirectoryInfo root = new(directory.GameData);
        DirectorySecurity original = root.GetAccessControl();
        DirectorySecurity denied = root.GetAccessControl();
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        denied.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.WriteData,
            AccessControlType.Deny));
        root.SetAccessControl(denied);
        try
        {
            Assert.Equal(StorageRootError.AccessDenied,
                Assert.Throws<StorageRootException>(() => resolver.OpenSession()).Error);
            Assert.False(Directory.Exists(resolver.LegacyRootDirectory));
        }
        finally
        {
            DirectorySecurity restored = new();
            restored.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
            root.SetAccessControl(restored);
        }

        using StorageRootSession recovered = resolver.OpenSession();
        Assert.Equal(directory.GameData, recovered.Paths.RootDirectory);
    }

    [Fact]
    public void ConfiguredPathOccupiedByFile_IsNotRecreatedOrReplaced()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        resolver.ConfigureNewRoot(directory.GameData);
        Directory.Delete(directory.GameData);
        File.WriteAllText(directory.GameData, "keep-file");
        Assert.Equal(StorageRootError.RootUnavailable,
            Assert.Throws<StorageRootException>(() => resolver.OpenSession()).Error);
        Assert.Equal("keep-file", File.ReadAllText(directory.GameData));
        Assert.False(Directory.Exists(resolver.LegacyRootDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":2,\"gameDataRoot\":\"C:\\\\Games\"}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"gameDataRoot\":null}")]
    [InlineData("{\"schemaVersion\":1,\"gameDataRoot\":\"relative\"}")]
    [InlineData("{\"schemaVersion\":1,\"gameDataRoot\":\"C:\\\\Games\",\"serverOverride\":true}")]
    public void InvalidPointer_NeverCreatesLegacyFallback(string json)
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        Directory.CreateDirectory(resolver.ControlDirectory);
        File.WriteAllText(resolver.ConfigurationFile, json);
        Assert.Throws<StorageRootException>(() => resolver.OpenSession());
        Assert.False(Directory.Exists(resolver.LegacyRootDirectory));
        Assert.Equal(json, File.ReadAllText(resolver.ConfigurationFile));
    }

    [Fact]
    public void OversizedPointer_IsRejectedBeforeDeserialization()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        Directory.CreateDirectory(resolver.ControlDirectory);
        File.WriteAllText(resolver.ConfigurationFile, new string(' ', 16 * 1024 + 1));
        Assert.Equal(StorageRootError.InvalidConfiguration,
            Assert.Throws<StorageRootException>(() => resolver.OpenSession()).Error);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData(@"C:relative")]
    [InlineData(@"\\server\share\GameData")]
    [InlineData(@"\\?\C:\GameData")]
    [InlineData(@"C:\folder\NUL")]
    [InlineData(@"C:\folder\name.")]
    public void UnsafeRoot_IsRejectedWithoutPointer(string root)
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        Assert.Equal(StorageRootError.UnsafePath,
            Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(root)).Error);
        Assert.False(File.Exists(resolver.ConfigurationFile));
    }

    [Fact]
    public void AppControlAndVolumeRoots_AreRejectedIncludingParentsAndDescendants()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        string[] unsafeRoots =
        [
            directory.AppDirectory, Path.Combine(directory.AppDirectory, "GameData"), directory.Path,
            resolver.ControlDirectory, Path.Combine(resolver.ControlDirectory, "GameData"),
            Path.GetPathRoot(directory.Path)!,
            Path.Combine(directory.LocalDirectory, "lDiestrol.MinecraftLauncher", "GameData"),
        ];
        foreach (string root in unsafeRoots)
        {
            Assert.Equal(StorageRootError.UnsafePath,
                Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(root)).Error);
        }
    }

    [Fact]
    public void InstalledAppRoot_WithCurrentDirectoryIsProtectedAsAWhole()
    {
        using TestDirectory directory = new();
        string installRoot = Path.Combine(directory.Path, "custom-install");
        string current = Path.Combine(installRoot, "current");
        Directory.CreateDirectory(current);
        File.WriteAllText(Path.Combine(installRoot, "Update.exe"), "stub");
        StorageRootResolver resolver = new(directory.LocalDirectory, current);
        Assert.Equal(StorageRootError.UnsafePath, Assert.Throws<StorageRootException>(() =>
            resolver.ConfigureNewRoot(Path.Combine(installRoot, "GameData"))).Error);
    }

    [Fact]
    public void ExistingDataAndRootChanges_AreNotSilentlyAdoptedOrMigrated()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        Directory.CreateDirectory(resolver.LegacyRootDirectory);
        File.WriteAllText(Path.Combine(resolver.LegacyRootDirectory, "settings.json"), "keep");
        Assert.Equal(StorageRootError.ExistingData,
            Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(directory.GameData)).Error);
        Assert.False(File.Exists(resolver.ConfigurationFile));
        resolver.ConfigureNewRoot(resolver.LegacyRootDirectory);
        resolver.ConfigureNewRoot(resolver.LegacyRootDirectory + Path.DirectorySeparatorChar);
        string original = File.ReadAllText(resolver.ConfigurationFile);
        Assert.Equal(StorageRootError.ExistingData,
            Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(directory.GameData)).Error);
        Assert.Equal(original, File.ReadAllText(resolver.ConfigurationFile));
    }

    [Fact]
    public void NonEmptyNewTarget_IsNotAdopted()
    {
        using TestDirectory directory = new();
        Directory.CreateDirectory(directory.GameData);
        File.WriteAllText(Path.Combine(directory.GameData, "user-file"), "keep");
        StorageRootResolver resolver = directory.Resolver();
        Assert.Equal(StorageRootError.ExistingData,
            Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(directory.GameData)).Error);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(directory.GameData, "user-file")));
    }

    [Fact]
    public void MultipleSessions_ShareRootAndBlockConfigurationUntilAllClose()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        resolver.ConfigureNewRoot(directory.GameData);
        using StorageRootSession first = resolver.OpenSession();
        using StorageRootSession second = directory.Resolver().OpenSession();
        Assert.Equal(first.Paths.RootDirectory, second.Paths.RootDirectory);
        Assert.Equal(StorageRootError.Busy,
            Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(directory.GameData)).Error);
        first.Dispose();
        Assert.Equal(StorageRootError.Busy,
            Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(directory.GameData)).Error);
        second.Dispose();
        resolver.ConfigureNewRoot(directory.GameData);
    }

    [Fact]
    public async Task OtherProcessSession_BlocksSelectionAndReleasesOnExit()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        resolver.ConfigureNewRoot(directory.GameData);
        string lockPath = Path.Combine(resolver.ControlDirectory, "storage-root.lock").Replace("'", "''");
        string command = $"$s = [IO.File]::Open('{lockPath}', 'Open', 'ReadWrite', 'ReadWrite'); " +
            "try { [Console]::WriteLine('ready'); [Console]::ReadLine() | Out-Null } finally { $s.Dispose() }";
        ProcessStartInfo start = new()
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
        using Process process = Process.Start(start)!;
        try
        {
            Assert.Equal("ready", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)));
            using StorageRootSession reader = resolver.OpenSession();
            Assert.Equal(StorageRootError.Busy,
                Assert.Throws<StorageRootException>(() => resolver.ConfigureNewRoot(directory.GameData)).Error);
        }
        finally
        {
            await process.StandardInput.WriteLineAsync("release");
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }

        Assert.Equal(0, process.ExitCode);
        resolver.ConfigureNewRoot(directory.GameData);
    }

    [Fact]
    public void SelectedRootRemovedAfterStartup_OperationDoesNotRecreateIt()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        resolver.ConfigureNewRoot(directory.GameData);
        using StorageRootSession session = resolver.OpenSession();
        Directory.Delete(directory.GameData);
        Assert.Equal(StorageRootError.RootUnavailable,
            Assert.Throws<StorageRootException>(() => session.Paths.GetInstanceDirectory("main")).Error);
        Assert.Throws<StorageRootException>(() =>
            new FileProfileOperationLock(session.Paths, new NullAppLogger()).TryAcquire("main", out _));
        Assert.False(Directory.Exists(directory.GameData));
    }

    [Fact]
    public async Task ResolvedSessions_KeepProfileAndSharedLockIdentity()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        resolver.ConfigureNewRoot(directory.GameData);
        using StorageRootSession first = resolver.OpenSession();
        using StorageRootSession second = directory.Resolver().OpenSession();
        FileProfileOperationLock firstLock = new(first.Paths, new NullAppLogger());
        FileProfileOperationLock secondLock = new(second.Paths, new NullAppLogger());
        Assert.True(firstLock.TryAcquire("main", out IDisposable? lease));
        using (lease)
        {
            Assert.False(secondLock.TryAcquire("main", out _));
            Assert.True(secondLock.TryAcquire("other", out IDisposable? otherLease));
            otherLease!.Dispose();
        }

        using IDisposable? shared = await new FileSharedInstallLock(first.Paths, new NullAppLogger())
            .TryAcquireAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.NotNull(shared);
        Assert.Null(await new FileSharedInstallLock(second.Paths, new NullAppLogger())
            .TryAcquireAsync(TimeSpan.Zero, CancellationToken.None));
    }

    [Fact]
    public void CmlLib_UsesSharedSelectedRootAndSeparateInstanceDirectories()
    {
        using TestDirectory directory = new();
        StorageRootResolver resolver = directory.Resolver();
        resolver.ConfigureNewRoot(directory.GameData);
        using StorageRootSession session = resolver.OpenSession();
        MinecraftPath shared = new(session.Paths.RootDirectory);
        MinecraftPath first = CmlLibGameLaunchService.CreateInstancePath(session.Paths.GetInstanceDirectory("main"), shared);
        MinecraftPath second = CmlLibGameLaunchService.CreateInstancePath(session.Paths.GetInstanceDirectory("other"), shared);
        Assert.NotEqual(first.BasePath, second.BasePath);
        Assert.Equal(shared.Assets, first.Assets);
        Assert.Equal(shared.Assets, second.Assets);
        Assert.Equal(shared.Library, first.Library);
        Assert.Equal(shared.Library, second.Library);
        Assert.Equal(shared.Versions, first.Versions);
        Assert.Equal(shared.Versions, second.Versions);
        Assert.Equal(shared.Runtime, first.Runtime);
        Assert.Equal(shared.Runtime, second.Runtime);
        Assert.Equal(shared.Resource, first.Resource);
        Assert.StartsWith(directory.GameData, shared.Runtime, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GameLaunchMapping_PreservesActionableStorageError()
    {
        StorageRootException failure = new(StorageRootError.RootUnavailable, "Подключите выбранный диск; другой каталог не выбран.");
        Assert.Equal(failure.Message, GameLaunchErrorMapper.Map(failure, Launcher.Core.Models.GameLaunchStage.Preparing).UserMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JunctionInRootAncestorOrControl_IsRejected(bool control)
    {
        using TestDirectory directory = new();
        string outside = Path.Combine(directory.Path, "outside");
        string link = Path.Combine(directory.Path, "linked");
        Directory.CreateDirectory(outside);
        ProcessStartInfo start = new()
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Arguments = $"/d /c mklink /J \"{link}\" \"{outside}\"";
        using Process process = Process.Start(start)!;
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
        try
        {
            StorageRootResolver resolver = control
                ? new StorageRootResolver(link, directory.AppDirectory)
                : directory.Resolver();
            Assert.Equal(StorageRootError.UnsafePath, Assert.Throws<StorageRootException>(() =>
                resolver.ConfigureNewRoot(control ? directory.GameData : Path.Combine(link, "GameData"))).Error);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MinecraftLauncherStorageRootTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(LocalDirectory);
        }

        public string Path { get; }
        public string LocalDirectory => System.IO.Path.Combine(Path, "local");
        public string GameData => System.IO.Path.Combine(Path, "GameData");
        public string AppDirectory => System.IO.Path.Combine(Path, "App");
        public StorageRootResolver Resolver(string appDirectory = "App") => new(LocalDirectory,
            System.IO.Path.Combine(Path, appDirectory.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
