namespace HammerspoonWin.Core.Logging;

public sealed class ReloadScriptConsoleLogger : IScriptConsoleLogger
{
    private readonly string _logDirectory;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();
    private string? _currentLogFilePath;

    public ReloadScriptConsoleLogger(string logDirectory)
        : this(logDirectory, () => DateTimeOffset.Now)
    {
    }

    public ReloadScriptConsoleLogger(string logDirectory, Func<DateTimeOffset> now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = logDirectory;
        _now = now;
    }

    public string? CurrentLogFilePath
    {
        get
        {
            lock (_gate)
            {
                return _currentLogFilePath;
            }
        }
    }

    public void BeginReload(string documentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        lock (_gate)
        {
            var logFilePath = DatedLogFileName.CreateUniquePath(_logDirectory, _now());
            _currentLogFilePath = logFilePath;
            File.AppendAllText(
                logFilePath,
                $"{DateTimeOffset.Now:O} [reload] Started {documentName}{Environment.NewLine}");
        }
    }

    public void Write(string level, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(level);

        lock (_gate)
        {
            if (_currentLogFilePath is null)
            {
                BeginReload("config.js");
            }

            var logFilePath = _currentLogFilePath
                ?? throw new InvalidOperationException("Console log file was not created.");
            File.AppendAllText(
                logFilePath,
                $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
        }
    }
}
