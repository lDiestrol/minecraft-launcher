namespace Launcher.Infrastructure.Persistence;

public enum StorageRootError
{
    InvalidConfiguration,
    UnsafePath,
    RootUnavailable,
    AccessDenied,
    Busy,
    ExistingData,
}

public sealed class StorageRootException : Exception
{
    public StorageRootException(StorageRootError error, string userMessage, Exception? innerException = null)
        : base(userMessage, innerException)
    {
        Error = error;
    }

    public StorageRootError Error { get; }
}
