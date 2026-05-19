using System.ComponentModel;
using System.Diagnostics;
using HsWin.Core.Logging;

namespace HsWin.Core.Applications;

public sealed class ProcessApplicationProvider : IApplicationProvider
{
    private readonly IRuntimeLogger _logger;

    public ProcessApplicationProvider()
        : this(NullRuntimeLogger.Instance)
    {
    }

    public ProcessApplicationProvider(IRuntimeLogger logger)
    {
        _logger = logger;
    }

    public bool IsRunning(string processName)
    {
        var normalizedName = ProcessNameMatcher.Normalize(processName);
        using var processes = new ProcessCollection(Process.GetProcessesByName(normalizedName));
        var isRunning = processes.Any();
        _logger.Info($"Application isRunning query processName='{processName}' normalized='{normalizedName}' result={isRunning}.");
        return isRunning;
    }

    public IReadOnlyList<ApplicationSnapshot> GetRunningApplications()
    {
        using var processes = new ProcessCollection(Process.GetProcesses());
        var snapshots = processes
            .Select(CreateSnapshot)
            .OrderBy(snapshot => snapshot.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(snapshot => snapshot.Pid)
            .ToArray();
        _logger.Info($"Application runningApplications query returned {snapshots.Length} processes.");
        return snapshots;
    }

    private static ApplicationSnapshot CreateSnapshot(Process process)
    {
        return new ApplicationSnapshot(
            process.Id,
            process.ProcessName,
            ReadSafe(() => string.IsNullOrWhiteSpace(process.MainWindowTitle) ? null : process.MainWindowTitle),
            ReadSafe(() => process.MainModule?.FileName));
    }

    private static T? ReadSafe<T>(Func<T?> read)
    {
        try
        {
            return read();
        }
        catch (Win32Exception)
        {
            return default;
        }
        catch (InvalidOperationException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }

    private sealed class ProcessCollection : IDisposable
    {
        private readonly IReadOnlyList<Process> _processes;

        public ProcessCollection(Process[] processes)
        {
            _processes = processes;
        }

        public bool Any()
        {
            return _processes.Count > 0;
        }

        public IEnumerable<TResult> Select<TResult>(Func<Process, TResult> selector)
        {
            return _processes.Select(selector);
        }

        public void Dispose()
        {
            foreach (var process in _processes)
            {
                process.Dispose();
            }
        }
    }
}
