using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Xml;
using System.Xml.Linq;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Infrastructure.Installation;

internal sealed class WindowsInstallationBackend : IInstallationBackend
{
    internal const string PackId = "lDiestrol.MinecraftLauncher";
    internal const string MainExe = "MinecraftLauncher.exe";
    private readonly string _localData;
    private readonly string _executionDirectory;
    private readonly Func<long, string, bool> _hasSpace;
    private readonly Func<string> _identity;
    private readonly bool _inspectRegistration;
    private readonly InstallationProduct _product;
    private readonly Func<InstallationRegistration?> _registration;

    public WindowsInstallationBackend() : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppContext.BaseDirectory, CurrentUserSid, (bytes, volume) => new DriveInfo(volume).AvailableFreeSpace >= bytes, true)
    { }

    internal WindowsInstallationBackend(string localData, string executionDirectory, Func<string> identity,
        Func<long, string, bool> hasSpace, bool inspectRegistration = false,
        Func<InstallationRegistration?>? registration = null, InstallationProduct? product = null)
    {
        _localData = localData;
        _executionDirectory = executionDirectory;
        _identity = identity;
        _hasSpace = hasSpace;
        _inspectRegistration = inspectRegistration;
        _product = product ?? InstallationProduct.Launcher;
        _registration = registration ?? (() => InstallationRegistration.Read(_product.PackId));
    }

    private StorageRootResolver Resolver(string? app = null) => new(_localData, app ?? _executionDirectory);

    public InstallationInspection Inspect()
    {
        string sid = _identity(); // Token check precedes ANY per-user filesystem side effects.
        StorageRootResolver resolver = Resolver();
        return new(sid, resolver.InspectConfiguredRoot(), HasData(resolver.LegacyRootDirectory),
            HasData(Path.Combine(_localData, _product.PackId)) || RegisteredApp() is not null,
            InstallationIntentStore.Read(resolver.ControlDirectory) is not null);
    }

    public InstallationPlan Preflight(InstallationRequest request)
    {
        InstallationInspection inspection = Inspect();
        if (request.ExpectedUserSid != inspection.UserSid)
            throw Error(InstallationError.Identity, "Установщик и помощник запущены под разными пользователями.");
        string root = Normalize(request.BaseDirectory);
        string app = Path.Combine(root, "App");
        string staging = Normalize(Path.Combine(root, ".launcher-install"));
        string game = inspection.ConfiguredGameData ?? (inspection.HasLegacyData ? Resolver().LegacyRootDirectory : Path.Combine(root, "GameData"));
        game = Normalize(game);
        if (File.Exists(game) || inspection.ConfiguredGameData is not null && !Directory.Exists(game))
            throw Error(InstallationError.Unavailable, "Сохранённый каталог игровых данных недоступен. Подключите диск и восстановите доступ; другой каталог не выбран.");
        RejectOverlap(app, game);
        RejectOverlap(staging, game);
        RejectOverlap(root, Resolver().ControlDirectory);
        foreach (Environment.SpecialFolder folder in new[] { Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonApplicationData })
        {
            string system = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(system)) RejectOverlap(root, system);
        }
        if (StoragePathPolicy.IsWithinOrEqual(root, Path.GetTempPath()))
            throw Error(InstallationError.UnsafePath, "Постоянная установка не допускается во временном каталоге.");
        if (StoragePathPolicy.IsWithinOrEqual(_executionDirectory, app))
            throw Error(InstallationError.UnsafePath, "Помощник установки должен находиться вне заменяемого App.");
        string volume = Path.GetPathRoot(root)!;
        DriveInfo drive = new(volume);
        if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
            throw Error(InstallationError.Unavailable, "Выбранный локальный том недоступен.");
        if (inspection.ConfiguredGameData is null && !inspection.HasLegacyData &&
            !volume.Equals(Path.GetPathRoot(game), StringComparison.OrdinalIgnoreCase))
            throw Error(InstallationError.UnsafePath, "App и новые игровые данные должны находиться на одном томе.");
        InstallationIntent? receipt = ReadReceipt();
        string? registeredApp = RegisteredApp();
        if (!string.IsNullOrEmpty(registeredApp) && !Same(Normalize(registeredApp), app))
            throw Error(InstallationError.ExistingInstallation, "HKCU указывает на другую установку этого продукта. Чужая регистрация и App не заменяются.");
        InstallationIntent? pending = InstallationIntentStore.Read(Resolver().ControlDirectory);
        InstallationIntent? completion = ReadCompletion();
        bool linkedOperation = receipt is not null && (SameOperation(receipt, pending) || SameOperation(receipt, completion));
        if (pending is not null && !SameOperation(receipt, pending))
            throw Error(InstallationError.Recovery, "Intent и запись владения не совпадают. Данные сохранены; требуется проверка служебных записей без удаления App или backup.");
        if (inspection.HasLegacyApp && receipt is null)
            throw Error(InstallationError.ExistingInstallation, "Обнаружена существующая установка без установочной записи. Её данные сохранены; создание второй установки или автоматический перенос запрещены.");
        if (receipt is not null && (!Same(receipt.AppDirectory, app) || !Same(receipt.GameDataDirectory, game)))
            throw Error(InstallationError.ExistingInstallation, "Другая установка уже зарегистрирована. Перенос каталога не поддерживается.");
        if (HasData(app) && receipt is null)
            throw Error(InstallationError.ExistingInstallation, "Непустой App не принадлежит этой установке; его содержимое не будет заменено.");
        if (Directory.Exists(root))
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(root))
            {
                StoragePathPolicy.EnsureNoReparsePoints(entry);
                if (!Same(entry, app) && !Same(entry, game) && !Same(entry, staging) && !linkedOperation)
                    throw Error(InstallationError.ExistingInstallation, "Выбранная папка содержит посторонние файлы. Неизвестные каталоги, включая App.*, не принимаются и не удаляются.");
                // An authenticated retry may leave vendor backups/foreign siblings alone. Setup only owns App.
            }
        }
        if (inspection.ConfiguredGameData is null && !inspection.HasLegacyData && HasData(game))
            throw Error(InstallationError.ExistingInstallation, "Непустой GameData не будет автоматически принят или перенесён.");
        long extracted = ValidatePayload(request.Payload, app, game);
        long required = checked(new FileInfo(request.Payload.SetupFile).Length + new FileInfo(request.Payload.FullPackageFile).Length * 2 + extracted * 2 + 512L * 1024 * 1024);
        if (!_hasSpace(required, volume))
            throw Error(InstallationError.Space, "Недостаточно места для пакета, распаковки и резервного пространства на выбранном томе.");
        if (!_hasSpace(64 * 1024, Path.GetPathRoot(Resolver().ControlDirectory)!))
            throw Error(InstallationError.Space, "Нет места для небольшого служебного указателя в профиле пользователя.");
        ProbeNearest(Resolver().ControlDirectory);
        ProbeNearest(root);
        ProbeNearest(game);
        foreach (string path in new[] { app, Path.Combine(app, "packages"), Path.Combine(app, "packages", "VelopackTemp") }) ProbeNearest(path);
        return new(root, app, game, staging, required, request.Payload);
    }

    public IDisposable AcquireLease() => Resolver().AcquireInstallationLease();

    public void Recover(InstallationPlan plan)
    {
        InstallationIntent? state = InstallationIntentStore.Read(Resolver().ControlDirectory);
        if (state is null) return;
        if (!SameOperation(ReadReceipt(), state))
            throw Error(InstallationError.Recovery, "Незавершённая операция не подтверждена записью владения; автоматическое восстановление запрещено.");
        if (!Same(state.AppDirectory, plan.AppDirectory) || !Same(state.GameDataDirectory, plan.GameDataDirectory) ||
            !state.PackageSha256.Equals(plan.Payload.PackageSha256, StringComparison.OrdinalIgnoreCase))
            throw Error(InstallationError.Recovery, "Незавершённая установка относится к другому пути или пакету. Автоматический откат запрещён.");
        using Process current = Process.GetCurrentProcess();
        bool sameOwner = current.Id == state.OwnerId && current.StartTime.ToUniversalTime().Ticks == state.OwnerStartTicks;
        if (!sameOwner && !InstallationIntentStore.OwnerFinished(state))
            throw Error(InstallationError.Busy, "Другой установщик ещё работает.");
        if (WindowsInstallationJob.Query(WindowsInstallationJob.NameFor(state.Token)) > 0)
            throw Error(InstallationError.Busy, "Установочные процессы ещё работают. Повторите проверку после их завершения.");
        // Missing job is safe ONLY for install contract v1: atomic JOB_LIST + KILL_ON_JOB_CLOSE.
        // This rule must never be applied to the Minecraft job / JVM witness contract.
        // Keep the gate and operation identity until verified completion, or atomically replace it in Prepare.
    }

    public void Prepare(InstallationPlan plan)
    {
        RejectOverlap(plan.StagingDirectory, plan.GameDataDirectory);
        StoragePathPolicy.EnsureNoReparsePoints(plan.StagingDirectory);
        using Process owner = Process.GetCurrentProcess();
        InstallationIntent state = new(1, 1, owner.Id, owner.StartTime.ToUniversalTime().Ticks,
            Guid.NewGuid().ToString("N"), plan.AppDirectory, plan.GameDataDirectory, plan.Payload.PackageSha256);
        InstallationIntentStore.Write(Resolver().ControlDirectory, state);
        WriteReceipt(state);
        StoragePathPolicy.EnsureNoReparsePoints(plan.StagingDirectory);
        Directory.CreateDirectory(plan.StagingDirectory);
    }

    public void ConfigureStorage(InstallationPlan plan) => Resolver(plan.AppDirectory).ConfigureNewRootUnderLease(plan.GameDataDirectory);

    public async Task RunSetupAsync(InstallationPlan plan, CancellationToken cancellationToken)
    {
        InstallationIntent state = InstallationIntentStore.Read(Resolver().ControlDirectory)!;
        // Unique staging is outside both App and GameData; only this owned file is copied, never a tree.
        string operation = Path.Combine(plan.StagingDirectory, state.Token);
        StoragePathPolicy.EnsureNoReparsePoints(operation);
        if (Directory.Exists(operation) || File.Exists(operation))
            throw Error(InstallationError.ExistingInstallation, "Установочный staging уже существует; чужие файлы не перезаписываются.");
        Directory.CreateDirectory(operation);
        string setupFile = Path.Combine(operation, "Setup.exe");
        File.Copy(plan.Payload.SetupFile, setupFile, overwrite: false);
        VerifyHash(setupFile, plan.Payload.SetupSha256);
        using FileStream pinnedPayload = SafeLeafFile.OpenRead(setupFile);
        using WindowsInstallationJob job = new(state.Token);
        ProcessStartInfo start = new(setupFile) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = operation };
        start.ArgumentList.Add("--silent"); start.ArgumentList.Add("--installto"); start.ArgumentList.Add(plan.AppDirectory);
        start.ArgumentList.Add("--log"); start.ArgumentList.Add(Path.Combine(operation, "setup.log"));
        start.Environment["TEMP"] = operation; start.Environment["TMP"] = operation;
        cancellationToken.ThrowIfCancellationRequested();
        using WindowsInstallationProcess setup = job.Start(start);
        using CancellationTokenSource deadline = new(TimeSpan.FromMinutes(15));
        using CancellationTokenSource combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await setup.WaitForExitAsync(combined.Token).ConfigureAwait(false);
            while (job.ActiveProcesses > 0)
                await Task.Delay(50, combined.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            job.Terminate(); // ONLY this newly created installer tree; never existing App/guardian/Minecraft.
            // Durable intent remains. No success/rollback/timeout-based cleanup is performed.
            throw;
        }
        if (setup.ExitCode != 0)
            throw Error(InstallationError.Setup, $"Установщик завершился с кодом {setup.ExitCode}. Игровые данные и указатель сохранены.");
        RecordSuccessfulSetup(plan, setup.ExitCode, job.ActiveProcesses);
    }

    public bool IsVerified(InstallationPlan plan)
    {
        InstallationIntent? receipt = ReadReceipt();
        InstallationIntent? completion = ReadCompletion();
        InstallationIntent? pending = InstallationIntentStore.Read(Resolver().ControlDirectory);
        return receipt is not null && MatchesPlan(receipt, plan) && SameOperation(receipt, completion) &&
            (pending is null || SameOperation(pending, completion)) && IsAppVerified(plan) && IsRegistrationVerified(plan);
    }

    internal bool IsAppVerified(InstallationPlan plan)
    {
        if (!Directory.Exists(plan.AppDirectory)) return false;
        try
        {
            StoragePathPolicy.EnsureNoReparsePoints(plan.AppDirectory);
            string current = Path.Combine(plan.AppDirectory, "current");
            string packages = Path.Combine(plan.AppDirectory, "packages");
            StoragePathPolicy.EnsureNoReparsePoints(current);
            StoragePathPolicy.EnsureNoReparsePoints(packages);
            if (!Directory.Exists(current) || !Directory.Exists(packages)) return false;
            using FileStream package = SafeLeafFile.OpenRead(plan.Payload.FullPackageFile);
            using ZipArchive archive = new(package, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => !entry.FullName.EndsWith('/')))
            {
                // Velopack 1.2.161 installs this package entry at the App root, renamed to Update.exe.
                string? destination = entry.FullName.Equals("lib/app/Squirrel.exe", StringComparison.Ordinal)
                    ? Path.Combine(plan.AppDirectory, "Update.exe")
                    : entry.FullName.StartsWith("lib/app/", StringComparison.Ordinal)
                        ? Contained(current, entry.FullName[8..])
                        : entry.FullName.EndsWith("Squirrel.exe", StringComparison.Ordinal) ? Path.Combine(plan.AppDirectory, "Update.exe") : null;
                if (entry.FullName.EndsWith("_ExecutionStub.exe", StringComparison.Ordinal)) continue;
                if (destination is null) continue;
                StoragePathPolicy.EnsureNoReparsePoints(destination);
                if (!File.Exists(destination)) return false;
                using Stream expected = entry.Open();
                using FileStream actual = SafeLeafFile.OpenRead(destination);
                if (actual.Length != entry.Length || !SHA256.HashData(expected).SequenceEqual(SHA256.HashData(actual))) return false;
            }
            VerifyHash(Path.Combine(plan.AppDirectory, "Update.exe"), plan.Payload.UpdateSha256);
            string[] installedPackages = Directory.GetFiles(packages, "*-full.nupkg");
            if (installedPackages.Length != 1) return false;
            using FileStream installedStream = SafeLeafFile.OpenRead(installedPackages[0]);
            using ZipArchive installedArchive = new(installedStream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => !entry.FullName.EndsWith('/')))
            {
                ZipArchiveEntry? stored = installedArchive.GetEntry(entry.FullName);
                if (stored is null || stored.Length != entry.Length) return false;
                using Stream expected = entry.Open();
                using Stream actual = stored.Open();
                if (!SHA256.HashData(expected).SequenceEqual(SHA256.HashData(actual))) return false;
            }
            string manifestFile = Path.Combine(current, "sq.version");
            StoragePathPolicy.EnsureNoReparsePoints(manifestFile);
            using FileStream installedManifest = SafeLeafFile.OpenRead(manifestFile);
            VerifyManifest(installedManifest, plan.Payload);
            return File.Exists(Path.Combine(current, _product.MainExe)) &&
                Same(Resolver(plan.AppDirectory).InspectConfiguredRoot()!, plan.GameDataDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or
                   InstallationException or XmlException or InvalidOperationException or StorageRootException)
        { return false; }
    }

    public void Complete(InstallationPlan plan)
    {
        if (!IsVerified(plan)) throw Error(InstallationError.Verification, "Нельзя подтвердить успешную установку.");
        InstallationIntentStore.Delete(Resolver().ControlDirectory);
        // Keep unique staging/logs for diagnosis. No recursive deletion of App, GameData or backups.
    }

    internal bool IsRegistrationVerified(InstallationPlan plan)
    {
        _identity(); // Recheck non-elevated/non-impersonated identity before reading HKCU.
        using FileStream package = SafeLeafFile.OpenRead(plan.Payload.FullPackageFile);
        using ZipArchive archive = new(package, ZipArchiveMode.Read);
        using Stream manifest = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
        using XmlReader reader = XmlReader.Create(manifest, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        XElement metadata = XDocument.Load(reader).Descendants().Single(element => element.Name.LocalName == "metadata");
        string Value(string name) => metadata.Elements().SingleOrDefault(element => element.Name.LocalName == name)?.Value ?? "";
        string title = Value("title");
        return _registration()?.Matches(_product.PackId, plan.AppDirectory, _product.MainExe, plan.Payload.Version,
            string.IsNullOrEmpty(title) ? _product.PackId : title, Value("authors")) == true;
    }

    // Called only after successful native Setup exit AND installer-tree rundown. Tests simulate that boundary explicitly.
    internal void RecordSuccessfulSetup(InstallationPlan plan, int exitCode, uint activeProcesses)
    {
        InstallationIntent? state = InstallationIntentStore.Read(Resolver().ControlDirectory);
        if (exitCode != 0 || activeProcesses != 0 || state is null || !MatchesPlan(state, plan) || !SameOperation(ReadReceipt(), state) ||
            !IsAppVerified(plan) || !IsRegistrationVerified(plan))
            throw Error(InstallationError.Verification, "Setup, файлы или HKCU-регистрация не подтверждены. Установка остаётся незавершённой; требуется безопасная повторная попытка.");
        WriteRecord("installation-completion", state);
    }

    private static bool MatchesPlan(InstallationIntent state, InstallationPlan plan) =>
        Same(state.AppDirectory, plan.AppDirectory) && Same(state.GameDataDirectory, plan.GameDataDirectory) &&
        Same(state.PackageSha256, plan.Payload.PackageSha256);

    private static bool SameOperation(InstallationIntent? first, InstallationIntent? second) => first is not null && second is not null &&
        first == second; // Includes token, owner PID/start ticks, paths, package and contract versions.

    private long ValidatePayload(InstallationPayload payload, string app, string game)
    {
        if (payload.Channel is not ("stable" or "dev") || string.IsNullOrWhiteSpace(payload.Version))
            throw Error(InstallationError.Payload, "Неверная версия или канал доверенного пакета.");
        foreach (string file in new[] { payload.SetupFile, payload.FullPackageFile })
        {
            Normalize(file);
            if (StoragePathPolicy.IsWithinOrEqual(file, app) || StoragePathPolicy.IsWithinOrEqual(file, game))
                throw Error(InstallationError.Payload, "Установочный payload должен находиться вне App и игровых данных.");
        }
        VerifyHash(payload.SetupFile, payload.SetupSha256);
        VerifyHash(payload.FullPackageFile, payload.PackageSha256);
        if (payload.UpdateSha256.Length != 64 || !payload.UpdateSha256.All(Uri.IsHexDigit))
            throw Error(InstallationError.Payload, "Нет доверенного hash Update.exe.");
        using FileStream stream = SafeLeafFile.OpenRead(payload.FullPackageFile);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);
        ZipArchiveEntry manifest = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        if (manifest.Length > 65536) throw Error(InstallationError.Payload, "Неверный размер manifest.");
        using Stream xml = manifest.Open();
        using XmlReader reader = XmlReader.Create(xml, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        VerifyManifest(reader, payload);
        bool main = false;
        long extracted = 0;
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string canonical = Contained(app, entry.FullName);
            if (!names.Add(canonical)) throw Error(InstallationError.Payload, "Повторяющиеся пути в пакете.");
            if (entry.FullName.StartsWith("lib/", StringComparison.Ordinal) && !entry.FullName.StartsWith("lib/app/", StringComparison.Ordinal))
                throw Error(InstallationError.Payload, "Неожиданная раскладка библиотек в пакете.");
            main |= entry.FullName == "lib/app/" + _product.MainExe;
            if (entry.FullName.EndsWith(".__symlink", StringComparison.Ordinal) ||
                (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000)
                throw Error(InstallationError.Payload, "Ссылки в установочном пакете запрещены.");
            extracted = checked(extracted + entry.Length);
        }
        if (!main) throw Error(InstallationError.Payload, "В пакете отсутствует Launcher.");
        return extracted;
    }

    private void VerifyManifest(Stream stream, InstallationPayload payload)
    {
        if (stream.Length > 65536) throw Error(InstallationError.Payload, "Неверный размер установленного manifest.");
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        VerifyManifest(reader, payload);
    }

    private void VerifyManifest(XmlReader reader, InstallationPayload payload)
    {
        XElement metadata = XDocument.Load(reader).Descendants().Single(element => element.Name.LocalName == "metadata");
        string Value(string name) => metadata.Elements().SingleOrDefault(element => element.Name.LocalName == name)?.Value ?? "";
        if (Value("id") != _product.PackId || Value("version") != payload.Version || Value("channel") != payload.Channel ||
            Value("mainExe") != _product.MainExe || Value("machineArchitecture") != "x64" || Value("runtimeDependencies").Length != 0)
            throw Error(InstallationError.Payload, "Пакет не соответствует ожидаемой идентичности Launcher или требует внешних компонентов.");
    }

    private InstallationIntent? ReadReceipt()
    {
        string control = Resolver().ControlDirectory;
        // Receipt uses the same strict versioned data shape but is never an active-process gate.
        string receiptControl = Path.Combine(control, "installation-record");
        return InstallationIntentStore.Read(receiptControl);
    }

    private void WriteReceipt(InstallationIntent state)
        => WriteRecord("installation-record", state);

    private InstallationIntent? ReadCompletion() => InstallationIntentStore.Read(Path.Combine(Resolver().ControlDirectory, "installation-completion"));

    private void WriteRecord(string name, InstallationIntent state)
    {
        string directory = Path.Combine(Resolver().ControlDirectory, name);
        StoragePathPolicy.EnsureNoReparsePoints(directory);
        Directory.CreateDirectory(directory);
        InstallationIntentStore.Write(directory, state);
    }

    private string? RegisteredApp()
    {
        if (!_inspectRegistration || !OperatingSystem.IsWindows()) return null;
        return _registration()?.Values.GetValueOrDefault("InstallLocation") as string;
    }

    internal static string CurrentUserSid()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using WindowsIdentity? impersonated = WindowsIdentity.GetCurrent(true);
        if (impersonated is not null)
            throw Error(InstallationError.Identity, "Установка из impersonated-контекста запрещена.");
        using WindowsIdentity user = WindowsIdentity.GetCurrent();
        if (!GetTokenInformation(user.AccessToken.DangerousGetHandle(), 20, out int elevated, sizeof(int), out _) || elevated != 0)
            throw Error(InstallationError.Identity, "Запустите установщик обычным пользователем, без прав администратора.");
        return user.User!.Value;
    }

    private static bool HasData(string path)
    {
        StoragePathPolicy.EnsureNoReparsePoints(path);
        return File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
    }
    private static string Normalize(string path)
    {
        if (path.Split(['/', '\\']).Any(segment => segment is "." or "..")) throw Error(InstallationError.UnsafePath, "Path traversal запрещён.");
        string normalized = StoragePathPolicy.NormalizeDirectory(path);
        StoragePathPolicy.EnsureNoReparsePoints(normalized);
        if (Same(normalized, Path.GetPathRoot(normalized)!)) throw Error(InstallationError.UnsafePath, "Корень тома нельзя использовать как каталог установки.");
        return normalized;
    }
    private static string Contained(string root, string relative)
    {
        if (string.IsNullOrEmpty(relative) || relative.Split(['/', '\\']).Any(segment => segment is "." or "..") || relative.Contains(':'))
            throw Error(InstallationError.Payload, "Небезопасный путь в пакете.");
        string path = StoragePathPolicy.NormalizeDirectory(Path.Combine(root, relative.Replace('/', '\\')));
        if (!StoragePathPolicy.IsWithinOrEqual(path, root)) throw Error(InstallationError.Payload, "Выход за пределы пакета.");
        return path;
    }
    private static void RejectOverlap(string first, string second)
    {
        if (StoragePathPolicy.IsWithinOrEqual(first, second) || StoragePathPolicy.IsWithinOrEqual(second, first))
            throw Error(InstallationError.UnsafePath, "Каталоги установки, данных и системные пути не должны пересекаться.");
    }
    private static void ProbeNearest(string path)
    {
        StoragePathPolicy.EnsureNoReparsePoints(path);
        while (!Directory.Exists(path)) path = Path.GetDirectoryName(path) ?? throw Error(InstallationError.Unavailable, "Каталог недоступен.");
        using FileStream probe = new(Path.Combine(path, $".install-probe-{Guid.NewGuid():N}"), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        probe.WriteByte(0); probe.Flush(true);
    }
    private static void VerifyHash(string file, string expected)
    {
        if (expected is null || expected.Length != 64 || !expected.All(Uri.IsHexDigit)) throw Error(InstallationError.Payload, "Нет доверенного SHA-256 payload.");
        StoragePathPolicy.EnsureNoReparsePoints(file);
        using FileStream stream = SafeLeafFile.OpenRead(file);
        if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw Error(InstallationError.Payload, "SHA-256 установочного payload не совпадает.");
    }
    private static bool Same(string first, string second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    private static InstallationException Error(InstallationError error, string message) => new(error, message);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int informationClass, out int information, int size, out int returned);
}
