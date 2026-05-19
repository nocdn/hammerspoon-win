using System.Collections;
using System.Globalization;
using HsWin.Core.Hotkeys;
using Microsoft.ClearScript;

namespace HsWin.Core.Keyboard;

public static class KeyboardScriptOptionsParser
{
    public static KeyboardEventWatchOptions ParseWatchOptions(object? value)
    {
        if (IsMissing(value))
        {
            return KeyboardEventWatchOptions.Default;
        }

        var includeInjected = GetPropertyValue(value!, "includeInjected");
        return new KeyboardEventWatchOptions(
            IsMissing(includeInjected)
                ? KeyboardEventWatchOptions.Default.IncludeInjected
                : Convert.ToBoolean(includeInjected, CultureInfo.InvariantCulture));
    }

    public static KeyboardTapOptions ParseTapOptions(object? value)
    {
        if (IsMissing(value))
        {
            return KeyboardTapOptions.Default;
        }

        var suppressValue = GetPropertyValue(value!, "suppressPhysicalModifiers")
            ?? GetPropertyValue(value!, "suppressModifiers")
            ?? GetPropertyValue(value!, "withoutModifiers");

        return new KeyboardTapOptions(
            IsMissing(suppressValue)
                ? KeyboardTapOptions.Default.SuppressPhysicalModifiers
                : HotkeyParser.ParseModifiers(suppressValue));
    }

    public static KeyboardRepeatOptions ParseRepeatOptions(object? value)
    {
        if (IsMissing(value))
        {
            return KeyboardRepeatOptions.Default;
        }

        var intervalValue = GetPropertyValue(value!, "intervalMs")
            ?? GetPropertyValue(value!, "interval");
        var tapOptions = ParseTapOptions(value);
        var intervalMs = IsMissing(intervalValue)
            ? KeyboardRepeatOptions.Default.IntervalMs
            : ConvertToRepeatInterval(intervalValue!);

        return new KeyboardRepeatOptions(intervalMs, tapOptions.SuppressPhysicalModifiers);
    }

    private static int ConvertToRepeatInterval(object value)
    {
        try
        {
            var intervalMs = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (intervalMs is < KeyboardRepeatOptions.MinimumIntervalMs or > KeyboardRepeatOptions.MaximumIntervalMs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Keyboard repeat interval must be between {KeyboardRepeatOptions.MinimumIntervalMs} and {KeyboardRepeatOptions.MaximumIntervalMs} milliseconds.");
            }

            return intervalMs;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("intervalMs must be a number of milliseconds.", nameof(value), exception);
        }
        catch (InvalidCastException exception)
        {
            throw new ArgumentException("intervalMs must be a number of milliseconds.", nameof(value), exception);
        }
    }

    private static object? GetPropertyValue(object value, string name)
    {
        if (value is ScriptObject scriptObject)
        {
            var actualName = scriptObject.PropertyNames
                .FirstOrDefault(propertyName => string.Equals(propertyName, name, StringComparison.OrdinalIgnoreCase));

            return actualName is null ? null : scriptObject.GetProperty(actualName);
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var item in readOnlyDictionary)
            {
                if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value;
                }
            }
        }

        if (value is IDictionary dictionary)
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

    private static bool IsMissing(object? value)
    {
        return value is null || ReferenceEquals(value, Undefined.Value);
    }
}
