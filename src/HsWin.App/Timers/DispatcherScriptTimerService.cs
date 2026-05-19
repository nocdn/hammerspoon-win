using System.Windows.Threading;
using HsWin.Core.Timers;

namespace HsWin.App.Timers;

internal sealed class DispatcherScriptTimerService : IScriptTimerService
{
    private readonly Dispatcher _dispatcher;

    public DispatcherScriptTimerService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public IDisposable DoAfter(int delayMs, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = CreateTimer(delayMs, callback, repeats: false);
        timer.Start();
        return new TimerHandle(timer);
    }

    public IDisposable DoEvery(int intervalMs, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = CreateTimer(intervalMs, callback, repeats: true);
        timer.Start();
        return new TimerHandle(timer);
    }

    private DispatcherTimer CreateTimer(int intervalMs, Action callback, bool repeats)
    {
        if (intervalMs < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalMs), "Timer interval must be at least 1 millisecond.");
        }

        var timer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(intervalMs)
        };

        timer.Tick += (_, _) =>
        {
            if (!repeats)
            {
                timer.Stop();
            }

            callback();
        };

        return timer;
    }

    private sealed class TimerHandle : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private bool _disposed;

        public TimerHandle(DispatcherTimer timer)
        {
            _timer = timer;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _timer.Stop();
            _disposed = true;
        }
    }
}
