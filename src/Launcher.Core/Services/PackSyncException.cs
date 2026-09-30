namespace Launcher.Core.Services;

public enum PackSyncError
{
    ManifestUnavailable,
    ManifestInvalid,
    UnsupportedSchema,
    ProfileMismatch,
    UnsafeManagedPath,
    DuplicateManagedPath,
    UnsafeFileUrl,
    DownloadFailed,
    FileSizeMismatch,
    HashMismatch,
    InsufficientDiskSpace,
    DirectoryAccessDenied,
    ManagedStateCorrupt,
    AlreadyRunning,
    Unknown,
}

public sealed class PackSyncException : Exception
{
    public PackSyncException(
        PackSyncError error,
        string userMessage,
        string technicalMessage,
        Exception? innerException = null)
        : base(technicalMessage, innerException)
    {
        Error = error;
        UserMessage = userMessage;
    }

    public PackSyncError Error { get; }

    public string UserMessage { get; }
}
