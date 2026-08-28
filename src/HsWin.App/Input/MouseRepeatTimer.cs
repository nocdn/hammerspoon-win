namespace HsWin.App.Input;

internal interface IMouseRepeatTimerFactory
{
    IMouseRepeatTimer Create(Action tick);
}

internal interface IMouseRepeatTimer : IDisposable
{
    void Change(int dueTimeMs, int periodMs);
}

internal sealed class SystemMouseRepeatTimerFactory : IMouseRepeatTimerFactory
{
    public static readonly SystemMouseRepeatTimerFactory Instance = new();

    private SystemMouseRepeatTimerFactory()
    {
    }

    public IMouseRepeatTimer Create(Action tick)
    {
        return new SystemMouseRepeatTimer(tick);
    }

    private sealed class SystemMouseRepeatTimer : IMouseRepeatTimer
    {
        private readonly System.Threading.Timer _timer;

        public SystemMouseRepeatTimer(Action tick)
        {
            ArgumentNullException.ThrowIfNull(tick);
            _timer = new System.Threading.Timer(_ => tick(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Change(int dueTimeMs, int periodMs)
        {
            _timer.Change(dueTimeMs, periodMs);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
