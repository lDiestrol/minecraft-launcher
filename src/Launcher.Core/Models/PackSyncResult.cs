namespace Launcher.Core.Models;

public sealed record PackSyncResult(
    int CheckedFiles,
    int DownloadedFiles,
    int RepairedFiles,
    int DeletedFiles,
    long DownloadedBytes,
    string PackVersion);
