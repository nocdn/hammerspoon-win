using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Mouse;

internal sealed class MouseScrollWatchDispatcher
{
    private readonly IRuntimeLogger _logger;
    private readonly IMouseScrollWatchCallbackScheduler _scheduler;

    public MouseScrollWatchDispatcher(
        IRuntimeLogger logger,
        IMouseScrollWatchCallbackScheduler scheduler)
    {
        _logger = logger;
        _scheduler = scheduler;
    }

    public bool Dispatch(
        MouseScrollEventSnapshot snapshot,
        IReadOnlyList<MouseScrollWatchSubscription> subscriptions)
    {
        foreach (var subscription in subscriptions)
        {
            if (ShouldSkip(subscription, snapshot))
            {
                continue;
            }

            if (subscription.Options.Blocking)
            {
                if (InvokeBlocking(subscription, snapshot))
                {
                    return true;
                }

                continue;
            }

            ScheduleNonBlocking(subscription, snapshot);
        }

        return false;
    }

    private static bool ShouldSkip(MouseScrollWatchSubscription subscription, MouseScrollEventSnapshot snapshot)
    {
        if (subscription.IsDisposed)
        {
            return true;
        }

        if (snapshot.IsInjected && !subscription.Options.IncludeInjected)
        {
            return true;
        }

        var axis = snapshot.IsVertical ? MouseScrollAxis.Vertical : MouseScrollAxis.Horizontal;
        return !subscription.Options.IncludesAxis(axis);
    }

    private bool InvokeBlocking(MouseScrollWatchSubscription subscription, MouseScrollEventSnapshot snapshot)
    {
        try
        {
            var shouldSwallow = subscription.Callback(snapshot);
            if (shouldSwallow)
            {
                _logger.Info(
                    $"Mouse scroll watch requested swallow id={subscription.Id} axis='{snapshot.Axis}' " +
                    $"direction='{snapshot.Direction}' delta={snapshot.Delta}.");
            }

            return shouldSwallow;
        }
        catch (Exception exception)
        {
            _logger.Error($"Mouse scroll watch callback failed id={subscription.Id}.", exception);
            return false;
        }
    }

    private void ScheduleNonBlocking(MouseScrollWatchSubscription subscription, MouseScrollEventSnapshot snapshot)
    {
        try
        {
            _scheduler.Schedule(() => InvokeNonBlocking(subscription, snapshot));
        }
        catch (Exception exception)
        {
            _logger.Error($"Mouse scroll watch callback scheduling failed id={subscription.Id}.", exception);
        }
    }

    private void InvokeNonBlocking(MouseScrollWatchSubscription subscription, MouseScrollEventSnapshot snapshot)
    {
        if (subscription.IsDisposed)
        {
            return;
        }

        try
        {
            if (subscription.Callback(snapshot))
            {
                _logger.Warning(
                    $"Mouse scroll watch callback returned true for non-blocking watcher id={subscription.Id}; use blocking=true to swallow input.");
            }
        }
        catch (Exception exception)
        {
            _logger.Error($"Mouse scroll watch callback failed id={subscription.Id}.", exception);
        }
    }
}

internal interface IMouseScrollWatchCallbackScheduler
{
    void Schedule(Action callback);
}

internal sealed class SynchronizationContextMouseScrollWatchCallbackScheduler : IMouseScrollWatchCallbackScheduler
{
    private readonly SynchronizationContext? _context;

    public SynchronizationContextMouseScrollWatchCallbackScheduler(SynchronizationContext? context)
    {
        _context = context;
    }

    public void Schedule(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (_context is null)
        {
            ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), callback);
            return;
        }

        _context.Post(static state => ((Action)state!).Invoke(), callback);
    }
}

internal sealed class MouseScrollWatchSubscription : IDisposable
{
    private readonly Action<MouseScrollWatchSubscription>? _dispose;
    private int _disposed;

    public MouseScrollWatchSubscription(
        long id,
        MouseScrollWatchOptions options,
        Func<MouseScrollEventSnapshot, bool> callback,
        Action<MouseScrollWatchSubscription>? dispose = null)
    {
        Id = id;
        Options = options;
        Callback = callback;
        _dispose = dispose;
    }

    public long Id { get; }

    public MouseScrollWatchOptions Options { get; }

    public Func<MouseScrollEventSnapshot, bool> Callback { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _dispose?.Invoke(this);
    }

    public void MarkDisposed()
    {
        Interlocked.Exchange(ref _disposed, 1);
    }
}
