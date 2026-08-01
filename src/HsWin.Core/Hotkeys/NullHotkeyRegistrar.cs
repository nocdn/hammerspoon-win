namespace HsWin.Core.Hotkeys;

public sealed class NullHotkeyRegistrar : IHotkeyRegistrar
{
    public static NullHotkeyRegistrar Instance { get; } = new();

    private NullHotkeyRegistrar()
    {
    }

    public IDisposable Register(HotkeyDefinition hotkey, Action pressed)
    {
        throw new NotSupportedException("Hotkeys are not available in this runtime.");
    }

    public IDisposable RegisterHeld(HotkeyDefinition hotkey, Action pressed, Action released, bool blocking)
    {
        throw new NotSupportedException("Hotkeys are not available in this runtime.");
    }
}
