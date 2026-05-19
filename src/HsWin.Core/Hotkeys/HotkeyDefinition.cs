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

        return $"{Modifiers}+{FormatVirtualKey(VirtualKey)}";
    }

    private static string FormatVirtualKey(uint virtualKey)
    {
        if (virtualKey is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return $"F{virtualKey - 0x70 + 1}";
        }

        return $"0x{virtualKey:X2}";
    }
}
