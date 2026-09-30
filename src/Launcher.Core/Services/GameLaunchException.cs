namespace Launcher.Core.Services;

public enum GameLaunchError
{
    InvalidProfile,
    UnsupportedLoader,
    AlreadyRunning,
    InsufficientDiskSpace,
    NetworkUnavailable,
    MetadataUnavailable,
    DownloadFailed,
    JavaPreparationFailed,
    FabricInstallationFailed,
    FabricVersionUnavailable,
    DirectoryAccessDenied,
    ProcessCreationFailed,
    Unknown,
}

public sealed class GameLaunchException : Exception
{
    public GameLaunchException(
        GameLaunchError error,
        string userMessage,
        string technicalMessage,
        Exception? innerException = null)
        : base(technicalMessage, innerException)
    {
        Error = error;
        UserMessage = userMessage;
    }

    public GameLaunchError Error { get; }

    public string UserMessage { get; }
}
