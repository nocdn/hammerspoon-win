using HsWin.Core.Keyboard;
using HsWin.Core.Logging;

namespace HsWin.App.Keyboard;

internal sealed class KeyboardWatchDispatcher
{
    private readonly IRuntimeLogger _logger;
    private readonly IKeyboardWatchCallbackScheduler _scheduler;

    public KeyboardWatchDispatcher(
        IRuntimeLogger logger,
        IKeyboardWatchCallbackScheduler scheduler)
    {
        _logger = logger;
        _scheduler = scheduler;
    }

    public bool Dispatch(
        KeyboardEventSnapshot snapshot,
        IReadOnlyList<KeyboardWatchSubscription> subscriptions)
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

    private static bool ShouldSkip(KeyboardWatchSubscription subscription, KeyboardEventSnapshot snapshot)
    {
        return subscription.IsDisposed
            || (snapshot.IsInjected && !subscription.Options.IncludeInjected)
            || (subscription.Options.KeyFilter is { Count: > 0 } && !subscription.Options.KeyFilter.Contains(snapshot.KeyCode));
    }

    private bool InvokeBlocking(KeyboardWatchSubscription subscription, KeyboardEventSnapshot snapshot)
    {
        try
        {
            var shouldSwallow = subscription.Callback(snapshot);
            if (shouldSwallow)
            {
                _logger.Info(
                    $"Keyboard watch requested swallow id={subscription.Id} key='{snapshot.Key}' type='{snapshot.Type}' " +
                    $"vk=0x{snapshot.KeyCode:X2} modifiers='{string.Join(",", snapshot.Modifiers)}'.");
            }

            return shouldSwallow;
        }
        catch (Exception exception)
        {
            _logger.Error($"Keyboard watch callback failed id={subscription.Id}.", exception);
            return false;
        }
    }

    private void ScheduleNonBlocking(KeyboardWatchSubscription subscription, KeyboardEventSnapshot snapshot)
    {
        try
        {
            _scheduler.Schedule(() => InvokeNonBlocking(subscription, snapshot));
        }
        catch (Exception exception)
        {
            _logger.Error($"Keyboard watch callback scheduling failed id={subscription.Id}.", exception);
        }
    }

    private void InvokeNonBlocking(KeyboardWatchSubscription subscription, KeyboardEventSnapshot snapshot)
    {
        if (subscription.IsDisposed)
        {
            return;
        }

        try
        {
            if (subscription.Callback(snapshot))
            {
                _logger.Warning($"Keyboard watch callback returned true for non-blocking watcher id={subscription.Id}; use blocking=true to swallow input.");
            }
        }
        catch (Exception exception)
        {
            _logger.Error($"Keyboard watch callback failed id={subscription.Id}.", exception);
        }
    }
}

internal interface IKeyboardWatchCallbackScheduler
{
    void Schedule(Action callback);
}

internal sealed class SynchronizationContextKeyboardWatchCallbackScheduler : IKeyboardWatchCallbackScheduler
{
    private readonly SynchronizationContext? _context;

    public SynchronizationContextKeyboardWatchCallbackScheduler(SynchronizationContext? context)
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

internal sealed class KeyboardWatchSubscription : IDisposable
{
    private readonly Action<KeyboardWatchSubscription>? _dispose;
    private int _disposed;

    public KeyboardWatchSubscription(
        long id,
        KeyboardEventWatchOptions options,
        Func<KeyboardEventSnapshot, bool> callback,
        Action<KeyboardWatchSubscription>? dispose = null)
    {
        Id = id;
        Options = options;
        Callback = callback;
        _dispose = dispose;
    }

    public long Id { get; }

    public KeyboardEventWatchOptions Options { get; }

    public Func<KeyboardEventSnapshot, bool> Callback { get; }

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
