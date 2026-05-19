using HsWin.Core.Logging;
using HsWin.Core.Shell;
using System.Diagnostics;

namespace HsWin.App.Shell;

internal sealed class ProcessShellService : IShellService
{
    private readonly IRuntimeLogger _logger;

    public ProcessShellService(IRuntimeLogger logger)
    {
        _logger = logger;
    }

    public ShellExecutionResult Execute(string command, ShellExecutionOptions options)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = CreateShellStartInfo(command, options)
            };

            if (!process.Start())
            {
                return new ShellExecutionResult(command, false, null, string.Empty, "Process did not start.", TimedOut: false);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(options.TimeoutMs))
            {
                KillTimedOutProcess(process);
                var timedOutOutput = outputTask.GetAwaiter().GetResult();
                var timedOutError = errorTask.GetAwaiter().GetResult();
                _logger.Warning($"Command timed out command='{command}' timeoutMs={options.TimeoutMs}.");
                return new ShellExecutionResult(command, false, null, timedOutOutput, timedOutError, TimedOut: true);
            }

            var output = outputTask.GetAwaiter().GetResult();
            var error = errorTask.GetAwaiter().GetResult();
            var success = process.ExitCode == 0;
            _logger.Info($"Command completed command='{command}' exitCode={process.ExitCode} success={success}.");
            return new ShellExecutionResult(command, success, process.ExitCode, output, error, TimedOut: false);
        }
        catch (Exception exception)
        {
            _logger.Error($"Command failed command='{command}'.", exception);
            return new ShellExecutionResult(command, false, null, string.Empty, exception.Message, TimedOut: false);
        }
    }

    public LaunchResult Launch(string target, LaunchOptions options)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
                WorkingDirectory = options.WorkingDirectory ?? string.Empty,
                Arguments = options.Arguments ?? string.Empty
            };

            using var process = Process.Start(startInfo);
            var processId = process?.Id;
            _logger.Info($"Launch completed target='{target}' processId={processId?.ToString() ?? "null"}.");
            return new LaunchResult(target, Success: true, processId, Error: null);
        }
        catch (Exception exception)
        {
            _logger.Error($"Launch failed target='{target}'.", exception);
            return new LaunchResult(target, Success: false, ProcessId: null, exception.Message);
        }
    }

    private static ProcessStartInfo CreateShellStartInfo(string command, ShellExecutionOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = options.WorkingDirectory ?? string.Empty
        };

        startInfo.ArgumentList.Add("/S");
        startInfo.ArgumentList.Add("/C");
        startInfo.ArgumentList.Add(command);
        return startInfo;
    }

    private static void KillTimedOutProcess(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
