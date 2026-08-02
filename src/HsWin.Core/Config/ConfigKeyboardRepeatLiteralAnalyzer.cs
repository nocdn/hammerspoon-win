using System.Globalization;

namespace HsWin.Core.Config;

internal static class ConfigKeyboardRepeatLiteralAnalyzer
{
    private const string KeyboardPrefix = "hs.keyboard.";

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

            if (TryAnalyzeCall(source, index, out var diagnostic, out nextIndex))
            {
                if (diagnostic is not null)
                {
                    diagnostics.Add(diagnostic);
                }

                index = nextIndex;
                continue;
            }

            index++;
        }

        return diagnostics;
    }

    private static bool TryAnalyzeCall(
        string source,
        int index,
        out ConfigLintDiagnostic? diagnostic,
        out int nextIndex)
    {
        diagnostic = null;
        nextIndex = index + 1;
        if (!IsTokenBoundaryBefore(source, index) || !HasPrefix(source, index, KeyboardPrefix))
        {
            return false;
        }

        var methodIndex = index + KeyboardPrefix.Length;
        string methodName;
        if (HasPrefix(source, methodIndex, "repeatPulse"))
        {
            methodName = "repeatPulse";
            methodIndex += "repeatPulse".Length;
        }
        else if (HasPrefix(source, methodIndex, "repeat"))
        {
            methodName = "repeat";
            methodIndex += "repeat".Length;
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

        var keyIndex = SkipWhiteSpace(source, callIndex + 1);
        if (!TryReadKeyLiteral(source, keyIndex, out var isModifier, out var afterKey))
        {
            return true;
        }

        if (methodName == "repeat" && isModifier)
        {
            diagnostic = CreateDiagnostic(
                source,
                index,
                "HSWIN002",
                "Modifier keys cannot use `hs.keyboard.repeat`; use `hs.keyboard.repeatPulse` with `keyDownMs > 0`.");
            return true;
        }

        if (methodName != "repeatPulse")
        {
            return true;
        }

        var commaIndex = SkipWhiteSpace(source, afterKey);
        if (commaIndex >= source.Length || source[commaIndex] != ',')
        {
            diagnostic = CreateMissingDurationDiagnostic(source, index);
            return true;
        }

        var optionsIndex = SkipWhiteSpace(source, commaIndex + 1);
        if (optionsIndex >= source.Length || source[optionsIndex] != '{')
        {
            // A dynamic options object cannot be proven invalid statically. Runtime validation
            // still rejects a missing or non-positive duration when this call executes.
            return true;
        }

        var optionsEnd = FindMatchingBrace(source, optionsIndex);
        nextIndex = optionsEnd;
        var duration = FindDurationProperty(source, optionsIndex + 1, optionsEnd - 1);
        if (duration is DurationStatus.Missing or DurationStatus.NonPositive)
        {
            diagnostic = duration == DurationStatus.Missing
                ? CreateMissingDurationDiagnostic(source, index)
                : CreateDiagnostic(
                    source,
                    index,
                    "HSWIN003",
                    "`hs.keyboard.repeatPulse` keyDownMs must be greater than 0.");
        }

        return true;
    }

    private static DurationStatus FindDurationProperty(string source, int start, int end)
    {
        var depth = 0;
        for (var index = start; index < end;)
        {
            if (TrySkipNonCode(source, index, out var nextIndex))
            {
                if ((source[index] is '\'' or '"') && depth == 0
                    && TryReadQuotedValue(source, index, out var propertyName, out var afterName)
                    && IsDurationProperty(propertyName))
                {
                    return ReadDurationValue(source, afterName, end);
                }

                index = nextIndex;
                continue;
            }

            if (source[index] is '{' or '[' or '(')
            {
                depth++;
                index++;
                continue;
            }

            if (source[index] is '}' or ']' or ')')
            {
                depth = Math.Max(0, depth - 1);
                index++;
                continue;
            }

            if (depth == 0 && TryReadIdentifier(source, index, out var identifier, out nextIndex))
            {
                if (IsDurationProperty(identifier))
                {
                    return ReadDurationValue(source, nextIndex, end);
                }

                index = nextIndex;
                continue;
            }

            index++;
        }

        return DurationStatus.Missing;
    }

    private static DurationStatus ReadDurationValue(string source, int afterName, int end)
    {
        var colonIndex = SkipWhiteSpace(source, afterName);
        if (colonIndex >= end || source[colonIndex] != ':')
        {
            return DurationStatus.Unknown;
        }

        var valueIndex = SkipWhiteSpace(source, colonIndex + 1);
        if (!TryReadNumber(source, valueIndex, out var value, out _))
        {
            return DurationStatus.Unknown;
        }

        return value > 0 ? DurationStatus.Positive : DurationStatus.NonPositive;
    }

    private static bool TryReadKeyLiteral(
        string source,
        int index,
        out bool isModifier,
        out int nextIndex)
    {
        isModifier = false;
        nextIndex = index;
        if (index >= source.Length)
        {
            return false;
        }

        if (source[index] is '\'' or '"')
        {
            if (!TryReadQuotedValue(source, index, out var value, out nextIndex))
            {
                return false;
            }

            isModifier = value.Equals("shift", StringComparison.OrdinalIgnoreCase)
                || value.Equals("ctrl", StringComparison.OrdinalIgnoreCase)
                || value.Equals("control", StringComparison.OrdinalIgnoreCase)
                || value.Equals("alt", StringComparison.OrdinalIgnoreCase)
                || value.Equals("win", StringComparison.OrdinalIgnoreCase)
                || value.Equals("windows", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        if (!TryReadInteger(source, index, out var virtualKey, out nextIndex))
        {
            return false;
        }

        isModifier = virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C
            or >= 0xA0 and <= 0xA5;
        return true;
    }

    private static bool TryReadQuotedValue(
        string source,
        int index,
        out string value,
        out int nextIndex)
    {
        value = string.Empty;
        nextIndex = index;
        if (index >= source.Length || source[index] is not ('\'' or '"'))
        {
            return false;
        }

        var quote = source[index];
        var builder = new System.Text.StringBuilder();
        for (var current = index + 1; current < source.Length; current++)
        {
            if (source[current] == '\\' && current + 1 < source.Length)
            {
                builder.Append(source[++current]);
                continue;
            }

            if (source[current] == quote)
            {
                value = builder.ToString();
                nextIndex = current + 1;
                return true;
            }

            builder.Append(source[current]);
        }

        nextIndex = source.Length;
        return false;
    }

    private static bool TryReadIdentifier(
        string source,
        int index,
        out string identifier,
        out int nextIndex)
    {
        identifier = string.Empty;
        nextIndex = index;
        if (index >= source.Length || !IsIdentifierStart(source[index]))
        {
            return false;
        }

        var current = index + 1;
        while (current < source.Length && IsIdentifierPart(source[current]))
        {
            current++;
        }

        identifier = source[index..current];
        nextIndex = current;
        return true;
    }

    private static bool TryReadInteger(string source, int index, out int value, out int nextIndex)
    {
        value = 0;
        nextIndex = index;
        var current = index;
        if (current + 2 <= source.Length
            && current + 1 < source.Length
            && source[current] == '0'
            && source[current + 1] is 'x' or 'X')
        {
            current += 2;
            var digitsStart = current;
            while (current < source.Length && Uri.IsHexDigit(source[current]))
            {
                current++;
            }

            if (current == digitsStart
                || !int.TryParse(source[digitsStart..current], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            nextIndex = current;
            return true;
        }

        if (!TryReadNumber(source, index, out var number, out nextIndex)
            || number != Math.Truncate(number)
            || number is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        value = (int)number;
        return true;
    }

    private static bool TryReadNumber(string source, int index, out double value, out int nextIndex)
    {
        value = 0;
        nextIndex = index;
        var current = index;
        if (current < source.Length && source[current] is '+' or '-')
        {
            current++;
        }

        var hasDigit = false;
        while (current < source.Length && (char.IsAsciiDigit(source[current]) || source[current] == '_'))
        {
            hasDigit |= char.IsAsciiDigit(source[current]);
            current++;
        }

        if (current < source.Length && source[current] == '.')
        {
            current++;
            while (current < source.Length && (char.IsAsciiDigit(source[current]) || source[current] == '_'))
            {
                hasDigit |= char.IsAsciiDigit(source[current]);
                current++;
            }
        }

        if (!hasDigit)
        {
            return false;
        }

        var literal = source[index..current].Replace("_", string.Empty, StringComparison.Ordinal);
        if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        nextIndex = current;
        return true;
    }

    private static int FindMatchingBrace(string source, int start)
    {
        var depth = 0;
        for (var index = start; index < source.Length;)
        {
            if (TrySkipNonCode(source, index, out var nextIndex))
            {
                index = nextIndex;
                continue;
            }

            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return index + 1;
            }

            index++;
        }

        return source.Length;
    }

    private static bool TrySkipNonCode(string source, int index, out int nextIndex)
    {
        nextIndex = index;
        if (source[index] is '\'' or '"' or '`')
        {
            var quote = source[index];
            for (var current = index + 1; current < source.Length; current++)
            {
                if (source[current] == '\\')
                {
                    current++;
                    continue;
                }

                if (source[current] == quote)
                {
                    nextIndex = current + 1;
                    return true;
                }
            }

            nextIndex = source.Length;
            return true;
        }

        if (index + 1 >= source.Length || source[index] != '/')
        {
            return false;
        }

        if (source[index + 1] == '/')
        {
            var newline = source.IndexOf('\n', index + 2);
            nextIndex = newline < 0 ? source.Length : newline + 1;
            return true;
        }

        if (source[index + 1] == '*')
        {
            var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
            nextIndex = end < 0 ? source.Length : end + 2;
            return true;
        }

        return false;
    }

    private static ConfigLintDiagnostic CreateMissingDurationDiagnostic(string source, int index) =>
        CreateDiagnostic(
            source,
            index,
            "HSWIN003",
            "`hs.keyboard.repeatPulse` requires `keyDownMs > 0` so sampled applications observe the key-down phase.");

    private static ConfigLintDiagnostic CreateDiagnostic(
        string source,
        int index,
        string code,
        string message)
    {
        var (line, column) = GetLocation(source, index);
        return new ConfigLintDiagnostic(ConfigLintSeverity.Error, code, message, line, column);
    }

    private static bool IsDurationProperty(string value) =>
        value is "keyDownMs" or "holdMs" or "pressDurationMs";

    private static int SkipWhiteSpace(string source, int index)
    {
        while (index < source.Length && char.IsWhiteSpace(source[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsTokenBoundaryBefore(string source, int index) =>
        index == 0 || (!IsIdentifierPart(source[index - 1]) && source[index - 1] != '.');

    private static bool HasPrefix(string source, int index, string prefix) =>
        index >= 0
        && index <= source.Length - prefix.Length
        && source.AsSpan(index, prefix.Length).SequenceEqual(prefix);

    private static bool IsIdentifierStart(char value) => char.IsAsciiLetter(value) || value is '_' or '$';

    private static bool IsIdentifierPart(char value) => char.IsAsciiLetterOrDigit(value) || value is '_' or '$';

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

    private enum DurationStatus
    {
        Missing,
        NonPositive,
        Positive,
        Unknown
    }
}
