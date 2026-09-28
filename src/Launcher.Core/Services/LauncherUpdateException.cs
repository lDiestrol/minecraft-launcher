namespace Launcher.Core.Services;

public enum LauncherUpdateError
{
    NotInstalled,
    AlreadyRunning,
    GameOperationActive,
    UpdateNotSelected,
    UpdateNotDownloaded,
    CheckFailed,
    DownloadFailed,
    ApplyFailed,
}

public sealed class LauncherUpdateException : Exception
{
    public LauncherUpdateException(
        LauncherUpdateError error,
        string userMessage,
        string technicalMessage,
        Exception? innerException = null)
        : base(technicalMessage, innerException)
    {
        Error = error;
        UserMessage = userMessage;
    }

    public LauncherUpdateError Error { get; }

    public string UserMessage { get; }
}
