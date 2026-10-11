using Launcher.Infrastructure.Installation;

namespace Launcher.Core.Tests;

public sealed class InstallationCoordinatorTests
{
    [Theory]
    [InlineData(@"C:\MinecraftLauncher")]
    [InlineData(@"D:\Игры Minecraft\Launcher")]
    public async Task Install_OrdersStagesAndHoldsLeaseAcrossSetupAndVerification(string root)
    {
        FakeBackend backend = new(root);
        InstallationResult result = await new InstallationCoordinator(backend).InstallAsync(backend.Request);
        Assert.Equal(new[] { "preflight", "lease", "preflight", "recover", "verify", "prepare", "storage", "setup", "verify", "complete", "release" }, backend.Events);
        Assert.Equal(Path.Combine(root, "GameData"), result.GameDataDirectory);
        Assert.False(result.AlreadyInstalled);
        Assert.False(backend.Leased);
    }

    [Fact]
    public async Task IdempotentVerifiedInstallation_DoesNotCallSetupOrChangeStorage()
    {
        FakeBackend backend = new() { Verified = true };
        InstallationResult result = await new InstallationCoordinator(backend).InstallAsync(backend.Request);
        Assert.True(result.AlreadyInstalled);
        Assert.DoesNotContain("prepare", backend.Events);
        Assert.DoesNotContain("storage", backend.Events);
        Assert.DoesNotContain("setup", backend.Events);
    }

    [Theory]
    [InlineData("preflight")]
    [InlineData("recover")]
    [InlineData("prepare")]
    [InlineData("storage")]
    [InlineData("setup")]
    [InlineData("complete")]
    public async Task FailureAtEachStage_DoesNotReportSuccessOrPerformDestructiveRollback(string failure)
    {
        FakeBackend backend = new() { Failure = failure };
        await Assert.ThrowsAsync<IOException>(() => new InstallationCoordinator(backend).InstallAsync(backend.Request));
        Assert.False(backend.Leased);
        Assert.DoesNotContain("delete", backend.Events);
        Assert.DoesNotContain("rollback", backend.Events);
    }

    [Fact]
    public async Task ExitZeroWithoutVerifiedLayout_IsNotSuccess()
    {
        FakeBackend backend = new() { VerifyAfterSetup = false };
        InstallationException error = await Assert.ThrowsAsync<InstallationException>(() => new InstallationCoordinator(backend).InstallAsync(backend.Request));
        Assert.Equal(InstallationError.Verification, error.Error);
        Assert.DoesNotContain("complete", backend.Events);
        Assert.True(backend.StorageConfigured);
        Assert.False(backend.Leased);
    }

    [Fact]
    public async Task CancellationBeforeStart_HasNoSideEffects()
    {
        FakeBackend backend = new();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new InstallationCoordinator(backend).InstallAsync(backend.Request, cancellationToken: new(true)));
        Assert.Empty(backend.Events);
    }

    [Fact]
    public async Task SetupCancellation_KeepsConfiguredStorageAndReleasesCoordinatorLease()
    {
        FakeBackend backend = new() { CancelSetup = true };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new InstallationCoordinator(backend).InstallAsync(backend.Request));
        Assert.True(backend.StorageConfigured);
        Assert.False(backend.Leased);
        Assert.DoesNotContain("complete", backend.Events);
    }

    [Fact]
    public async Task HangingSetup_RespondsToCallerCancellationWithoutRollback()
    {
        FakeBackend backend = new() { HangSetup = true };
        using CancellationTokenSource cancellation = new();
        Task<InstallationResult> installation = new InstallationCoordinator(backend).InstallAsync(backend.Request, cancellationToken: cancellation.Token);
        Assert.True(backend.Leased);
        Assert.True(backend.StorageConfigured);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => installation.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.False(backend.Leased);
        Assert.DoesNotContain("complete", backend.Events);
        Assert.DoesNotContain("rollback", backend.Events);
    }

    private sealed class FakeBackend(string root = @"C:\MinecraftLauncher") : IInstallationBackend
    {
        public List<string> Events { get; } = [];
        public bool Leased, Verified, StorageConfigured, CancelSetup, HangSetup;
        public bool VerifyAfterSetup = true;
        public string? Failure;
        public InstallationRequest Request { get; } = new(root, "test-user", new("setup", "hash", "package", "hash", "hash", "0.6.0", "stable"));
        public InstallationInspection Inspect() => new("test-user", null, false, false, false);
        public InstallationPlan Preflight(InstallationRequest request)
        {
            Step("preflight");
            return new(root, Path.Combine(root, "App"), Path.Combine(root, "GameData"), Path.Combine(root, "staging"), 1, Request.Payload);
        }
        public IDisposable AcquireLease() { Step("lease"); Leased = true; return new Lease(this); }
        public void Recover(InstallationPlan plan) => Inside("recover");
        public void Prepare(InstallationPlan plan) => Inside("prepare");
        public void ConfigureStorage(InstallationPlan plan) { Inside("storage"); StorageConfigured = true; }
        public Task RunSetupAsync(InstallationPlan plan, CancellationToken token)
        {
            Inside("setup");
            if (CancelSetup) throw new OperationCanceledException();
            if (HangSetup) return Task.Delay(Timeout.Infinite, token);
            Verified = VerifyAfterSetup;
            return Task.CompletedTask;
        }
        public bool IsVerified(InstallationPlan plan) { Inside("verify"); return Verified; }
        public void Complete(InstallationPlan plan) => Inside("complete");
        private void Inside(string name) { Assert.True(Leased); Step(name); }
        private void Step(string name) { Events.Add(name); if (Failure == name) throw new IOException(name); }
        private sealed class Lease(FakeBackend owner) : IDisposable
        {
            public void Dispose() { owner.Events.Add("release"); owner.Leased = false; }
        }
    }
}
