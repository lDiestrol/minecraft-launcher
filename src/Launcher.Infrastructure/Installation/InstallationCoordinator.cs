using Launcher.Infrastructure.Persistence;

namespace Launcher.Infrastructure.Installation;

/// <summary>API for a future native GUI's out-of-process managed helper. No UI or automatic launch.</summary>
public sealed class InstallationCoordinator
{
    private readonly IInstallationBackend _backend;

    public InstallationCoordinator() : this(new WindowsInstallationBackend()) { }

    internal InstallationCoordinator(IInstallationBackend backend) => _backend = backend;

    public InstallationInspection Inspect() => _backend.Inspect();

    public string SuggestBaseDirectory(string localVolumeRoot) =>
        Path.Combine(StoragePathPolicy.NormalizeDirectory(localVolumeRoot), "MinecraftLauncher");

    public InstallationPreflightResult Preflight(InstallationRequest request)
    {
        InstallationPlan plan = _backend.Preflight(request);
        return new(plan.AppDirectory, plan.GameDataDirectory, plan.StagingDirectory, plan.RequiredBytes);
    }

    public async Task<InstallationResult> InstallAsync(InstallationRequest request,
        IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Report(InstallationStage.Preflight, "Проверка путей, прав, пакета и свободного места.");
        _backend.Preflight(request);
        using IDisposable lease = _backend.AcquireLease();
        // Recheck under the same lease used by Launcher/guardian; preflight alone is not authorization.
        InstallationPlan plan = _backend.Preflight(request);
        _backend.Recover(plan);
        cancellationToken.ThrowIfCancellationRequested();
        if (_backend.IsVerified(plan))
        {
            _backend.Complete(plan);
            Report(InstallationStage.Completed, "Установка уже проверена; игровые данные сохранены.");
            return new(plan.AppDirectory, plan.GameDataDirectory, true);
        }

        _backend.Prepare(plan);
        Report(InstallationStage.Storage, "Сохранение каталога игровых данных без переноса.");
        _backend.ConfigureStorage(plan);
        cancellationToken.ThrowIfCancellationRequested();
        Report(InstallationStage.Setup, "Установка приложения.");
        await _backend.RunSetupAsync(plan, cancellationToken).ConfigureAwait(false);
        Report(InstallationStage.Verification, "Проверка установленного приложения.");
        if (!_backend.IsVerified(plan))
        {
            throw new InstallationException(InstallationError.Verification,
                "Установленное приложение не прошло проверку. Игровые данные и указатель сохранены; требуется безопасная повторная попытка.");
        }

        _backend.Complete(plan);
        Report(InstallationStage.Completed, "Установка завершена. Игровые данные размещены отдельно от приложения.");
        return new(plan.AppDirectory, plan.GameDataDirectory, false);

        void Report(InstallationStage stage, string message) => progress?.Report(new(stage, message));
    }
}
