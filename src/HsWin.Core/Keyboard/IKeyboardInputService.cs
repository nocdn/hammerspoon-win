namespace HsWin.Core.Keyboard;

public interface IKeyboardInputService
{
    void KeyDown(uint virtualKey);

    void KeyUp(uint virtualKey);

    void Tap(uint virtualKey, KeyboardTapOptions options);

    IDisposable Repeat(uint virtualKey, KeyboardRepeatOptions options);

    /// <summary>
    /// Stops the active global keyboard-repeat session, if any. Safe to call when idle.
    /// </summary>
    void StopActiveRepeat();
}
