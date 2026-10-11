using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Launcher.Infrastructure.Installation;
using Launcher.Infrastructure.Persistence;
using Launcher.Infrastructure.Logging;
using System.Diagnostics;

namespace Launcher.Core.Tests;

public sealed class InstallationSafetyTests
{
    [Fact]
    public void MissingConfiguredGameDirectoryFailsBeforeInstallationIntent()
    {
        using Sandbox box = new();
        string game = Path.Combine(box.Directory, "configured-game");
        box.Resolver.ConfigureNewRoot(game);
        Directory.Delete(game); // This fixture's own empty root only.
        Assert.Equal(InstallationError.Unavailable, Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request)).Error);
        Assert.Equal(game, box.Resolver.InspectConfiguredRoot());
        Assert.False(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
        Assert.False(Directory.Exists(box.Base));
    }

    [Fact]
    public void SdkStartupHoldsLeaseUntilCallbackCompletesWithoutCreatingLegacyData()
    {
        using Sandbox box = new();
        bool called = false;
        Assert.True(InstallationStartupGuard.TryInitializeVelopack([], box.Resolver, () =>
        {
            called = true;
            Assert.ThrowsAny<IOException>(() => box.Backend.AcquireLease());
        }));
        Assert.True(called);
        Assert.False(Directory.Exists(box.Resolver.LegacyRootDirectory));
        using IDisposable installer = box.Backend.AcquireLease();
    }

    [Fact]
    public void PendingIntentAndExclusiveLeasePreventSdkCallback()
    {
        using Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using (IDisposable lease = box.Backend.AcquireLease())
        {
            Assert.False(InstallationStartupGuard.TryInitializeVelopack([], box.Resolver, () => Assert.Fail("SDK must not run during installation")));
            box.Backend.Prepare(plan);
        }
        Assert.False(InstallationStartupGuard.TryInitializeVelopack([], box.Resolver, () => Assert.Fail("SDK must not run with pending intent")));
    }

    [Fact]
    public void ReparseInstallationTargetNeverTouchesExternalWorld()
    {
        using Sandbox box = new();
        string external = Path.Combine(box.Directory, "external");
        Directory.CreateDirectory(external);
        string world = Path.Combine(external, "world.dat");
        File.WriteAllText(world, "precious");
        Directory.CreateSymbolicLink(box.Base, external);
        Assert.ThrowsAny<Exception>(() => box.Backend.Preflight(box.Request));
        Assert.Equal("precious", File.ReadAllText(world));
        Assert.False(File.Exists(box.Resolver.ConfigurationFile));
    }

    [Fact]
    public void InstallationInsideConfiguredGameDataIsRejected()
    {
        using Sandbox box = new();
        string game = Path.Combine(box.Directory, "existing-game");
        box.Resolver.ConfigureNewRoot(game);
        File.WriteAllText(Path.Combine(game, "world.dat"), "keep");
        Assert.Equal(InstallationError.UnsafePath, Assert.Throws<InstallationException>(() =>
            box.Backend.Preflight(box.Request with { BaseDirectory = Path.Combine(game, "installer") })).Error);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(game, "world.dat")));
        Assert.Equal(game, box.Resolver.InspectConfiguredRoot());
    }

    [Fact]
    public void ControlVolumeSpaceFailureDoesNotCreateLocator()
    {
        using Sandbox box = new();
        WindowsInstallationBackend backend = new(box.LocalData, box.Application, () => "user", (bytes, _) => bytes != 64 * 1024);
        Assert.Equal(InstallationError.Space, Assert.Throws<InstallationException>(() => backend.Preflight(box.Request)).Error);
        Assert.False(Directory.Exists(box.Resolver.ControlDirectory));
        Assert.False(Directory.Exists(box.Base));
    }

    [Fact]
    public void UnknownJobContractIsNotRecovered()
    {
        using Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        string file = InstallationIntentStore.PathFor(box.Resolver.ControlDirectory);
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"jobContractVersion\":1", "\"jobContractVersion\":99", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => box.Backend.Recover(plan));
        Assert.False(InstallationStartupGuard.CanInitializeVelopack([], box.Resolver.ControlDirectory));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void PermissionFailureNeverConfiguresStorageOrRunsSetup()
    {
        using Sandbox box = new();
        WindowsInstallationBackend backend = new(box.LocalData, box.Application,
            () => throw new UnauthorizedAccessException("denied identity"), (_, _) => true);
        Assert.Throws<UnauthorizedAccessException>(() => backend.Preflight(box.Request));
        Assert.False(Directory.Exists(box.Resolver.ControlDirectory));
        Assert.False(Directory.Exists(box.Base));
    }

    [Fact]
    public void ActiveMinecraftStateBlocksInstallerEvenWithoutLauncherSession()
    {
        using Sandbox box = new();
        string game = Path.Combine(box.Base, "GameData");
        box.Resolver.ConfigureNewRoot(game);
        ProfileGameActivity activity = new(new LauncherDataPaths(game), new NullAppLogger());
        using Process current = Process.GetCurrentProcess();
        activity.WriteState("main", current.Id, current.StartTime.ToUniversalTime(), Guid.NewGuid().ToString("N"));
        Assert.Equal(StorageRootError.Busy, Assert.Throws<StorageRootException>(() => box.Backend.AcquireLease()).Error);
        Assert.True(File.Exists(box.Resolver.ConfigurationFile));
    }

    [Fact]
    public void MissingVolumeDoesNotCreateFallbackOrLocator()
    {
        using Sandbox box = new();
        string missing = Enumerable.Range('A', 26).Select(letter => $"{(char)letter}:\\").First(volume => !System.IO.Directory.Exists(volume));
        Assert.ThrowsAny<Exception>(() => box.Backend.Preflight(box.Request with { BaseDirectory = Path.Combine(missing, "MinecraftLauncher") }));
        Assert.False(File.Exists(box.Resolver.ConfigurationFile));
        Assert.False(System.IO.Directory.Exists(box.Base));
    }

    [Fact]
    public void Inspect_DoesNotCreateControlOrLegacy()
    {
        using Sandbox box = new();
        InstallationInspection inspection = box.Backend.Inspect();
        Assert.Null(inspection.ConfiguredGameData);
        Assert.False(Directory.Exists(box.Resolver.ControlDirectory));
        Assert.False(Directory.Exists(box.Resolver.LegacyRootDirectory));
    }

    [Fact]
    public void Preflight_IsReadOnlyAndProbesSelectedVolumeWithoutCreatingInstallation()
    {
        using Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        Assert.Equal(Path.Combine(box.Base, "App"), plan.AppDirectory);
        Assert.Equal(Path.Combine(box.Base, "GameData"), plan.GameDataDirectory);
        Assert.True(plan.RequiredBytes > 512L * 1024 * 1024);
        Assert.False(Directory.Exists(box.Base));
        Assert.False(Directory.Exists(box.Resolver.ControlDirectory));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData(@"\\server\share\Launcher")]
    [InlineData("//server/share/Launcher")]
    [InlineData(@"\\?\C:\Launcher")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Windows\InstallerTest")]
    public void UnsafePathsAreRejectedWithoutChangingStorage(string path)
    {
        using Sandbox box = new();
        Assert.ThrowsAny<Exception>(() => box.Backend.Preflight(box.Request with { BaseDirectory = path }));
        Assert.False(File.Exists(box.Resolver.ConfigurationFile));
    }

    [Fact]
    public void Traversal_IsRejectedEvenWhenCanonicalTargetWouldBeLocal()
    {
        using Sandbox box = new();
        Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request with { BaseDirectory = Path.Combine(box.Directory, "other", "..", "target") }));
    }

    [Theory]
    [InlineData("App")]
    [InlineData("GameData")]
    [InlineData("foreign")]
    public void ForeignFilesAreNeverAdoptedOrDeleted(string child)
    {
        using Sandbox box = new();
        string folder = Path.Combine(box.Base, child);
        Directory.CreateDirectory(folder);
        string sentinel = Path.Combine(folder, "world.dat");
        File.WriteAllText(sentinel, "precious");
        Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request));
        Assert.Equal("precious", File.ReadAllText(sentinel));
        Assert.False(File.Exists(box.Resolver.ConfigurationFile));
    }

    [Fact]
    public void LegacyDataIsPreservedAndChosenWithoutMigration()
    {
        using Sandbox box = new();
        Directory.CreateDirectory(box.Resolver.LegacyRootDirectory);
        string world = Path.Combine(box.Resolver.LegacyRootDirectory, "world.dat");
        File.WriteAllText(world, "legacy");
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        box.Backend.ConfigureStorage(plan);
        Assert.Equal(box.Resolver.LegacyRootDirectory, plan.GameDataDirectory);
        Assert.Equal("legacy", File.ReadAllText(world));
        Assert.Equal(plan.GameDataDirectory, box.Resolver.InspectConfiguredRoot());
    }

    [Fact]
    public void UnmanagedLegacyApp_IsExplicitlyRejectedInsteadOfInstallingASecondPackId()
    {
        using Sandbox box = new();
        string installed = Path.Combine(box.LocalData, WindowsInstallationBackend.PackId);
        Directory.CreateDirectory(installed);
        File.WriteAllText(Path.Combine(installed, "existing-app"), "keep");
        InstallationException error = Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request));
        Assert.Equal(InstallationError.ExistingInstallation, error.Error);
        Assert.False(Directory.Exists(box.Base));
    }

    [Fact]
    public void WrongUserAndInsufficientSpaceFailBeforeAnyConfiguration()
    {
        using Sandbox box = new();
        Assert.Equal(InstallationError.Identity, Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request with { ExpectedUserSid = "other" })).Error);
        WindowsInstallationBackend lowSpace = new(box.LocalData, box.Application, () => "user", (_, _) => false);
        Assert.Equal(InstallationError.Space, Assert.Throws<InstallationException>(() => lowSpace.Preflight(box.Request)).Error);
        Assert.False(File.Exists(box.Resolver.ConfigurationFile));
    }

    [Fact]
    public void LauncherAndSecondInstallerAreExcludedByTheSameLease()
    {
        using Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using (IDisposable lease = box.Backend.AcquireLease())
        {
            Assert.ThrowsAny<IOException>(() => box.Backend.AcquireLease());
            Assert.Equal(StorageRootError.Busy, Assert.Throws<StorageRootException>(() => box.Resolver.OpenSession()).Error);
            box.Backend.Prepare(plan);
            box.Backend.ConfigureStorage(plan);
        }
        Assert.Equal(StorageRootError.Busy, Assert.Throws<StorageRootException>(() => box.Resolver.OpenSession()).Error);
        Assert.False(InstallationStartupGuard.CanInitializeVelopack([], box.Resolver.ControlDirectory));
        Assert.False(InstallationStartupGuard.CanInitializeVelopack(["--veloapp-install", "0.6.0"], box.Resolver.ControlDirectory));
        using (IDisposable lease = box.Backend.AcquireLease()) box.Backend.Recover(plan);
        using StorageRootSession session = box.Resolver.OpenSession();
        Assert.Equal(plan.GameDataDirectory, session.Paths.RootDirectory);
        Assert.ThrowsAny<IOException>(() => box.Backend.AcquireLease());
    }

    [Fact]
    public void UnknownIntentVersionFailsClosed()
    {
        using Sandbox box = new();
        Directory.CreateDirectory(box.Resolver.ControlDirectory);
        File.WriteAllText(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory), "{\"schemaVersion\":999}");
        Assert.ThrowsAny<Exception>(() => box.Resolver.OpenSession());
        Assert.False(InstallationStartupGuard.CanInitializeVelopack([], box.Resolver.ControlDirectory));
        Assert.True(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
    }

    [Fact]
    public void CorruptPayloadAndWrongPackageIdentityAreRejected()
    {
        using Sandbox box = new();
        Assert.Equal(InstallationError.Payload, Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request with { Payload = box.Request.Payload with { SetupSha256 = new string('0', 64) } })).Error);
        Assert.Equal(InstallationError.Payload, Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request with { Payload = box.Request.Payload with { Version = "9.9.9" } })).Error);
    }

    [Fact]
    public void InstalledVerificationChecksManifestEveryAppFileUpdaterPackageAndRoot()
    {
        using Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        box.Backend.ConfigureStorage(plan);
        Assert.False(box.Backend.IsVerified(plan));
        box.CreateInstalledLayout(plan);
        Assert.True(box.Backend.IsVerified(plan));
        box.Backend.Complete(plan);
        Assert.False(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
        File.WriteAllText(Path.Combine(plan.AppDirectory, "Update.exe"), "tamper");
        Assert.False(box.Backend.IsVerified(plan));
    }

    internal sealed class Sandbox : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")), "artifacts", "install-tests", Guid.NewGuid().ToString("N"));
        public string Base => Path.Combine(Directory, "Игры Minecraft с пробелами");
        public string LocalData => Path.Combine(Directory, "UserData");
        public string Application => Path.Combine(Directory, "helper");
        public StorageRootResolver Resolver => new(LocalData, Application);
        public WindowsInstallationBackend Backend { get; }
        public InstallationRequest Request { get; }
        private const string Manifest = "<package><metadata><id>lDiestrol.MinecraftLauncher</id><version>0.6.0</version><channel>stable</channel><mainExe>MinecraftLauncher.exe</mainExe><machineArchitecture>x64</machineArchitecture></metadata></package>";
        public Sandbox()
        {
            System.IO.Directory.CreateDirectory(Directory);
            string setup = Path.Combine(Directory, "Setup.exe");
            File.WriteAllText(setup, "trusted-setup-fixture");
            string package = Path.Combine(Directory, "lDiestrol.MinecraftLauncher-0.6.0-stable-full.nupkg");
            using (ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Create))
            {
                Add(archive, "Launcher.nuspec", Manifest);
                Add(archive, "lib/app/MinecraftLauncher.exe", "main-exe-fixture");
                Add(archive, "lib/app/library.dll", "library-fixture");
            }
            Request = new(Base, "user", new(setup, Hash(setup), package, Hash(package), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("updater-fixture"))), "0.6.0", "stable"));
            Backend = new(LocalData, Application, () => "user", (_, _) => true);
        }
        public void CreateInstalledLayout(InstallationPlan plan)
        {
            string current = Path.Combine(plan.AppDirectory, "current");
            System.IO.Directory.CreateDirectory(current);
            File.WriteAllText(Path.Combine(current, "MinecraftLauncher.exe"), "main-exe-fixture");
            File.WriteAllText(Path.Combine(current, "library.dll"), "library-fixture");
            File.WriteAllText(Path.Combine(current, "sq.version"), Manifest);
            File.WriteAllText(Path.Combine(plan.AppDirectory, "Update.exe"), "updater-fixture");
            string packages = Path.Combine(plan.AppDirectory, "packages");
            System.IO.Directory.CreateDirectory(packages);
            File.Copy(Request.Payload.FullPackageFile, Path.Combine(packages, Path.GetFileName(Request.Payload.FullPackageFile)));
        }
        private static void Add(ZipArchive archive, string name, string text)
        {
            using StreamWriter writer = new(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(text);
        }
        private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        // Only the unique test sandbox owned by this fixture is removed; no user locator/data paths are used.
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
