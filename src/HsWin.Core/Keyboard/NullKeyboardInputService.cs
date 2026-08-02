namespace HsWin.Core.Keyboard;

public sealed class NullKeyboardInputService : IKeyboardInputService
{
    public static NullKeyboardInputService Instance { get; } = new();

    private NullKeyboardInputService()
    {
    }

    public void KeyDown(uint virtualKey)
    {
    }

    public void KeyUp(uint virtualKey)
    {
    }

    public void Tap(uint virtualKey, KeyboardTapOptions options)
    {
    }

    public IDisposable Repeat(uint virtualKey, KeyboardRepeatOptions options)
    {
        return new NullDisposable();
    }

    public void StopActiveRepeat()
    {
    }

    private sealed class NullDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
