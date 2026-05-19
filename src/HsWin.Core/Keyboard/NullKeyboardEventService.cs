namespace HsWin.Core.Keyboard;

public sealed class NullKeyboardEventService : IKeyboardEventService
{
    public static NullKeyboardEventService Instance { get; } = new();

    private NullKeyboardEventService()
    {
    }

    public IDisposable Watch(KeyboardEventWatchOptions options, Func<KeyboardEventSnapshot, bool> callback)
    {
        return new NullDisposable();
    }

    public bool IsKeyDown(uint virtualKey)
    {
        return false;
    }

    private sealed class NullDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
