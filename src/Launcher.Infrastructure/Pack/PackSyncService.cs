using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Http;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Infrastructure.Pack;

public sealed class PackSyncService : IPackSyncService
{
    private const int SupportedSchemaVersion = 1;
    private const int MaximumManifestBytes = 4 * 1024 * 1024;
    private const int MaximumManagedFiles = 10_000;
    private const long DiskSafetyMarginBytes = 512L * 1024 * 1024;
    private const int BufferSize = 64 * 1024;
    private readonly ValidatedHttpClient _http;
    private readonly LauncherDataPaths _paths;
    private readonly IAppLogger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public PackSyncService(HttpClient httpClient, LauncherDataPaths paths, IAppLogger logger)
    {
        _http = new ValidatedHttpClient(httpClient);
        _paths = paths;
        _logger = logger;
    }

    public async Task<PackSyncResult> SyncAsync(
        GameProfile profile,
        IProgress<PackSyncProgress> progress,
        CancellationToken cancellationToken)
    {
        string? operationDirectory = null;
        try
        {
            Report(progress, PackSyncStage.FetchingManifest, "Получение manifest сборки");
            PackManifest manifest = await FetchManifestAsync(profile, cancellationToken);
            string instanceDirectory = PrepareInstanceDirectories(profile.Id);
            ManagedState? previousState = await LoadStateAsync(instanceDirectory, profile.Id, cancellationToken);

            _logger.Info(
                $"Pack sync: profile={profile.Id}, pack={manifest.PackVersion}, " +
                $"manifest={manifest.ManifestUri}, files={manifest.Files.Count}.");

            Report(progress, PackSyncStage.CheckingPack, "Проверка сборки", manifest.Files.Count, 0);
            List<PlannedDownload> downloads = [];
            int checkedFiles = 0;
            foreach (PackFileEntry entry in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destination = ManagedPathPolicy.ResolveDestination(instanceDirectory, entry.Path);
                bool existed = File.Exists(destination);
                bool valid = existed && await FileMatchesAsync(destination, entry, cancellationToken);
                if (!valid)
                {
                    downloads.Add(new PlannedDownload(entry, destination, existed));
                }

                checkedFiles++;
                Report(
                    progress,
                    PackSyncStage.HashingFiles,
                    "Хеширование файлов сборки",
                    manifest.Files.Count,
                    checkedFiles);
            }

            HashSet<string> currentPaths = manifest.Files
                .Select(file => file.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<string> obsoletePaths = previousState?.Files
                .Where(file => !currentPaths.Contains(file.Path))
                .Select(file => file.Path)
                .ToList() ?? [];

            long totalDownloadBytes = SumDownloadBytes(downloads);
            EnsureFreeDiskSpace(instanceDirectory, totalDownloadBytes);
            _logger.Info(
                $"Pack plan: valid={manifest.Files.Count - downloads.Count}, " +
                $"downloads={downloads.Count}, bytes={totalDownloadBytes}, obsolete={obsoletePaths.Count}.");

            if (downloads.Count > 0)
            {
                operationDirectory = CreateOperationDirectory(instanceDirectory);
                Report(
                    progress,
                    PackSyncStage.PreparingDownloads,
                    "Подготовка загрузок",
                    downloads.Count,
                    0,
                    totalDownloadBytes,
                    0);
                await StageDownloadsAsync(
                    downloads,
                    operationDirectory,
                    progress,
                    totalDownloadBytes,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, PackSyncStage.ApplyingUpdate, "Применение обновления", downloads.Count, 0);
            PublishDownloads(downloads, instanceDirectory);

            Report(
                progress,
                PackSyncStage.RemovingObsoleteFiles,
                "Удаление устаревших файлов",
                obsoletePaths.Count,
                0);
            int deletedFiles = DeleteObsoleteFiles(obsoletePaths, instanceDirectory, progress);

            await WriteStateAsync(instanceDirectory, manifest, CancellationToken.None);
            PackSyncResult result = new(
                checkedFiles,
                downloads.Count,
                downloads.Count(download => download.DestinationExisted),
                deletedFiles,
                totalDownloadBytes,
                manifest.PackVersion);
            Report(progress, PackSyncStage.Complete, "Сборка готова");
            _logger.Info(
                $"Pack sync complete: profile={profile.Id}, downloaded={result.DownloadedFiles}, " +
                $"repaired={result.RepairedFiles}, deleted={result.DeletedFiles}, bytes={result.DownloadedBytes}.");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.Info($"Pack sync cancelled: profile={profile.Id}.");
            throw;
        }
        catch (PackSyncException)
        {
            throw;
        }
        catch (TaskCanceledException exception)
        {
            throw Failure(
                PackSyncError.ManifestUnavailable,
                "Сервер сборки не ответил вовремя.",
                "Pack HTTP request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw Failure(
                PackSyncError.DownloadFailed,
                "Не удалось загрузить файлы сборки.",
                "Pack HTTP request failed.",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw Failure(
                PackSyncError.DirectoryAccessDenied,
                "Нет доступа к файлам игровой сборки.",
                "Access denied while synchronizing pack files.",
                exception);
        }
        catch (IOException exception)
        {
            throw Failure(
                PackSyncError.DownloadFailed,
                "Не удалось обновить файлы игровой сборки.",
                "I/O failure while synchronizing pack files.",
                exception);
        }
        catch (OverflowException exception)
        {
            throw Failure(
                PackSyncError.ManifestInvalid,
                "Manifest содержит слишком большой суммарный размер файлов.",
                "Pack size arithmetic overflow.",
                exception);
        }
        finally
        {
            DeleteDirectoryBestEffort(operationDirectory);
        }
    }

    private async Task<PackManifest> FetchManifestAsync(GameProfile profile, CancellationToken cancellationToken)
    {
        using ValidatedHttpResponse downloaded = await _http.GetAsync(
            profile.ManifestUrl,
            "pack manifest",
            cancellationToken);
        if (!downloaded.Response.IsSuccessStatusCode)
        {
            throw new PackSyncException(
                PackSyncError.ManifestUnavailable,
                "Manifest сборки недоступен.",
                $"HTTP {(int)downloaded.Response.StatusCode} for manifest {downloaded.FinalUri}.");
        }

        string json = await ReadLimitedTextAsync(
            downloaded.Response.Content,
            downloaded.FinalUri,
            cancellationToken);
        try
        {
            ManifestDto? document = JsonSerializer.Deserialize<ManifestDto>(json, _jsonOptions);
            if (document is null)
            {
                throw InvalidManifest("Manifest is empty.");
            }

            return ValidateManifest(document, downloaded.FinalUri, profile);
        }
        catch (JsonException exception)
        {
            throw Failure(
                PackSyncError.ManifestInvalid,
                "Сервер вернул некорректный manifest сборки.",
                "Invalid pack manifest JSON.",
                exception);
        }
    }

    private static PackManifest ValidateManifest(ManifestDto document, Uri finalUri, GameProfile profile)
    {
        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new PackSyncException(
                PackSyncError.UnsupportedSchema,
                document.SchemaVersion > SupportedSchemaVersion
                    ? "Manifest требует более новой версии Launcher."
                    : "Версия manifest сборки не поддерживается.",
                $"Unsupported pack manifest schemaVersion {document.SchemaVersion}.");
        }

        if (!StringEquals(document.ProfileId, profile.Id) ||
            !StringEquals(document.PackVersion, profile.PackVersion) ||
            !StringEquals(document.MinecraftVersion, profile.MinecraftVersion) ||
            document.Loader is null ||
            !StringEquals(document.Loader.Type, profile.LoaderType) ||
            !StringEquals(document.Loader.Version, profile.LoaderVersion))
        {
            throw new PackSyncException(
                PackSyncError.ProfileMismatch,
                "Manifest не соответствует выбранной игровой сборке.",
                $"Manifest/profile mismatch for profile '{profile.Id}'.");
        }

        if (document.Files is null || document.Files.Count > MaximumManagedFiles)
        {
            throw InvalidManifest(
                document.Files is null
                    ? "Manifest files array is missing."
                    : $"Manifest contains more than {MaximumManagedFiles} files.");
        }

        List<PackFileEntry> files = new(document.Files.Count);
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (ManifestFileDto file in document.Files)
        {
            string path = ManagedPathPolicy.ValidateManifestPath(file.Path);
            if (!paths.Add(path))
            {
                throw new PackSyncException(
                    PackSyncError.DuplicateManagedPath,
                    "Manifest содержит повторяющиеся пути файлов.",
                    $"Duplicate managed path: {path}.");
            }

            if (!IsSha256(file.Sha256) || file.Size < 0 || string.IsNullOrWhiteSpace(file.Url))
            {
                throw InvalidManifest($"Invalid file metadata for '{path}'.");
            }

            if (!Uri.TryCreate(finalUri, file.Url, out Uri? downloadUri) ||
                !HttpEndpointPolicy.IsAllowed(downloadUri))
            {
                throw new PackSyncException(
                    PackSyncError.UnsafeFileUrl,
                    "Manifest содержит запрещённый URL файла.",
                    $"Unsafe file URL for '{path}': '{file.Url}'.");
            }

            files.Add(new PackFileEntry(
                path,
                file.Sha256!.ToLowerInvariant(),
                file.Size,
                downloadUri));
        }

        return new PackManifest(
            document.SchemaVersion,
            document.ProfileId!.Trim(),
            document.PackVersion!.Trim(),
            document.MinecraftVersion!.Trim(),
            document.Loader!.Type!.Trim(),
            document.Loader.Version!.Trim(),
            finalUri,
            files);
    }

    private async Task StageDownloadsAsync(
        IReadOnlyList<PlannedDownload> downloads,
        string operationDirectory,
        IProgress<PackSyncProgress> progress,
        long totalBytes,
        CancellationToken cancellationToken)
    {
        long completedBytes = 0;
        for (int index = 0; index < downloads.Count; index++)
        {
            PlannedDownload download = downloads[index];
            string stagedPath = Path.Combine(operationDirectory, $"{index:D6}.download");
            download.StagedPath = stagedPath;
            await DownloadAndVerifyAsync(
                download.Entry,
                stagedPath,
                bytesRead =>
                {
                    long currentBytes = checked(completedBytes + bytesRead);
                    Report(
                        progress,
                        PackSyncStage.DownloadingFiles,
                        "Загрузка файлов сборки",
                        downloads.Count,
                        index,
                        totalBytes,
                        currentBytes);
                },
                cancellationToken);
            completedBytes = checked(completedBytes + download.Entry.Size);
            Report(
                progress,
                PackSyncStage.VerifyingFiles,
                "Проверка SHA-256",
                downloads.Count,
                index + 1,
                totalBytes,
                completedBytes);
        }
    }

    private async Task DownloadAndVerifyAsync(
        PackFileEntry entry,
        string stagedPath,
        Action<long> reportCurrentFileBytes,
        CancellationToken cancellationToken)
    {
        using ValidatedHttpResponse downloaded = await _http.GetAsync(
            entry.DownloadUri,
            entry.Path,
            cancellationToken);
        if (!downloaded.Response.IsSuccessStatusCode)
        {
            throw new PackSyncException(
                PackSyncError.DownloadFailed,
                $"Не удалось загрузить файл сборки '{entry.Path}'.",
                $"HTTP {(int)downloaded.Response.StatusCode} for {entry.DownloadUri}.");
        }

        if (downloaded.Response.Content.Headers.ContentLength is long contentLength &&
            contentLength != entry.Size)
        {
            throw SizeMismatch(entry, contentLength);
        }

        await using Stream source = await downloaded.Response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream destination = new(
            stagedPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[BufferSize];
        long totalRead = 0;
        while (true)
        {
            int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead = checked(totalRead + read);
            if (totalRead > entry.Size)
            {
                throw SizeMismatch(entry, totalRead);
            }

            hash.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            reportCurrentFileBytes(totalRead);
        }

        await destination.FlushAsync(cancellationToken);
        destination.Flush(true);
        if (totalRead != entry.Size)
        {
            throw SizeMismatch(entry, totalRead);
        }

        string actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (!actualHash.Equals(entry.Sha256, StringComparison.Ordinal))
        {
            _logger.Error($"SHA-256 mismatch for {entry.Path}: expected={entry.Sha256}, actual={actualHash}.");
            throw new PackSyncException(
                PackSyncError.HashMismatch,
                $"Проверка SHA-256 не пройдена для файла '{entry.Path}'.",
                $"SHA-256 mismatch for {entry.Path}: expected {entry.Sha256}, actual {actualHash}.");
        }
    }

    private static async Task<bool> FileMatchesAsync(
        string path,
        PackFileEntry entry,
        CancellationToken cancellationToken)
    {
        FileInfo info = new(path);
        if (info.Length != entry.Size)
        {
            return false;
        }

        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[BufferSize];
        while (true)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
        }

        string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return actual.Equals(entry.Sha256, StringComparison.Ordinal);
    }

    private static void PublishDownloads(
        IReadOnlyList<PlannedDownload> downloads,
        string instanceDirectory)
    {
        foreach (PlannedDownload download in downloads)
        {
            string destination = ManagedPathPolicy.ResolveDestination(instanceDirectory, download.Entry.Path);
            string parent = Path.GetDirectoryName(destination) ??
                throw new InvalidOperationException("Managed destination has no parent directory.");
            Directory.CreateDirectory(parent);
            destination = ManagedPathPolicy.ResolveDestination(instanceDirectory, download.Entry.Path);
            string stagedPath = download.StagedPath ??
                throw new InvalidOperationException("Download was not staged.");
            if (File.Exists(destination))
            {
                File.Replace(stagedPath, destination, null, true);
            }
            else
            {
                File.Move(stagedPath, destination);
            }
        }
    }

    private int DeleteObsoleteFiles(
        IReadOnlyList<string> obsoletePaths,
        string instanceDirectory,
        IProgress<PackSyncProgress> progress)
    {
        int deleted = 0;
        for (int index = 0; index < obsoletePaths.Count; index++)
        {
            string relativePath = ManagedPathPolicy.ValidateManifestPath(obsoletePaths[index]);
            string destination = ManagedPathPolicy.ResolveDestination(instanceDirectory, relativePath);
            if (File.Exists(destination))
            {
                File.Delete(destination);
                deleted++;
                _logger.Info($"Removed obsolete managed file: {relativePath}.");
            }

            DeleteEmptyParents(destination, instanceDirectory, relativePath);
            Report(
                progress,
                PackSyncStage.RemovingObsoleteFiles,
                "Удаление устаревших файлов",
                obsoletePaths.Count,
                index + 1);
        }

        return deleted;
    }

    private static void DeleteEmptyParents(string destination, string instanceDirectory, string relativePath)
    {
        string managedRootName = relativePath.Split('/')[0];
        string managedRoot = Path.Combine(instanceDirectory, managedRootName);
        string? current = Path.GetDirectoryName(destination);
        while (current is not null &&
               !current.Equals(managedRoot, StringComparison.OrdinalIgnoreCase) &&
               Directory.Exists(current) &&
               !Directory.EnumerateFileSystemEntries(current).Any())
        {
            Directory.Delete(current);
            current = Path.GetDirectoryName(current);
        }
    }

    private async Task<ManagedState?> LoadStateAsync(
        string instanceDirectory,
        string profileId,
        CancellationToken cancellationToken)
    {
        string statePath = GetStatePath(instanceDirectory);
        if (!File.Exists(statePath))
        {
            return null;
        }

        EnsureNotReparsePoint(statePath);

        try
        {
            FileInfo info = new(statePath);
            if (info.Length > MaximumManifestBytes)
            {
                throw new JsonException("Managed state exceeds the size limit.");
            }

            await using FileStream stream = new(
                statePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            ManagedStateDto? document = await JsonSerializer.DeserializeAsync<ManagedStateDto>(
                stream,
                _jsonOptions,
                cancellationToken);
            if (document is null ||
                document.SchemaVersion != SupportedSchemaVersion ||
                !StringEquals(document.ProfileId, profileId) ||
                string.IsNullOrWhiteSpace(document.PackVersion) ||
                document.Files is null ||
                document.Files.Count > MaximumManagedFiles)
            {
                throw new JsonException("Managed state header is invalid.");
            }

            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
            List<ManagedStateFile> files = new(document.Files.Count);
            foreach (ManagedStateFileDto file in document.Files)
            {
                string path = ManagedPathPolicy.ValidateManifestPath(file.Path);
                if (!paths.Add(path) || !IsSha256(file.Sha256) || file.Size < 0)
                {
                    throw new JsonException($"Managed state entry is invalid: '{file.Path}'.");
                }

                files.Add(new ManagedStateFile(path, file.Sha256!.ToLowerInvariant(), file.Size));
            }

            return new ManagedState(document.PackVersion.Trim(), files);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or PackSyncException)
        {
            throw Failure(
                PackSyncError.ManagedStateCorrupt,
                "Локальное состояние сборки повреждено. Удалите managed-state.json и повторите проверку.",
                $"Invalid managed state at {statePath}.",
                exception);
        }
    }

    private async Task WriteStateAsync(
        string instanceDirectory,
        PackManifest manifest,
        CancellationToken cancellationToken)
    {
        string statePath = GetStatePath(instanceDirectory);
        if (File.Exists(statePath))
        {
            EnsureNotReparsePoint(statePath);
        }

        string temporaryPath = statePath + $".{Guid.NewGuid():N}.tmp";
        ManagedStateDto document = new()
        {
            SchemaVersion = SupportedSchemaVersion,
            ProfileId = manifest.ProfileId,
            PackVersion = manifest.PackVersion,
            Files = manifest.Files
                .Select(file => new ManagedStateFileDto
                {
                    Path = file.Path,
                    Sha256 = file.Sha256,
                    Size = file.Size,
                })
                .ToList(),
        };

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document, _jsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }

            if (File.Exists(statePath))
            {
                File.Replace(temporaryPath, statePath, null, true);
            }
            else
            {
                File.Move(temporaryPath, statePath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task<string> ReadLimitedTextAsync(
        HttpContent content,
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumManifestBytes)
        {
            throw InvalidManifest($"Manifest at {uri} exceeds {MaximumManifestBytes} bytes.");
        }

        await using Stream source = await content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream destination = new();
        byte[] buffer = new byte[BufferSize];
        int total = 0;
        while (true)
        {
            int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            if (total > MaximumManifestBytes)
            {
                throw InvalidManifest($"Manifest at {uri} exceeds {MaximumManifestBytes} bytes.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return Encoding.UTF8.GetString(destination.GetBuffer(), 0, total);
    }

    private string PrepareInstanceDirectories(string profileId)
    {
        string instanceDirectory = _paths.GetInstanceDirectory(profileId);
        Directory.CreateDirectory(instanceDirectory);
        EnsureNotReparsePoint(instanceDirectory);
        string launcherDirectory = Path.Combine(instanceDirectory, ".launcher");
        Directory.CreateDirectory(launcherDirectory);
        EnsureNotReparsePoint(launcherDirectory);
        string stagingDirectory = Path.Combine(launcherDirectory, "staging");
        Directory.CreateDirectory(stagingDirectory);
        EnsureNotReparsePoint(stagingDirectory);
        return instanceDirectory;
    }

    private static string CreateOperationDirectory(string instanceDirectory)
    {
        string stagingDirectory = Path.Combine(instanceDirectory, ".launcher", "staging");
        EnsureNotReparsePoint(stagingDirectory);
        string operationDirectory = Path.Combine(stagingDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(operationDirectory);
        return operationDirectory;
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new PackSyncException(
                PackSyncError.UnsafeManagedPath,
                "Внутренняя директория сборки является небезопасной ссылкой.",
                $"Reparse point is not allowed: {path}.");
        }
    }

    private static void EnsureFreeDiskSpace(string instanceDirectory, long downloadBytes)
    {
        if (downloadBytes == 0)
        {
            return;
        }

        long required = checked(downloadBytes + DiskSafetyMarginBytes);
        string root = Path.GetPathRoot(instanceDirectory) ??
            throw new IOException("Could not determine the instance volume.");
        long available = new DriveInfo(root).AvailableFreeSpace;
        if (available < required)
        {
            throw new PackSyncException(
                PackSyncError.InsufficientDiskSpace,
                "Недостаточно места для обновления игровой сборки.",
                $"Pack sync requires {required} bytes, but only {available} bytes are available.");
        }
    }

    private static long SumDownloadBytes(IEnumerable<PlannedDownload> downloads)
    {
        long total = 0;
        foreach (PlannedDownload download in downloads)
        {
            total = checked(total + download.Entry.Size);
        }

        return total;
    }

    private static string GetStatePath(string instanceDirectory) =>
        Path.Combine(instanceDirectory, ".launcher", "managed-state.json");

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool StringEquals(string? left, string right) =>
        !string.IsNullOrWhiteSpace(left) && left.Trim().Equals(right, StringComparison.Ordinal);

    private static PackSyncException InvalidManifest(string technicalMessage) =>
        new(PackSyncError.ManifestInvalid, "Manifest игровой сборки некорректен.", technicalMessage);

    private PackSyncException Failure(
        PackSyncError error,
        string userMessage,
        string technicalMessage,
        Exception exception)
    {
        _logger.Error(technicalMessage, exception);
        return new PackSyncException(error, userMessage, technicalMessage, exception);
    }

    private static PackSyncException SizeMismatch(PackFileEntry entry, long actualSize) =>
        new(
            PackSyncError.FileSizeMismatch,
            $"Размер файла '{entry.Path}' не совпадает с manifest.",
            $"Size mismatch for {entry.Path}: expected {entry.Size}, actual {actualSize}.");

    private static void Report(
        IProgress<PackSyncProgress> progress,
        PackSyncStage stage,
        string message,
        int? totalFiles = null,
        int? completedFiles = null,
        long? totalBytes = null,
        long? completedBytes = null) =>
        progress.Report(new PackSyncProgress(
            stage,
            message,
            totalFiles,
            completedFiles,
            totalBytes,
            completedBytes));

    private static void DeleteDirectoryBestEffort(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _ = exception;
        }
    }

    private sealed class PlannedDownload
    {
        public PlannedDownload(PackFileEntry entry, string destination, bool destinationExisted)
        {
            Entry = entry;
            Destination = destination;
            DestinationExisted = destinationExisted;
        }

        public PackFileEntry Entry { get; }

        public string Destination { get; }

        public bool DestinationExisted { get; }

        public string? StagedPath { get; set; }
    }

    private sealed record ManagedState(string PackVersion, IReadOnlyList<ManagedStateFile> Files);

    private sealed record ManagedStateFile(string Path, string Sha256, long Size);

    private sealed class ManifestDto
    {
        public int SchemaVersion { get; init; }
        public string? ProfileId { get; init; }
        public string? PackVersion { get; init; }
        public string? MinecraftVersion { get; init; }
        public LoaderDto? Loader { get; init; }
        public List<ManifestFileDto>? Files { get; init; }
    }

    private sealed class LoaderDto
    {
        public string? Type { get; init; }
        public string? Version { get; init; }
    }

    private sealed class ManifestFileDto
    {
        public string? Path { get; init; }
        public string? Sha256 { get; init; }
        public long Size { get; init; }
        public string? Url { get; init; }
    }

    private sealed class ManagedStateDto
    {
        public int SchemaVersion { get; set; }
        public string? ProfileId { get; set; }
        public string? PackVersion { get; set; }
        public List<ManagedStateFileDto>? Files { get; set; }
    }

    private sealed class ManagedStateFileDto
    {
        public string? Path { get; set; }
        public string? Sha256 { get; set; }
        public long Size { get; set; }
    }
}
