using HsWin.Core.Scripting;

namespace HsWin.Core.Alerts;

public static class AlertRequestParser
{
    public static AlertRequest FromScriptArguments(object? text, object? optionsOrKind = null, object? durationMs = null)
    {
        var message = ScriptArgumentReader.RequireText(text, "text");
        var kind = AlertRequest.DefaultKind;
        var icon = AlertRequest.DefaultIcon;
        int? duration = null;

        if (!ScriptArgumentReader.IsMissing(optionsOrKind))
        {
            if (TryReadOptions(optionsOrKind, out var optionsKind, out var optionsDurationMs, out var optionsIcon))
            {
                kind = optionsKind ?? kind;
                duration = optionsDurationMs;
                icon = optionsIcon ?? icon;
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

        return AlertRequest.Create(message, kind, duration, icon);
    }

    private static bool TryReadOptions(object? value, out AlertKind? kind, out int? durationMs, out AlertIcon? icon)
    {
        kind = null;
        durationMs = null;
        icon = null;
        if (!ScriptArgumentReader.IsOptionsObject(value))
        {
            return false;
        }

        kind = ReadKind(ScriptArgumentReader.GetPropertyValue(value, "type", "kind", "state", "status"));
        durationMs = ReadDuration(ScriptArgumentReader.GetPropertyValue(value, "durationMs", "duration"));
        icon = ReadIcon(
            ScriptArgumentReader.GetPropertyValue(value, "icon", "indicator"),
            ScriptArgumentReader.GetPropertyValue(value, "loading", "loader", "spinner"));
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

    private static AlertIcon? ReadIcon(object? iconValue, object? loadingValue)
    {
        if (!ScriptArgumentReader.IsMissing(iconValue))
        {
            return AlertRequest.ParseIcon(ScriptArgumentReader.RequireText(iconValue, "icon"));
        }

        if (ScriptArgumentReader.IsMissing(loadingValue))
        {
            return null;
        }

        return ScriptArgumentReader.RequireBoolean(loadingValue, "loading")
            ? AlertIcon.Loader
            : AlertIcon.Auto;
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
