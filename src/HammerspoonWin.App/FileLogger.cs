namespace HammerspoonWin.App;

using HammerspoonWin.Core.Logging;
using System.IO;

internal sealed class FileLogger : IRuntimeLogger
{
    private readonly string _logFilePath;
    private readonly object _gate = new();

    private FileLogger(string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);
        _logFilePath = logFilePath;
    }

    public string LogFilePath => _logFilePath;

    public static FileLogger CreateForLaunch(string logDirectory)
    {
        return new FileLogger(DatedLogFileName.CreateUniquePath(logDirectory, DateTimeOffset.Now));
    }

    public void Info(string message)
    {
        Write("INFO", message, null);
    }

    public void Warning(string message)
    {
        Write("WARN", message, null);
    }

    public void Error(string message, Exception exception)
    {
        Write("ERROR", message, exception);
    }

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (_gate)
            {
                var directory = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(
                    _logFilePath,
                    $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}{exception}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never break the tray app or script reload loop.
        }
    }
}
