using HsWin.Core.Scripting;

namespace HsWin.Core.Keyboard;

public static class KeyboardInputMethodParser
{
    public static KeyboardInputMethod Parse(object? value)
    {
        if (ScriptArgumentReader.IsMissing(value))
        {
            return KeyboardInputMethod.SendInput;
        }

        var method = ScriptArgumentReader.RequireNonWhiteSpaceString(value, "inputMethod");
        return method.Trim().ToLowerInvariant() switch
        {
            "sendinput" or "send-input" or "global" => KeyboardInputMethod.SendInput,
            "windowmessage" or "window-message" or "postmessage" or "window" => KeyboardInputMethod.WindowMessage,
            _ => throw new ArgumentException(
                "inputMethod must be 'sendInput' or 'windowMessage'.",
                nameof(value))
        };
    }

    public static string GetDisplayName(KeyboardInputMethod method)
    {
        return method switch
        {
            KeyboardInputMethod.SendInput => "sendInput",
            KeyboardInputMethod.WindowMessage => "windowMessage",
            _ => method.ToString()
        };
    }
}
