using HsWin.App.Shell;
using HsWin.Core.Logging;
using HsWin.Core.Shell;

namespace HsWin.App.Tests;

public sealed class ProcessShellServiceTests
{
    [Fact]
    public void ExecuteLogsCommandDescriptionWithoutRawCommandText()
    {
        var logger = new CapturingRuntimeLogger();
        var service = new ProcessShellService(logger);
        const string command = "echo secret-shell-token";

        service.Execute(command, ShellExecutionOptions.Default);

        Assert.Contains(logger.Infos, message => message.Contains("Command completed", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Infos, message => message.Contains("secret-shell-token", StringComparison.Ordinal));
        Assert.Contains(logger.Infos, message => message.Contains("length=", StringComparison.Ordinal));
        Assert.Contains(logger.Infos, message => message.Contains("sha256=", StringComparison.Ordinal));
    }

    private sealed class CapturingRuntimeLogger : IRuntimeLogger
    {
        public List<string> Infos { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<string> Errors { get; } = [];

        public void Info(string message)
        {
            Infos.Add(message);
        }

        public void Warning(string message)
        {
            Warnings.Add(message);
        }

        public void Error(string message, Exception exception)
        {
            Errors.Add($"{message} {exception.Message}");
        }
    }
}
