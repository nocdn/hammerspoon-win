using HsWin.Core.Hotkeys;

namespace HsWin.Core.Keyboard;

public sealed record KeyboardTapOptions(
    HotkeyModifiers SuppressPhysicalModifiers,
    HotkeyModifiers Modifiers,
    KeyboardInputMethod InputMethod = KeyboardInputMethod.SendInput)
{
    public static KeyboardTapOptions Default { get; } =
        new(HotkeyModifiers.None, HotkeyModifiers.None, KeyboardInputMethod.SendInput);
}
