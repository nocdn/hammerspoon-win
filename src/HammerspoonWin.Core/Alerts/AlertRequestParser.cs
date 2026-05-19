using System.Collections;
using System.Globalization;
using Microsoft.ClearScript;

namespace HammerspoonWin.Core.Alerts;

public static class AlertRequestParser
{
    public static AlertRequest FromScriptArguments(object? text, object? optionsOrKind = null, object? durationMs = null)
    {
        var message = ConvertToString(text, "text");
        var kind = AlertRequest.DefaultKind;
        int? duration = null;

        if (!IsMissing(optionsOrKind))
        {
            if (TryReadOptions(optionsOrKind, out var optionsKind, out var optionsDurationMs))
            {
                kind = optionsKind ?? kind;
                duration = optionsDurationMs;
            }
            else
            {
                kind = AlertRequest.ParseKind(ConvertToString(optionsOrKind, "type"));
            }
        }

        if (!IsMissing(durationMs))
        {
            duration = ConvertToDurationMs(durationMs, "durationMs");
        }

        return AlertRequest.Create(message, kind, duration);
    }

    private static bool TryReadOptions(object? value, out AlertKind? kind, out int? durationMs)
    {
        kind = null;
        durationMs = null;

        if (value is ScriptObject scriptObject)
        {
            kind = ReadKind(GetFirstScriptProperty(scriptObject, "type", "kind", "state", "status"));
            durationMs = ReadDuration(GetFirstScriptProperty(scriptObject, "durationMs", "duration"));
            return true;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            kind = ReadKind(GetFirstDictionaryValue(readOnlyDictionary, "type", "kind", "state", "status"));
            durationMs = ReadDuration(GetFirstDictionaryValue(readOnlyDictionary, "durationMs", "duration"));
            return true;
        }

        if (value is IDictionary dictionary)
        {
            kind = ReadKind(GetFirstDictionaryValue(dictionary, "type", "kind", "state", "status"));
            durationMs = ReadDuration(GetFirstDictionaryValue(dictionary, "durationMs", "duration"));
            return true;
        }

        return false;
    }

    private static AlertKind? ReadKind(object? value)
    {
        return IsMissing(value) ? null : AlertRequest.ParseKind(ConvertToString(value, "type"));
    }

    private static int? ReadDuration(object? value)
    {
        return IsMissing(value) ? null : ConvertToDurationMs(value, "durationMs");
    }

    private static object? GetFirstScriptProperty(ScriptObject scriptObject, params string[] names)
    {
        var propertyNames = scriptObject.PropertyNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            var actualName = propertyNames.FirstOrDefault(propertyName => string.Equals(propertyName, name, StringComparison.OrdinalIgnoreCase));
            if (actualName is not null)
            {
                return scriptObject.GetProperty(actualName);
            }
        }

        return null;
    }

    private static object? GetFirstDictionaryValue(IReadOnlyDictionary<string, object?> dictionary, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var item in dictionary)
            {
                if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value;
                }
            }
        }

        return null;
    }

    private static object? GetFirstDictionaryValue(IDictionary dictionary, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (DictionaryEntry item in dictionary)
            {
                if (item.Key is string key && string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value;
                }
            }
        }

        return null;
    }

    private static string ConvertToString(object? value, string argumentName)
    {
        if (IsMissing(value))
        {
            throw new ArgumentException($"{argumentName} is required.", argumentName);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int ConvertToDurationMs(object? value, string argumentName)
    {
        if (IsMissing(value))
        {
            return AlertRequest.DefaultDurationMs;
        }

        try
        {
            var durationMs = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (durationMs < 0)
            {
                throw new ArgumentOutOfRangeException(argumentName, "Alert duration cannot be negative.");
            }

            return durationMs;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException($"{argumentName} must be a number of milliseconds.", argumentName, exception);
        }
        catch (InvalidCastException exception)
        {
            throw new ArgumentException($"{argumentName} must be a number of milliseconds.", argumentName, exception);
        }
    }

    private static bool IsMissing(object? value)
    {
        return value is null || ReferenceEquals(value, Undefined.Value);
    }
}
