namespace HammerspoonWin.Core.Hotkeys;

public sealed record HotkeyDefinition(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public override string ToString()
    {
        return $"{Modifiers}+0x{VirtualKey:X2}";
    }
}
