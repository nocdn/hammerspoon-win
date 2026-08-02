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
        var blocking = ScriptArgumentReader.GetPropertyValue(
            value,
            "blocking",
            "synchronous",
            "sync",
            "swallow",
            "preventDefault",
            "prevent",
            "capture");
        var keys = ScriptArgumentReader.GetPropertyValue(value, "keys", "key", "keyCodes", "keyCode");
        return new KeyboardEventWatchOptions(
            ScriptArgumentReader.IsMissing(includeInjected)
                ? KeyboardEventWatchOptions.Default.IncludeInjected
                : Convert.ToBoolean(includeInjected, CultureInfo.InvariantCulture),
            ScriptArgumentReader.IsMissing(blocking)
                ? KeyboardEventWatchOptions.Default.Blocking
                : Convert.ToBoolean(blocking, CultureInfo.InvariantCulture),
            ParseKeyFilter(keys));
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
        var modifiersValue = ScriptArgumentReader.GetPropertyValue(
            value,
            "modifiers",
            "withModifiers",
            "holdModifiers");
        var inputMethod = KeyboardInputMethodParser.Parse(
            ScriptArgumentReader.GetPropertyValue(value, "inputMethod", "method"));

        return new KeyboardTapOptions(
            ScriptArgumentReader.IsMissing(suppressValue)
                ? KeyboardTapOptions.Default.SuppressPhysicalModifiers
                : HotkeyParser.ParseModifiers(suppressValue),
            ScriptArgumentReader.IsMissing(modifiersValue)
                ? KeyboardTapOptions.Default.Modifiers
                : HotkeyParser.ParseModifiers(modifiersValue),
            inputMethod);
    }

    public static KeyboardRepeatOptions ParseRepeatOptions(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return KeyboardRepeatOptions.Default;
        }

        var intervalValue = ScriptArgumentReader.GetPropertyValue(value, "intervalMs", "interval");
        var keyDownValue = ScriptArgumentReader.GetPropertyValue(
            value,
            "keyDownMs",
            "holdMs",
            "pressDurationMs");
        var tapOptions = ParseTapOptions(value);
        var intervalMs = ScriptArgumentReader.IsMissing(intervalValue)
            ? KeyboardRepeatOptions.Default.IntervalMs
            : ConvertToRepeatInterval(intervalValue!);
        var keyDownMs = ScriptArgumentReader.IsMissing(keyDownValue)
            ? KeyboardRepeatOptions.Default.KeyDownMs
            : ConvertToKeyDownDuration(keyDownValue!, intervalMs);

        return new KeyboardRepeatOptions(
            intervalMs,
            tapOptions.SuppressPhysicalModifiers,
            tapOptions.InputMethod,
            keyDownMs);
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

    private static int ConvertToKeyDownDuration(object value, int intervalMs)
    {
        try
        {
            var keyDownMs = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (keyDownMs < 0 || keyDownMs >= intervalMs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Keyboard repeat keyDownMs must be at least 0 and less than intervalMs ({intervalMs}).");
            }

            return keyDownMs;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("keyDownMs must be a number of milliseconds.", nameof(value), exception);
        }
        catch (InvalidCastException exception)
        {
            throw new ArgumentException("keyDownMs must be a number of milliseconds.", nameof(value), exception);
        }
    }

    private static IReadOnlySet<uint>? ParseKeyFilter(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return null;
        }

        var keys = new HashSet<uint>();
        if (value is string or int or uint)
        {
            keys.Add(HotkeyParser.ParseVirtualKey(value));
            return keys;
        }

        foreach (var item in ScriptArgumentReader.EnumerateIndexedValues(value))
        {
            keys.Add(HotkeyParser.ParseVirtualKey(item));
        }

        if (keys.Count == 0)
        {
            keys.Add(HotkeyParser.ParseVirtualKey(value));
        }

        return keys;
    }
}
