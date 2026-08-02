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

    public IKeyboardRepeatSession Repeat(uint virtualKey, KeyboardRepeatOptions options)
    {
        return new NullRepeatSession(options.IntervalMs);
    }

    public void StopActiveRepeat()
    {
    }

    private sealed class NullRepeatSession : IKeyboardRepeatSession
    {
        public NullRepeatSession(int intervalMs)
        {
            IntervalMs = intervalMs;
        }

        public int IntervalMs { get; private set; }

        public void SetIntervalMs(int intervalMs)
        {
            IntervalMs = intervalMs;
        }

        public void Dispose()
        {
        }
    }
}
