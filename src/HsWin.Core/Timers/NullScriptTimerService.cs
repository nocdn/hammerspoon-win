namespace HsWin.Core.Timers;

public sealed class NullScriptTimerService : IScriptTimerService
{
    public static NullScriptTimerService Instance { get; } = new();

    private NullScriptTimerService()
    {
    }

    public IDisposable DoAfter(int delayMs, Action callback)
    {
        return new NullDisposable();
    }

    public IDisposable DoEvery(int intervalMs, Action callback)
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
