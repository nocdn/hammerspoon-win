using System.Reflection;
using HsWin.Core.Scripting;
using Microsoft.ClearScript;

namespace HsWin.Core.Config;

public sealed class ConfigLinter
{
    public ConfigLintResult LintFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            return new ConfigLintResult(
                [
                    new ConfigLintDiagnostic(
                        ConfigLintSeverity.Error,
                        "HSWIN000",
                        $"Config file was not found: {filePath}")
                ]);
        }

        return LintSource(File.ReadAllText(filePath), filePath);
    }

    public ConfigLintResult LintSource(string source, string documentName = "config.js")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        var diagnostics = new List<ConfigLintDiagnostic>();
        diagnostics.AddRange(ConfigTimerLiteralAnalyzer.Analyze(source));

        try
        {
            using var runtime = new ScriptRuntime(ConfigLintRuntimeServices.Create());
            runtime.Reload(source, documentName);
        }
        catch (Exception exception)
        {
            if (!IsDuplicateTimerLiteralDiagnostic(exception, diagnostics))
            {
                diagnostics.Add(new ConfigLintDiagnostic(
                    ConfigLintSeverity.Error,
                    "HSWIN100",
                    FormatException(exception)));
            }
        }

        return new ConfigLintResult(diagnostics);
    }

    private static string FormatException(Exception exception)
    {
        var unwrapped = Unwrap(exception);
        var message = unwrapped is ScriptEngineException scriptException
            ? scriptException.ErrorDetails ?? scriptException.Message
            : unwrapped.Message;

        message = FirstNonEmptyLine(message).Trim();
        return message.StartsWith("Error:", StringComparison.Ordinal)
            ? message[6..].Trim()
            : message;
    }

    private static Exception Unwrap(Exception exception)
    {
        var current = exception;
        while (true)
        {
            switch (current)
            {
                case TargetInvocationException { InnerException: { } inner }:
                    current = inner;
                    continue;
                case AggregateException { InnerException: { } inner }:
                    current = inner;
                    continue;
                case ScriptEngineException scriptException when scriptException.InnerException is not null:
                    current = scriptException.InnerException;
                    continue;
            }

            return current;
        }
    }

    private static string FirstNonEmptyLine(string value)
    {
        using var reader = new StringReader(value);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }

        return value;
    }

    private static bool IsDuplicateTimerLiteralDiagnostic(
        Exception exception,
        IReadOnlyCollection<ConfigLintDiagnostic> diagnostics)
    {
        return diagnostics.Any(static diagnostic => diagnostic.Code == "HSWIN001")
            && FormatException(exception).Contains("Timer interval must be at least 1 millisecond", StringComparison.Ordinal);
    }
}
