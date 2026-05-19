using System.Globalization;
using HsWin.Core.Hotkeys;
using HsWin.Core.Scripting;

namespace HsWin.Core.Keyboard;

public static class KeyboardScriptOptionsParser
{
    public static KeyboardEventWatchOptions ParseWatchOptions(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return KeyboardEventWatchOptions.Default;
        }

        var includeInjected = ScriptArgumentReader.GetPropertyValue(value, "includeInjected");
        return new KeyboardEventWatchOptions(
            ScriptArgumentReader.IsMissing(includeInjected)
                ? KeyboardEventWatchOptions.Default.IncludeInjected
                : Convert.ToBoolean(includeInjected, CultureInfo.InvariantCulture));
    }

    public static KeyboardTapOptions ParseTapOptions(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return KeyboardTapOptions.Default;
        }

        var suppressValue = ScriptArgumentReader.GetPropertyValue(
            value,
            "suppressPhysicalModifiers",
            "suppressModifiers",
            "withoutModifiers");

        return new KeyboardTapOptions(
            ScriptArgumentReader.IsMissing(suppressValue)
                ? KeyboardTapOptions.Default.SuppressPhysicalModifiers
                : HotkeyParser.ParseModifiers(suppressValue));
    }

    public static KeyboardRepeatOptions ParseRepeatOptions(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return KeyboardRepeatOptions.Default;
        }

        var intervalValue = ScriptArgumentReader.GetPropertyValue(value, "intervalMs", "interval");
        var tapOptions = ParseTapOptions(value);
        var intervalMs = ScriptArgumentReader.IsMissing(intervalValue)
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
}
