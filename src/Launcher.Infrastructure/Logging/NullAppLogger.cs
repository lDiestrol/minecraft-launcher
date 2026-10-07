using Launcher.Core.Services;

namespace Launcher.Infrastructure.Logging;

public sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();

    public void Info(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
