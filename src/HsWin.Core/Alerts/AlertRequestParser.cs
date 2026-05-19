using HsWin.Core.Scripting;

namespace HsWin.Core.Alerts;

public static class AlertRequestParser
{
    public static AlertRequest FromScriptArguments(object? text, object? optionsOrKind = null, object? durationMs = null)
    {
        var message = ScriptArgumentReader.RequireText(text, "text");
        var kind = AlertRequest.DefaultKind;
        int? duration = null;

        if (!ScriptArgumentReader.IsMissing(optionsOrKind))
        {
            if (TryReadOptions(optionsOrKind, out var optionsKind, out var optionsDurationMs))
            {
                kind = optionsKind ?? kind;
                duration = optionsDurationMs;
            }
            else
            {
                kind = AlertRequest.ParseKind(ScriptArgumentReader.RequireText(optionsOrKind, "type"));
            }
        }

        if (!ScriptArgumentReader.IsMissing(durationMs))
        {
            duration = ConvertToDurationMs(durationMs, "durationMs");
        }

        return AlertRequest.Create(message, kind, duration);
    }

    private static bool TryReadOptions(object? value, out AlertKind? kind, out int? durationMs)
    {
        kind = null;
        durationMs = null;
        if (!ScriptArgumentReader.IsOptionsObject(value))
        {
            return false;
        }

        kind = ReadKind(ScriptArgumentReader.GetPropertyValue(value, "type", "kind", "state", "status"));
        durationMs = ReadDuration(ScriptArgumentReader.GetPropertyValue(value, "durationMs", "duration"));
        return true;
    }

    private static AlertKind? ReadKind(object? value)
    {
        return ScriptArgumentReader.IsMissing(value) ? null : AlertRequest.ParseKind(ScriptArgumentReader.RequireText(value, "type"));
    }

    private static int? ReadDuration(object? value)
    {
        return ScriptArgumentReader.IsMissing(value) ? null : ConvertToDurationMs(value, "durationMs");
    }

    private static int ConvertToDurationMs(object? value, string argumentName)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return AlertRequest.DefaultDurationMs;
        }

        var durationMs = ScriptArgumentReader.RequireInt32(value, argumentName, "a number of milliseconds");
        if (durationMs < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Alert duration cannot be negative.");
        }

        return durationMs;
    }
}
