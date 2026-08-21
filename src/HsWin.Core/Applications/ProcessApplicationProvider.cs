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

    public IReadOnlyList<ApplicationSnapshot> GetRunningApplications(bool includeDetails)
    {
        using var processes = new ProcessCollection(Process.GetProcesses());
        var snapshots = processes
            .Select(process => CreateSnapshot(process, includeDetails))
            .OrderBy(snapshot => snapshot.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(snapshot => snapshot.Pid)
            .ToArray();
        _logger.Info($"Application runningApplications query returned {snapshots.Length} processes includeDetails={includeDetails}.");
        return snapshots;
    }

    private static ApplicationSnapshot CreateSnapshot(Process process, bool includeDetails)
    {
        // MainModule enumerates each process's loaded modules and throws for elevated
        // processes; reading it (and the window title) for every process on the machine is
        // expensive, so detail fields exist only when the caller asked for them.
        if (!includeDetails)
        {
            return new ApplicationSnapshot(process.Id, process.ProcessName, MainWindowTitle: null, Path: null);
        }

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
