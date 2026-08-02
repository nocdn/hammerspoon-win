namespace HsWin.Core.Mouse;

public sealed class NullMouseEventService : IMouseEventService
{
    public static NullMouseEventService Instance { get; } = new();

    private NullMouseEventService()
    {
    }

    public IDisposable WatchScroll(MouseScrollWatchOptions options, Func<MouseScrollEventSnapshot, bool> callback)
    {
        return new NullDisposable();
    }

    private sealed class NullDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
