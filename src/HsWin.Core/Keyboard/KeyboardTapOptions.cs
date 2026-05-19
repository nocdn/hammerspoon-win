using HsWin.Core.Hotkeys;

namespace HsWin.Core.Keyboard;

public sealed record KeyboardTapOptions(HotkeyModifiers SuppressPhysicalModifiers)
{
    public static KeyboardTapOptions Default { get; } = new(HotkeyModifiers.None);
}
