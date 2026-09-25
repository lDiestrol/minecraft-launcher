namespace Launcher.Core.Services;

public sealed class ServerConnectionException : Exception
{
    public ServerConnectionException(string userMessage, string technicalMessage, Exception? innerException = null)
        : base(technicalMessage, innerException)
    {
        UserMessage = userMessage;
    }

    public string UserMessage { get; }
}
