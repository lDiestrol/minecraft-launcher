namespace Launcher.Core.Services;

public interface IAppLogger
{
    void Info(string message);

    void Error(string message, Exception? exception = null);
}
