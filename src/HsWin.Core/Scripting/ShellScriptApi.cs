using System.Globalization;
using HsWin.Core.Logging;
using HsWin.Core.Shell;

namespace HsWin.Core.Scripting;

public sealed class ShellScriptApi
{
    private readonly IShellService _shell;
    private readonly IRuntimeLogger _logger;

    public ShellScriptApi(IShellService shell, IRuntimeLogger logger)
    {
        _shell = shell;
        _logger = logger;
    }

    public string ExecuteCommandJson(object? command, object? options = null)
    {
        var normalizedCommand = ScriptArgumentReader.RequireNonWhiteSpaceString(command, "command");
        var parsedOptions = ParseExecutionOptions(options);
        _logger.Info($"Script hs.execute() requested command='{normalizedCommand}' timeoutMs={parsedOptions.TimeoutMs}.");
        var result = _shell.Execute(normalizedCommand, parsedOptions);
        _logger.Info($"Script hs.execute() completed success={result.Success} exitCode={result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "null"} timedOut={result.TimedOut}.");
        return ScriptJson.Serialize(result);
    }

    public static ShellExecutionOptions ParseExecutionOptions(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return ShellExecutionOptions.Default;
        }

        var workingDirectory = ScriptArgumentReader.OptionalString(
            ScriptArgumentReader.GetPropertyValue(value, "cwd", "workingDirectory", "directory"));
        var timeoutValue = ScriptArgumentReader.GetPropertyValue(value, "timeoutMs", "timeout");
        var timeoutMs = ScriptArgumentReader.IsMissing(timeoutValue)
            ? ShellExecutionOptions.DefaultTimeoutMs
            : ConvertPositiveInt(timeoutValue, "timeoutMs");

        return new ShellExecutionOptions(workingDirectory, timeoutMs);
    }

    public static LaunchOptions ParseLaunchOptions(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return LaunchOptions.Default;
        }

        var workingDirectory = ScriptArgumentReader.OptionalString(
            ScriptArgumentReader.GetPropertyValue(value, "cwd", "workingDirectory", "directory"));
        var arguments = ScriptArgumentReader.OptionalString(
            ScriptArgumentReader.GetPropertyValue(value, "arguments", "args"));
        return new LaunchOptions(workingDirectory, arguments);
    }

    private static int ConvertPositiveInt(object? value, string argumentName)
    {
        var result = ScriptArgumentReader.RequireInt32(value, argumentName, "a positive integer");
        if (result < 1)
        {
            throw new ArgumentOutOfRangeException(argumentName, $"{argumentName} must be at least 1.");
        }

        return result;
    }
}
