using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using HsWin.Core.Logging;

namespace HsWin.App;

internal sealed class FileLogger : IRuntimeLogger, IDisposable
{
    private readonly string _logFilePath;
    private readonly BlockingCollection<LogEntry> _entries = [];
    private readonly Thread _worker;
    private int _disposed;

    private FileLogger(string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);
        _logFilePath = logFilePath;
        EnsureLogFile();
        _worker = new Thread(WriteQueuedEntries)
        {
            IsBackground = true,
            Name = "HsWin Runtime Logger"
        };
        _worker.Start();
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
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _entries.Add(new LogEntry(DateTimeOffset.Now, level, message, exception));
        }
        catch
        {
            // Logging must never break the tray app or script reload loop.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _entries.CompleteAdding();
            _worker.Join(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Logging shutdown must not block app shutdown.
        }
        finally
        {
            _entries.Dispose();
        }
    }

    private void EnsureLogFile()
    {
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var _ = new FileStream(
            _logFilePath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.ReadWrite);
    }

    private void WriteQueuedEntries()
    {
        try
        {
            using var stream = new FileStream(
                _logFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 16 * 1024,
                FileOptions.SequentialScan);
            using var writer = new StreamWriter(stream);

            foreach (var entry in _entries.GetConsumingEnumerable())
            {
                WriteEntry(writer, entry);
                while (_entries.TryTake(out var nextEntry))
                {
                    WriteEntry(writer, nextEntry);
                }

                writer.Flush();
            }
        }
        catch
        {
            // Logging must never break the tray app or script reload loop.
        }
    }

    private static void WriteEntry(TextWriter writer, LogEntry entry)
    {
        writer.Write(entry.Timestamp.ToString("O", CultureInfo.InvariantCulture));
        writer.Write(" [");
        writer.Write(entry.Level);
        writer.Write("] ");
        writer.WriteLine(entry.Message);
        writer.WriteLine(entry.Exception);
    }

    private sealed record LogEntry(
        DateTimeOffset Timestamp,
        string Level,
        string Message,
        Exception? Exception);
}
