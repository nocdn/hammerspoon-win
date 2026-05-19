namespace HsWin.Core.Keyboard;

public interface IKeyboardEventService
{
    IDisposable Watch(KeyboardEventWatchOptions options, Func<KeyboardEventSnapshot, bool> callback);

    bool IsKeyDown(uint virtualKey);
}
