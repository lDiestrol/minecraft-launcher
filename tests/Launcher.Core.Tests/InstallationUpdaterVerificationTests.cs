using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Launcher.Infrastructure.Installation;

namespace Launcher.Core.Tests;

public sealed class InstallationUpdaterVerificationTests
{
    [Fact]
    public void PackagedSquirrelIsVerifiedAsRootUpdaterWithoutCurrentCopy()
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = CreateLayout(box);
        using ZipArchive package = ZipFile.OpenRead(plan.Payload.FullPackageFile);
        Assert.NotNull(package.GetEntry("lib/app/Squirrel.exe"));
        Assert.True(File.Exists(Path.Combine(plan.AppDirectory, "Update.exe")));
        Assert.False(File.Exists(Path.Combine(plan.AppDirectory, "current", "Squirrel.exe")));
        Assert.True(box.Backend.IsAppVerified(plan));
        Assert.False(box.Backend.IsVerified(plan));
        box.Backend.RecordSuccessfulSetup(plan, 0, 0);
        Assert.True(box.Backend.IsVerified(plan));
        box.Backend.Complete(plan);
        Assert.False(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
        Assert.True(box.Backend.IsVerified(plan));
    }

    [Fact]
    public void CurrentSquirrelCannotSubstituteForMissingRootUpdater()
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = CreateLayout(box);
        File.Move(Path.Combine(plan.AppDirectory, "Update.exe"), Path.Combine(plan.AppDirectory, "current", "Squirrel.exe"));
        AssertRejected(box, plan);
    }

    [Theory]
    [InlineData("Updater-fixture")]
    [InlineData("short")]
    [InlineData("updater-fixture-extra")]
    [InlineData("")]
    public void ChangedRootUpdaterCannotCompleteInstallation(string content)
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = CreateLayout(box);
        File.WriteAllText(Path.Combine(plan.AppDirectory, "Update.exe"), content);
        AssertRejected(box, plan);
    }

    [Theory]
    [InlineData("Updater-fixture")]
    [InlineData("short")]
    [InlineData("updater-fixture-extra")]
    public void TrustedUpdaterHashDoesNotBypassPackageEntrySizeAndHash(string packagedUpdater)
    {
        using InstallationSafetyTests.Sandbox box = new(packagedUpdater);
        InstallationPlan plan = CreateLayout(box);
        Assert.Equal(plan.Payload.UpdateSha256, Hash(Path.Combine(plan.AppDirectory, "Update.exe")));
        AssertRejected(box, plan);
    }

    [Fact]
    public void MatchingPackageEntryDoesNotBypassTrustedUpdaterHash()
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = CreateLayout(box);
        AssertRejected(box, plan with { Payload = plan.Payload with { UpdateSha256 = new string('0', 64) } });
    }

    [Theory]
    [InlineData("MinecraftLauncher.exe", true)]
    [InlineData("MinecraftLauncher.exe", false)]
    [InlineData("library.dll", true)]
    [InlineData("library.dll", false)]
    public void OrdinaryAppFilesStillRequirePresenceAndMatchingContents(string file, bool missing)
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = CreateLayout(box);
        string path = Path.Combine(plan.AppDirectory, "current", file);
        if (missing) File.Delete(path);
        else File.WriteAllText(path, File.ReadAllText(path).ToUpperInvariant()); // Same size, different SHA-256.
        AssertRejected(box, plan);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("changed")]
    public void NestedSquirrelRemainsAnOrdinaryCurrentFile(string fault)
    {
        using InstallationSafetyTests.Sandbox box = new();
        using (ZipArchive package = ZipFile.Open(box.Request.Payload.FullPackageFile, ZipArchiveMode.Update))
        {
            using StreamWriter writer = new(package.CreateEntry("lib/app/tools/Squirrel.exe").Open(), new UTF8Encoding(false));
            writer.Write("nested-library-fixture");
        }
        InstallationRequest request = box.Request with { Payload = box.Request.Payload with { PackageSha256 = Hash(box.Request.Payload.FullPackageFile) } };
        InstallationPlan plan = CreateLayout(box, request);
        string directory = Path.Combine(plan.AppDirectory, "current", "tools");
        Directory.CreateDirectory(directory);
        if (fault != "missing") File.WriteAllText(Path.Combine(directory, "Squirrel.exe"), fault == "valid" ? "nested-library-fixture" : "Nested-library-fixture");
        if (fault == "valid")
        {
            Assert.True(box.Backend.IsAppVerified(plan));
            box.Backend.RecordSuccessfulSetup(plan, 0, 0);
            Assert.True(box.Backend.IsVerified(plan));
        }
        else AssertRejected(box, plan);
    }

    [Theory]
    [InlineData("manifest-missing")]
    [InlineData("manifest-version")]
    [InlineData("package-missing")]
    [InlineData("package-entry")]
    public void ValidUpdaterCannotHideAnIncompleteManifestOrInstalledPackage(string fault)
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = CreateLayout(box);
        string manifest = Path.Combine(plan.AppDirectory, "current", "sq.version");
        string package = Path.Combine(plan.AppDirectory, "packages", Path.GetFileName(plan.Payload.FullPackageFile));
        switch (fault)
        {
            case "manifest-missing": File.Delete(manifest); break;
            case "manifest-version": File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("0.6.0", "9.9.9", StringComparison.Ordinal)); break;
            case "package-missing": File.Delete(package); break;
            case "package-entry":
                using (ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Update))
                    archive.GetEntry("lib/app/Squirrel.exe")!.Delete();
                break;
        }
        AssertRejected(box, plan);
    }

    [Theory]
    [InlineData("Update.exe")]
    [InlineData("App")]
    [InlineData("current")]
    [InlineData("packages")]
    public void UpdaterOrAncestorReparsePointsAreRejectedWithoutChangingTheirTargets(string linkedPath)
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = CreateLayout(box);
        string path = linkedPath == "App" ? plan.AppDirectory : Path.Combine(plan.AppDirectory, linkedPath);
        string external = Path.Combine(box.Directory, "external-" + linkedPath);
        bool file = linkedPath == "Update.exe";
        if (file)
        {
            File.Move(path, external);
            File.CreateSymbolicLink(path, external);
        }
        else
        {
            Directory.Move(path, external);
            Directory.CreateSymbolicLink(path, external);
        }
        string updater = linkedPath == "App" ? Path.Combine(external, "Update.exe") : file ? external : Path.Combine(plan.AppDirectory, "Update.exe");
        string hash = Hash(updater);
        AssertRejected(box, plan);
        Assert.Equal(hash, Hash(updater));
    }

    [Theory]
    [InlineData("lib/app/../Squirrel.exe")]
    [InlineData("lib/app/tools/../../Squirrel.exe")]
    public void TraversalCannotBeTreatedAsTheUpdaterSpecialCase(string name)
    {
        using InstallationSafetyTests.Sandbox box = new();
        using (ZipArchive package = ZipFile.Open(box.Request.Payload.FullPackageFile, ZipArchiveMode.Update))
        {
            using StreamWriter writer = new(package.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write("updater-fixture");
        }
        InstallationRequest request = box.Request with { Payload = box.Request.Payload with { PackageSha256 = Hash(box.Request.Payload.FullPackageFile) } };
        Assert.Equal(InstallationError.Payload, Assert.Throws<InstallationException>(() => box.Backend.Preflight(request)).Error);
        Assert.False(Directory.Exists(box.Base));
        Assert.False(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
    }

    // Only synthetic files and injected registration; native Setup is never executed.
    private static InstallationPlan CreateLayout(InstallationSafetyTests.Sandbox box, InstallationRequest? request = null)
    {
        InstallationPlan plan = box.Backend.Preflight(request ?? box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        box.Backend.ConfigureStorage(plan);
        box.CreateInstalledLayout(plan);
        box.Registration = InstallationSafetyTests.Sandbox.ValidRegistration(plan);
        return plan;
    }

    private static void AssertRejected(InstallationSafetyTests.Sandbox box, InstallationPlan plan)
    {
        Assert.False(box.Backend.IsAppVerified(plan));
        Assert.False(box.Backend.IsVerified(plan));
        Assert.Equal(InstallationError.Verification, Assert.Throws<InstallationException>(() => box.Backend.RecordSuccessfulSetup(plan, 0, 0)).Error);
        Assert.Equal(InstallationError.Verification, Assert.Throws<InstallationException>(() => box.Backend.Complete(plan)).Error);
        Assert.True(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
        Assert.False(Directory.Exists(Path.Combine(box.Resolver.ControlDirectory, "installation-completion")));
    }

    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
}
