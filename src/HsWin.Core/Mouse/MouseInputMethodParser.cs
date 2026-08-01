using HsWin.Core.Scripting;

namespace HsWin.Core.Mouse;

public static class MouseInputMethodParser
{
    public static MouseInputMethod Parse(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return MouseInputMethod.SendInput;
        }

        var method = ScriptArgumentReader.RequireNonWhiteSpaceString(value, "inputMethod");
        return method.Trim().ToLowerInvariant() switch
        {
            "sendinput" or "send-input" or "global" => MouseInputMethod.SendInput,
            "windowmessage" or "window-message" or "postmessage" or "window" => MouseInputMethod.WindowMessage,
            _ => throw new ArgumentException(
                "inputMethod must be 'sendInput' or 'windowMessage'.",
                nameof(value))
        };
    }

    public static string GetDisplayName(MouseInputMethod method)
    {
        return method switch
        {
            MouseInputMethod.SendInput => "sendInput",
            MouseInputMethod.WindowMessage => "windowMessage",
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported mouse input method.")
        };
    }
}
