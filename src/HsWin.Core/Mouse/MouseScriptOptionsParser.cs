using System.Globalization;
using HsWin.Core.Scripting;

namespace HsWin.Core.Mouse;

public static class MouseScriptOptionsParser
{
    public static MouseRepeatOptions ParseRepeatOptions(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return MouseRepeatOptions.Default;
        }

        var intervalValue = ScriptArgumentReader.GetPropertyValue(value, "intervalMs", "interval");
        var intervalMs = ScriptArgumentReader.IsMissing(intervalValue)
            ? MouseRepeatOptions.Default.IntervalMs
            : ConvertToRepeatInterval(intervalValue!);

        var inputMethod = MouseInputMethodParser.Parse(
            ScriptArgumentReader.GetPropertyValue(value, "inputMethod", "method"));

        return new MouseRepeatOptions(intervalMs, inputMethod);
    }

    private static int ConvertToRepeatInterval(object value)
    {
        try
        {
            var intervalMs = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (intervalMs is < MouseRepeatOptions.MinimumIntervalMs or > MouseRepeatOptions.MaximumIntervalMs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Mouse repeat interval must be between {MouseRepeatOptions.MinimumIntervalMs} and {MouseRepeatOptions.MaximumIntervalMs} milliseconds.");
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
