using System.Globalization;

namespace HsWin.Core.Config;

internal static class ConfigTimerLiteralAnalyzer
{
    private const string TimerPrefix = "hs.timer.";

    public static IReadOnlyList<ConfigLintDiagnostic> Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var diagnostics = new List<ConfigLintDiagnostic>();
        for (var index = 0; index < source.Length;)
        {
            if (TrySkipNonCode(source, index, out var nextIndex))
            {
                index = nextIndex;
                continue;
            }

            if (TryReadBadTimerLiteral(source, index, out var methodName, out var literalStart))
            {
                var (line, column) = GetLocation(source, literalStart);
                var argumentName = string.Equals(methodName, "doAfter", StringComparison.Ordinal)
                    ? "delay"
                    : "interval";
                diagnostics.Add(new ConfigLintDiagnostic(
                    ConfigLintSeverity.Error,
                    "HSWIN001",
                    $"`hs.timer.{methodName}` {argumentName} must be at least 1 millisecond.",
                    line,
                    column));
            }

            index++;
        }

        return diagnostics;
    }

    private static bool TryReadBadTimerLiteral(
        string source,
        int index,
        out string methodName,
        out int literalStart)
    {
        methodName = string.Empty;
        literalStart = 0;
        if (!IsTokenBoundaryBefore(source, index) || !HasPrefix(source, index, TimerPrefix))
        {
            return false;
        }

        var methodIndex = index + TimerPrefix.Length;
        if (HasPrefix(source, methodIndex, "doAfter"))
        {
            methodName = "doAfter";
            methodIndex += "doAfter".Length;
        }
        else if (HasPrefix(source, methodIndex, "doEvery"))
        {
            methodName = "doEvery";
            methodIndex += "doEvery".Length;
        }
        else
        {
            return false;
        }

        var callIndex = SkipWhiteSpace(source, methodIndex);
        if (callIndex >= source.Length || source[callIndex] != '(')
        {
            return false;
        }

        literalStart = SkipWhiteSpace(source, callIndex + 1);
        if (!TryReadNumericLiteral(source, literalStart, out var literal, out _))
        {
            return false;
        }

        var normalizedLiteral = literal.Replace("_", string.Empty, StringComparison.Ordinal);
        return double.TryParse(normalizedLiteral, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && value < 1;
    }

    private static bool TryReadNumericLiteral(string source, int index, out string literal, out int nextIndex)
    {
        literal = string.Empty;
        nextIndex = index;
        if (index >= source.Length)
        {
            return false;
        }

        var current = index;
        if (source[current] is '+' or '-')
        {
            current++;
        }

        var hasDigit = false;
        while (current < source.Length && IsDigitOrSeparator(source[current]))
        {
            hasDigit |= char.IsAsciiDigit(source[current]);
            current++;
        }

        if (current < source.Length && source[current] == '.')
        {
            current++;
            while (current < source.Length && IsDigitOrSeparator(source[current]))
            {
                hasDigit |= char.IsAsciiDigit(source[current]);
                current++;
            }
        }

        if (!hasDigit)
        {
            return false;
        }

        if (current < source.Length && source[current] is 'e' or 'E')
        {
            var exponentIndex = current + 1;
            if (exponentIndex < source.Length && source[exponentIndex] is '+' or '-')
            {
                exponentIndex++;
            }

            var hasExponentDigit = false;
            while (exponentIndex < source.Length && IsDigitOrSeparator(source[exponentIndex]))
            {
                hasExponentDigit |= char.IsAsciiDigit(source[exponentIndex]);
                exponentIndex++;
            }

            if (hasExponentDigit)
            {
                current = exponentIndex;
            }
        }

        literal = source[index..current];
        nextIndex = current;
        return true;
    }

    private static bool TrySkipNonCode(string source, int index, out int nextIndex)
    {
        nextIndex = index;
        if (source[index] is '\'' or '"')
        {
            nextIndex = SkipQuotedString(source, index, source[index]);
            return true;
        }

        if (source[index] == '`')
        {
            nextIndex = SkipQuotedString(source, index, '`');
            return true;
        }

        if (index + 1 >= source.Length || source[index] != '/')
        {
            return false;
        }

        if (source[index + 1] == '/')
        {
            nextIndex = SkipLineComment(source, index + 2);
            return true;
        }

        if (source[index + 1] == '*')
        {
            nextIndex = SkipBlockComment(source, index + 2);
            return true;
        }

        return false;
    }

    private static int SkipQuotedString(string source, int index, char quote)
    {
        for (var current = index + 1; current < source.Length; current++)
        {
            if (source[current] == '\\')
            {
                current++;
                continue;
            }

            if (source[current] == quote)
            {
                return current + 1;
            }
        }

        return source.Length;
    }

    private static int SkipLineComment(string source, int index)
    {
        var newlineIndex = source.IndexOf('\n', index);
        return newlineIndex < 0 ? source.Length : newlineIndex + 1;
    }

    private static int SkipBlockComment(string source, int index)
    {
        var endIndex = source.IndexOf("*/", index, StringComparison.Ordinal);
        return endIndex < 0 ? source.Length : endIndex + 2;
    }

    private static int SkipWhiteSpace(string source, int index)
    {
        var current = index;
        while (current < source.Length && char.IsWhiteSpace(source[current]))
        {
            current++;
        }

        return current;
    }

    private static bool IsTokenBoundaryBefore(string source, int index)
    {
        if (index == 0)
        {
            return true;
        }

        var previous = source[index - 1];
        return !IsIdentifierPart(previous) && previous != '.';
    }

    private static bool HasPrefix(string source, int index, string prefix) =>
        index >= 0
        && index <= source.Length - prefix.Length
        && source.AsSpan(index, prefix.Length).SequenceEqual(prefix);

    private static bool IsDigitOrSeparator(char value) => char.IsAsciiDigit(value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '_' or '$';

    private static (int Line, int Column) GetLocation(string source, int index)
    {
        var line = 1;
        var column = 1;
        for (var current = 0; current < index && current < source.Length; current++)
        {
            if (source[current] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return (line, column);
    }
}
