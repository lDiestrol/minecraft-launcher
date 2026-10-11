namespace Launcher.Infrastructure.Installation;

// These values must come from the trusted, locally shipped bootstrapper catalog, never a server profile.
public sealed record InstallationPayload(string SetupFile, string SetupSha256, string FullPackageFile,
    string PackageSha256, string UpdateSha256, string Version, string Channel);

public sealed record InstallationRequest(string BaseDirectory, string ExpectedUserSid, InstallationPayload Payload);

public enum InstallationStage { Preflight, Storage, Setup, Verification, Completed }

public sealed record InstallationProgress(InstallationStage Stage, string Message);

public sealed record InstallationResult(string AppDirectory, string GameDataDirectory, bool AlreadyInstalled);

public sealed record InstallationPreflightResult(string AppDirectory, string GameDataDirectory,
    string StagingDirectory, long RequiredBytes);

public sealed record InstallationInspection(string UserSid, string? ConfiguredGameData, bool HasLegacyData,
    bool HasLegacyApp, bool HasPendingInstallation);

public enum InstallationError { UnsafePath, Identity, Busy, ExistingInstallation, Payload, Space, Unavailable, Setup, Verification, Recovery }

public sealed class InstallationException(InstallationError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public InstallationError Error { get; } = error;
}

internal sealed record InstallationPlan(string BaseDirectory, string AppDirectory, string GameDataDirectory,
    string StagingDirectory, long RequiredBytes, InstallationPayload Payload);

internal interface IInstallationBackend
{
    InstallationInspection Inspect();
    InstallationPlan Preflight(InstallationRequest request);
    IDisposable AcquireLease();
    void Recover(InstallationPlan plan);
    void Prepare(InstallationPlan plan);
    void ConfigureStorage(InstallationPlan plan);
    Task RunSetupAsync(InstallationPlan plan, CancellationToken cancellationToken);
    bool IsVerified(InstallationPlan plan);
    void Complete(InstallationPlan plan);
}
