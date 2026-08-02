using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Mouse;

/// <summary>
/// Dispatches mouse-scroll watch subscriptions.
/// <para>
/// Critical safety rule: script callbacks never run on the low-level mouse hook thread.
/// When <see cref="MouseScrollWatchOptions.Blocking"/> / preventDefault is enabled, matching
/// events are swallowed natively on the hook path and the script callback is scheduled off-hook.
/// That keeps physical input responsive and prevents global script-lock deadlocks with hotkey release.
/// </para>
/// </summary>
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
        var shouldSwallow = false;

        foreach (var subscription in subscriptions)
        {
            if (ShouldSkip(subscription, snapshot))
            {
                continue;
            }

            if (subscription.Options.Blocking)
            {
                // Native swallow only — never invoke JavaScript on the hook thread.
                // Avoid per-notch Info logging: high-resolution wheels would flood the runtime log.
                shouldSwallow = true;
            }

            ScheduleCallback(subscription, snapshot);
        }

        return shouldSwallow;
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

    private void ScheduleCallback(MouseScrollWatchSubscription subscription, MouseScrollEventSnapshot snapshot)
    {
        try
        {
            _scheduler.Schedule(() => InvokeCallback(subscription, snapshot));
        }
        catch (Exception exception)
        {
            _logger.Error($"Mouse scroll watch callback scheduling failed id={subscription.Id}.", exception);
        }
    }

    private void InvokeCallback(MouseScrollWatchSubscription subscription, MouseScrollEventSnapshot snapshot)
    {
        if (subscription.IsDisposed)
        {
            return;
        }

        try
        {
            // Return value is ignored for preventDefault watchers (host already swallowed natively).
            // For non-blocking watchers, true is only a usage warning.
            var requestedSwallow = subscription.Callback(snapshot);
            if (requestedSwallow && !subscription.Options.Blocking)
            {
                _logger.Warning(
                    $"Mouse scroll watch callback returned true for non-blocking watcher id={subscription.Id}; " +
                    "use {{ preventDefault: true }} so matching scroll events are swallowed while the watcher is registered.");
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
