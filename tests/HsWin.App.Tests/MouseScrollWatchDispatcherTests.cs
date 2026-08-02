using HsWin.App.Mouse;
using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Tests;

public sealed class MouseScrollWatchDispatcherTests
{
    [Fact]
    public void NonBlockingWatcherSchedulesCallbackAndDoesNotSwallowImmediately()
    {
        var scheduler = new CapturingScheduler();
        var logger = new CapturingRuntimeLogger();
        var dispatcher = new MouseScrollWatchDispatcher(logger, scheduler);
        var called = false;
        var subscription = CreateSubscription(blocking: false, _ =>
        {
            called = true;
            return true;
        });

        var shouldSwallow = dispatcher.Dispatch(CreateSnapshot(), [subscription]);

        Assert.False(shouldSwallow);
        Assert.False(called);
        var scheduled = Assert.Single(scheduler.Callbacks);

        scheduled();

        Assert.True(called);
        Assert.Contains(logger.Warnings, warning => warning.Contains("non-blocking watcher", StringComparison.Ordinal));
    }

    [Fact]
    public void BlockingWatcherRunsInlineAndCanSwallow()
    {
        var scheduler = new CapturingScheduler();
        var dispatcher = new MouseScrollWatchDispatcher(new CapturingRuntimeLogger(), scheduler);
        var called = false;
        var subscription = CreateSubscription(blocking: true, _ =>
        {
            called = true;
            return true;
        });

        var shouldSwallow = dispatcher.Dispatch(CreateSnapshot(), [subscription]);

        Assert.True(shouldSwallow);
        Assert.True(called);
        Assert.Empty(scheduler.Callbacks);
    }

    [Fact]
    public void SkipsAxesOutsideWatcherFilter()
    {
        var scheduler = new CapturingScheduler();
        var dispatcher = new MouseScrollWatchDispatcher(new CapturingRuntimeLogger(), scheduler);
        var subscription = CreateSubscription(
            blocking: true,
            _ => true,
            axes: MouseScrollAxis.Horizontal);

        var shouldSwallow = dispatcher.Dispatch(CreateSnapshot(vertical: true), [subscription]);

        Assert.False(shouldSwallow);
        Assert.Empty(scheduler.Callbacks);
    }

    [Fact]
    public void SkipsInjectedEventsUnlessRequested()
    {
        var scheduler = new CapturingScheduler();
        var dispatcher = new MouseScrollWatchDispatcher(new CapturingRuntimeLogger(), scheduler);
        var subscription = CreateSubscription(blocking: true, _ => true, includeInjected: false);

        var shouldSwallow = dispatcher.Dispatch(CreateSnapshot(isInjected: true), [subscription]);

        Assert.False(shouldSwallow);
    }

    [Fact]
    public void BlockingSwallowStopsLaterWatchers()
    {
        var scheduler = new CapturingScheduler();
        var dispatcher = new MouseScrollWatchDispatcher(new CapturingRuntimeLogger(), scheduler);
        var secondCalled = false;
        var first = CreateSubscription(blocking: true, _ => true, id: 1);
        var second = CreateSubscription(blocking: true, _ =>
        {
            secondCalled = true;
            return false;
        }, id: 2);

        var shouldSwallow = dispatcher.Dispatch(CreateSnapshot(), [first, second]);

        Assert.True(shouldSwallow);
        Assert.False(secondCalled);
    }

    [Fact]
    public void NonBlockingBeforeBlockerStillSchedules()
    {
        var scheduler = new CapturingScheduler();
        var dispatcher = new MouseScrollWatchDispatcher(new CapturingRuntimeLogger(), scheduler);
        var nonBlocking = CreateSubscription(blocking: false, _ => false, id: 1);
        var blocking = CreateSubscription(blocking: true, _ => true, id: 2);

        var shouldSwallow = dispatcher.Dispatch(CreateSnapshot(), [nonBlocking, blocking]);

        Assert.True(shouldSwallow);
        Assert.Single(scheduler.Callbacks);
    }

    [Fact]
    public void IncludeInjectedDeliversInjectedEvents()
    {
        var scheduler = new CapturingScheduler();
        var dispatcher = new MouseScrollWatchDispatcher(new CapturingRuntimeLogger(), scheduler);
        var called = false;
        var subscription = CreateSubscription(blocking: true, _ =>
        {
            called = true;
            return false;
        }, includeInjected: true);

        var shouldSwallow = dispatcher.Dispatch(CreateSnapshot(isInjected: true), [subscription]);

        Assert.False(shouldSwallow);
        Assert.True(called);
    }

    private static MouseScrollWatchSubscription CreateSubscription(
        bool blocking,
        Func<MouseScrollEventSnapshot, bool> callback,
        MouseScrollAxis axes = MouseScrollAxis.Both,
        bool includeInjected = false,
        long id = 1)
    {
        return new MouseScrollWatchSubscription(
            id,
            new MouseScrollWatchOptions(includeInjected, blocking, axes),
            callback);
    }

    private static MouseScrollEventSnapshot CreateSnapshot(bool vertical = true, bool isInjected = false)
    {
        return new MouseScrollEventSnapshot(
            MouseScrollEventSnapshot.ScrollType,
            vertical ? MouseScrollEventSnapshot.VerticalAxis : MouseScrollEventSnapshot.HorizontalAxis,
            vertical ? MouseScrollEventSnapshot.DirectionUp : MouseScrollEventSnapshot.DirectionRight,
            Delta: 120,
            Notches: 1,
            IsVertical: vertical,
            IsHorizontal: !vertical,
            IsInjected: isInjected,
            Modifiers: [],
            ModifierFlags: 0,
            X: 1,
            Y: 2);
    }

    private sealed class CapturingScheduler : IMouseScrollWatchCallbackScheduler
    {
        public List<Action> Callbacks { get; } = [];

        public void Schedule(Action callback)
        {
            Callbacks.Add(callback);
        }
    }

    private sealed class CapturingRuntimeLogger : IRuntimeLogger
    {
        public List<string> Warnings { get; } = [];

        public void Info(string message)
        {
        }

        public void Warning(string message) => Warnings.Add(message);

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
