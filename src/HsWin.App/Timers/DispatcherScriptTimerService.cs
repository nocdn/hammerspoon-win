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

        if (!_dispatcher.CheckAccess())
        {
            return _dispatcher.Invoke(() => DoAfter(delayMs, callback));
        }

        var timer = CreateTimer(delayMs, callback, repeats: false);
        timer.Start();
        return new TimerHandle(_dispatcher, timer);
    }

    public IDisposable DoEvery(int intervalMs, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (!_dispatcher.CheckAccess())
        {
            return _dispatcher.Invoke(() => DoEvery(intervalMs, callback));
        }

        var timer = CreateTimer(intervalMs, callback, repeats: true);
        timer.Start();
        return new TimerHandle(_dispatcher, timer);
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
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _timer;
        private bool _disposed;

        public TimerHandle(Dispatcher dispatcher, DispatcherTimer timer)
        {
            _dispatcher = dispatcher;
            _timer = timer;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                StopTimer();
                return;
            }

            _dispatcher.Invoke(StopTimer);
        }

        private void StopTimer()
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
