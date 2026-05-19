namespace HsWin.Core.Hotkeys;

public interface IHotkeyRegistrar
{
    IDisposable Register(HotkeyDefinition hotkey, Action pressed);
}
