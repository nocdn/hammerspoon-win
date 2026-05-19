namespace HsWin.Core.Hotkeys;

public sealed record HotkeyDefinition(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public HotkeyInputKind InputKind { get; init; } = HotkeyInputKind.Keyboard;

    public HotkeyMouseButton? MouseButton { get; init; }

    public static HotkeyDefinition CreateKeyboard(HotkeyModifiers modifiers, uint virtualKey)
    {
        return new HotkeyDefinition(modifiers, virtualKey);
    }

    public static HotkeyDefinition CreateMouseButton(HotkeyModifiers modifiers, HotkeyMouseButton mouseButton)
    {
        return new HotkeyDefinition(modifiers, 0)
        {
            InputKind = HotkeyInputKind.MouseButton,
            MouseButton = mouseButton
        };
    }

    public override string ToString()
    {
        if (InputKind == HotkeyInputKind.MouseButton)
        {
            return $"{Modifiers}+{MouseButton}";
        }

        return $"{Modifiers}+0x{VirtualKey:X2}";
    }
}
