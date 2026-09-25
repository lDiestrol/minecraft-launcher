using System.Globalization;
using System.Text;
using Launcher.Core.Services;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Infrastructure.Logging;

public sealed class FileAppLogger : IAppLogger
{
    private readonly object _sync = new();
    private readonly string _logFile;

    public FileAppLogger(LauncherDataPaths paths)
    {
        Directory.CreateDirectory(paths.LogsDirectory);
        _logFile = Path.Combine(
            paths.LogsDirectory,
            $"launcher-{DateTimeOffset.Now:yyyyMMdd}.log");
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        StringBuilder entry = new();
        entry.Append(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        entry.Append(" [").Append(level).Append("] ").AppendLine(message);
        if (exception is not null)
        {
            entry.AppendLine(exception.ToString());
        }

        try
        {
            lock (_sync)
            {
                File.AppendAllText(_logFile, entry.ToString(), Encoding.UTF8);
            }
        }
        catch (IOException)
        {
            // Logging must never terminate the launcher.
        }
        catch (UnauthorizedAccessException)
        {
            // Logging must never terminate the launcher.
        }
    }
}
