using Launcher.Infrastructure.Installation;

namespace Launcher.Core.Tests;

public sealed class InstallationReviewRegressionTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("partial")]
    [InlineData("path")]
    [InlineData("version")]
    [InlineData("product")]
    [InlineData("name")]
    [InlineData("command")]
    [InlineData("publisher")]
    public void CompleteFilesAndReceiptDoNotProveRegistrationOrSetupCompletion(string fault)
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        box.Backend.ConfigureStorage(plan);
        box.CreateInstalledLayout(plan);
        box.Registration = FaultyRegistration(plan, fault);
        Assert.True(box.Backend.IsAppVerified(plan));
        Assert.False(box.Backend.IsRegistrationVerified(plan));
        Assert.False(box.Backend.IsVerified(plan));
        Assert.Throws<InstallationException>(() => box.Backend.RecordSuccessfulSetup(plan, 0, 0));
        Assert.Throws<InstallationException>(() => box.Backend.Complete(plan));
        Assert.True(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
        Assert.False(Directory.Exists(Path.Combine(box.Resolver.ControlDirectory, "installation-completion")));
    }

    [Fact]
    public void ValidRegistrationStillRequiresSuccessfulSetupAndInactiveTree()
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        box.Backend.ConfigureStorage(plan);
        box.CreateInstalledLayout(plan);
        box.Registration = InstallationSafetyTests.Sandbox.ValidRegistration(plan);
        Assert.True(box.Backend.IsRegistrationVerified(plan));
        Assert.False(box.Backend.IsVerified(plan));
        Assert.Throws<InstallationException>(() => box.Backend.RecordSuccessfulSetup(plan, 1, 0));
        Assert.Throws<InstallationException>(() => box.Backend.RecordSuccessfulSetup(plan, 0, 1));
        box.Backend.RecordSuccessfulSetup(plan, 0, 0);
        Assert.True(box.Backend.IsVerified(plan));
        box.Backend.Complete(plan);
        Assert.True(box.Backend.IsVerified(plan)); // No shortcuts in fixture: they are not mandatory evidence.
        box.Backend.Prepare(plan); // A new intent must not reuse the old operation's completion.
        Assert.False(box.Backend.IsVerified(plan));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteInstallationRetriesSetupAndRepeatedRegistrationFailureNeverCompletes(bool registrationFails)
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using (IDisposable lease = box.Backend.AcquireLease())
        {
            box.Backend.Prepare(plan);
            box.Backend.ConfigureStorage(plan);
            box.CreateInstalledLayout(plan);
        }
        PublicationFixture backend = new(box) { RegistrationFails = registrationFails };
        InstallationCoordinator coordinator = new(backend);
        if (registrationFails)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.Equal(InstallationError.Verification, (await Assert.ThrowsAsync<InstallationException>(() => coordinator.InstallAsync(box.Request))).Error);
                Assert.True(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
                Assert.False(InstallationStartupGuard.CanInitializeVelopack([], box.Resolver.ControlDirectory));
            }
            Assert.Equal(2, backend.SetupCalls);
        }
        else
        {
            Assert.False((await coordinator.InstallAsync(box.Request)).AlreadyInstalled);
            Assert.Equal(1, backend.SetupCalls);
            Assert.True((await coordinator.InstallAsync(box.Request)).AlreadyInstalled);
            Assert.Equal(1, backend.SetupCalls);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkedOperationPreservesRollbackAndForeignSiblingsDuringRetry(bool completedBeforeCleanupFailure)
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using (IDisposable lease = box.Backend.AcquireLease())
        {
            box.Backend.Prepare(plan);
            box.Backend.ConfigureStorage(plan);
            box.CreateInstalledLayout(plan);
            if (completedBeforeCleanupFailure)
            {
                box.ConfirmSuccessfulSetup(plan);
                box.Backend.Complete(plan);
            }
            else Directory.Move(plan.AppDirectory, plan.AppDirectory + ".abcdefghijklmnop"); // Setup161 rename crash fixture.
        }
        string backup = plan.AppDirectory + ".abcdefghijklmnop";
        Directory.CreateDirectory(backup);
        string backupSentinel = Path.Combine(backup, "keep.dat");
        File.WriteAllText(backupSentinel, "backup");
        string foreign = Path.Combine(box.Base, "foreign");
        Directory.CreateDirectory(foreign);
        File.WriteAllText(Path.Combine(foreign, "keep.dat"), "foreign");
        string world = Path.Combine(plan.GameDataDirectory, "world.dat");
        File.WriteAllText(world, "world");
        byte[] locator = File.ReadAllBytes(box.Resolver.ConfigurationFile);
        PublicationFixture backend = new(box);
        InstallationResult result = await new InstallationCoordinator(backend).InstallAsync(box.Request);
        Assert.Equal(completedBeforeCleanupFailure, result.AlreadyInstalled);
        Assert.Equal(completedBeforeCleanupFailure ? 0 : 1, backend.SetupCalls);
        Assert.Equal("backup", File.ReadAllText(backupSentinel));
        Assert.Equal("foreign", File.ReadAllText(Path.Combine(foreign, "keep.dat")));
        Assert.Equal("world", File.ReadAllText(world));
        Assert.Equal(locator, File.ReadAllBytes(box.Resolver.ConfigurationFile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConflictingOrMissingReceiptCannotAuthorizeRecovery(bool missing)
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        string receipt = Path.Combine(box.Resolver.ControlDirectory, "installation-record");
        if (missing) InstallationIntentStore.Delete(receipt);
        else InstallationIntentStore.Write(receipt, InstallationIntentStore.Read(receipt)! with { Token = Guid.NewGuid().ToString("N") });
        Assert.Equal(InstallationError.Recovery, Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request)).Error);
        Assert.Equal(InstallationError.Recovery, Assert.Throws<InstallationException>(() => box.Backend.Recover(plan)).Error);
        Assert.True(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
    }

    [Fact]
    public void AppSuffixAloneDoesNotAuthorizeFreshInstall()
    {
        using InstallationSafetyTests.Sandbox box = new();
        string backup = Path.Combine(box.Base, "App.abcdefghijklmnop");
        Directory.CreateDirectory(backup);
        string sentinel = Path.Combine(backup, "world.dat");
        File.WriteAllText(sentinel, "keep");
        Assert.Equal(InstallationError.ExistingInstallation, Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request)).Error);
        Assert.Equal("keep", File.ReadAllText(sentinel));
        Assert.False(File.Exists(box.Resolver.ConfigurationFile));
    }

    [Fact]
    public void OwnedRetryDoesNotFollowForeignSiblingLink()
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        box.Backend.ConfigureStorage(plan);
        string external = Path.Combine(box.Directory, "external");
        Directory.CreateDirectory(external);
        string sentinel = Path.Combine(external, "world.dat");
        File.WriteAllText(sentinel, "keep");
        Directory.CreateSymbolicLink(Path.Combine(box.Base, "App.unknown"), external);
        Assert.ThrowsAny<Exception>(() => box.Backend.Preflight(box.Request));
        Assert.Equal("keep", File.ReadAllText(sentinel));
    }

    [Fact]
    public void ReceiptWithoutLinkedIntentOrCompletionCannotRelaxForeignContentCheck()
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        InstallationIntentStore.Delete(box.Resolver.ControlDirectory); // Old incomplete metadata fixture, never production cleanup.
        Directory.CreateDirectory(Path.Combine(box.Base, "App.unknown"));
        Assert.Equal(InstallationError.ExistingInstallation, Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request)).Error);
    }

    [Fact]
    public void RegistrationForAnotherAppCannotBeOverwrittenEvenWithOwnedReceipt()
    {
        using InstallationSafetyTests.Sandbox box = new();
        InstallationPlan plan = box.Backend.Preflight(box.Request);
        using IDisposable lease = box.Backend.AcquireLease();
        box.Backend.Prepare(plan);
        box.Backend.ConfigureStorage(plan);
        InstallationRegistration? foreign = FaultyRegistration(plan, "path");
        WindowsInstallationBackend backend = new(box.LocalData, box.Application, () => "user", (_, _) => true,
            inspectRegistration: true, registration: () => foreign);
        Assert.Equal(InstallationError.ExistingInstallation, Assert.Throws<InstallationException>(() => backend.Preflight(box.Request)).Error);
        Assert.True(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
    }

    [Fact]
    public async Task RootChangeBetweenPreflightAndLeaseIsRecheckedBeforeIntent()
    {
        using InstallationSafetyTests.Sandbox box = new();
        PublicationFixture backend = new(box)
        {
            AfterFirstPreflight = () => box.Resolver.ConfigureNewRoot(Path.Combine(box.Base, ".launcher-install")),
        };
        Assert.Equal(InstallationError.UnsafePath, (await Assert.ThrowsAsync<InstallationException>(() =>
            new InstallationCoordinator(backend).InstallAsync(box.Request))).Error);
        Assert.Equal(0, backend.SetupCalls);
        Assert.False(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
        Assert.Equal(Path.Combine(box.Base, ".launcher-install"), box.Resolver.InspectConfiguredRoot());
    }

    [Theory]
    [InlineData("equal")]
    [InlineData("game-inside")]
    [InlineData("staging-inside")]
    public void StagingOverlapIsRejectedWithoutChangingLocatorIntentOrWorld(string relationship)
    {
        using InstallationSafetyTests.Sandbox box = new();
        string staging = Path.Combine(box.Base, ".launcher-install");
        string game = relationship switch { "game-inside" => Path.Combine(staging, "GameData"), "staging-inside" => box.Base, _ => staging };
        box.Resolver.ConfigureNewRoot(game);
        string world = Path.Combine(game, "world.dat");
        File.WriteAllText(world, "keep");
        byte[] locator = File.ReadAllBytes(box.Resolver.ConfigurationFile);
        Assert.Equal(InstallationError.UnsafePath, Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request)).Error);
        using (IDisposable lease = box.Backend.AcquireLease())
            Assert.Equal(InstallationError.UnsafePath, Assert.Throws<InstallationException>(() => box.Backend.Preflight(box.Request)).Error);
        Assert.Equal(locator, File.ReadAllBytes(box.Resolver.ConfigurationFile));
        Assert.Equal("keep", File.ReadAllText(world));
        Assert.False(File.Exists(InstallationIntentStore.PathFor(box.Resolver.ControlDirectory)));
    }

    private static InstallationRegistration? FaultyRegistration(InstallationPlan plan, string fault)
    {
        if (fault == "missing") return null;
        InstallationRegistration valid = InstallationSafetyTests.Sandbox.ValidRegistration(plan);
        Dictionary<string, object?> values = new(valid.Values);
        switch (fault)
        {
            case "partial": values.Remove("Language"); break;
            case "path": values["InstallLocation"] = Path.Combine(plan.BaseDirectory, "other-App"); break;
            case "version": values["DisplayVersion"] = "9.9.9"; break;
            case "name": values["DisplayName"] = "Other Product"; break;
            case "command": values["UninstallString"] = "other.exe --uninstall"; break;
            case "publisher": values.Remove("Publisher"); break;
        }
        return new(fault == "product" ? "other-product" : valid.ProductId, values);
    }

    // Actual filesystem/intent/verification, simulated publication. Does not execute native vendor Setup.
    private sealed class PublicationFixture(InstallationSafetyTests.Sandbox box) : IInstallationBackend
    {
        public bool RegistrationFails { get; init; }
        public Action? AfterFirstPreflight { get; init; }
        public int SetupCalls { get; private set; }
        private int _preflights;
        public InstallationInspection Inspect() => box.Backend.Inspect();
        public InstallationPlan Preflight(InstallationRequest request)
        {
            InstallationPlan plan = box.Backend.Preflight(request);
            if (++_preflights == 1) AfterFirstPreflight?.Invoke();
            return plan;
        }
        public IDisposable AcquireLease() => box.Backend.AcquireLease();
        public void Recover(InstallationPlan plan) => box.Backend.Recover(plan);
        public void Prepare(InstallationPlan plan) => box.Backend.Prepare(plan);
        public void ConfigureStorage(InstallationPlan plan) => box.Backend.ConfigureStorage(plan);
        public bool IsVerified(InstallationPlan plan) => box.Backend.IsVerified(plan);
        public void Complete(InstallationPlan plan) => box.Backend.Complete(plan);
        public Task RunSetupAsync(InstallationPlan plan, CancellationToken cancellationToken)
        {
            SetupCalls++;
            Assert.ThrowsAny<IOException>(() => box.Backend.AcquireLease());
            box.CreateInstalledLayout(plan);
            box.Registration = RegistrationFails ? null : InstallationSafetyTests.Sandbox.ValidRegistration(plan);
            box.Backend.RecordSuccessfulSetup(plan, 0, 0);
            return Task.CompletedTask;
        }
    }
}
