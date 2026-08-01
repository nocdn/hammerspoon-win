namespace HsWin.Core.Hotkeys;

public interface IHotkeyRegistrar
{
    IDisposable Register(HotkeyDefinition hotkey, Action pressed);

    IDisposable RegisterHeld(HotkeyDefinition hotkey, Action pressed, Action released, bool blocking);
}
