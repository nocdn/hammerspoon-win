using HsWin.Core.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace HsWin.App;

internal static class PreviousInstanceCleaner
{
    private const int ExitTimeoutMs = 5_000;

    public static void TerminatePreviousInstances(IRuntimeLogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        using var currentProcess = Process.GetCurrentProcess();
        var currentProcessId = currentProcess.Id;
        var currentSessionId = TryGetSessionId(currentProcess);
        var currentExecutablePath = TryGetExecutablePath(currentProcess);

        var inspected = 0;
        var matched = 0;
        var stopped = 0;
        var failed = 0;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                inspected++;

                var processId = process.Id;
                if (processId == currentProcessId)
                {
                    continue;
                }

                var processName = TryGetProcessName(process);
                var sessionId = TryGetSessionId(process);
                var executablePath = TryGetExecutablePath(process);
                if (!PreviousInstanceProcessMatcher.ShouldTerminate(
                    processId,
                    currentProcessId,
                    processName,
                    sessionId,
                    currentSessionId,
                    executablePath,
                    currentExecutablePath))
                {
                    continue;
                }

                matched++;
                if (Terminate(process, processName, processId, executablePath, logger))
                {
                    stopped++;
                }
                else
                {
                    failed++;
                }
            }
        }

        logger.Info(
            $"Previous instance cleanup completed inspected={inspected} matched={matched} stopped={stopped} failed={failed}.");
    }

    private static bool Terminate(
        Process process,
        string processName,
        int processId,
        string? executablePath,
        IRuntimeLogger logger)
    {
        try
        {
            if (process.HasExited)
            {
                logger.Info($"Previous instance already exited processName='{processName}' id={processId}.");
                return true;
            }

            logger.Warning(
                $"Stopping previous HsWin instance processName='{processName}' id={processId} path='{executablePath ?? "<unknown>"}'.");

            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(ExitTimeoutMs))
            {
                logger.Warning($"Previous HsWin instance did not exit within {ExitTimeoutMs}ms id={processId}.");
                return false;
            }

            logger.Info($"Stopped previous HsWin instance processName='{processName}' id={processId}.");
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            logger.Error($"Could not stop previous HsWin instance processName='{processName}' id={processId}.", exception);
            return false;
        }
    }

    private static string TryGetProcessName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static int? TryGetSessionId(Process process)
    {
        try
        {
            return process.SessionId;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
        }
    }
}

internal static class PreviousInstanceProcessMatcher
{
    private static readonly string[] KnownProcessNames =
    [
        AppBranding.DisplayName,
        "HsWin.App"
    ];

    public static bool ShouldTerminate(
        int candidateProcessId,
        int currentProcessId,
        string candidateProcessName,
        int? candidateSessionId,
        int? currentSessionId,
        string? candidateExecutablePath,
        string? currentExecutablePath)
    {
        if (candidateProcessId == currentProcessId)
        {
            return false;
        }

        if (candidateSessionId.HasValue &&
            currentSessionId.HasValue &&
            candidateSessionId.Value != currentSessionId.Value)
        {
            return false;
        }

        if (KnownProcessNames.Any(name =>
            string.Equals(name, candidateProcessName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return PathsEqual(candidateExecutablePath, currentExecutablePath);
    }

    private static bool PathsEqual(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return string.Equals(NormalizePath(first), NormalizePath(second), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
