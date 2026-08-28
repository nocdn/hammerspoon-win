using HsWin.App.Input;
using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Tests;

public sealed class MouseInputServiceTests
{
    [Fact]
    public void ClickSendsTheRequestedButton()
    {
        var sender = new CapturingMouseInputSender();
        var service = new MouseInputService(NullRuntimeLogger.Instance, sender);

        service.Click(MouseButton.Right);

        Assert.Equal([MouseButton.Right], sender.Clicks);
    }

    [Fact]
    public void RepeatStartsImmediatelyAndStopsAfterDispose()
    {
        var sender = new CapturingMouseInputSender();
        var service = new MouseInputService(NullRuntimeLogger.Instance, sender);

        using var repeat = service.Repeat(MouseButton.Right, new MouseRepeatOptions(10));

        Assert.True(sender.WaitForClickCount(2, TimeSpan.FromSeconds(2)));
        repeat.Dispose();
        var countAfterDispose = sender.Clicks.Count;

        Thread.Sleep(50);

        Assert.Equal(countAfterDispose, sender.Clicks.Count);
    }

    [Fact]
    public void RepeatPassesTheRequestedInputMethodToTheSender()
    {
        var sender = new CapturingMouseInputSender();
        var service = new MouseInputService(NullRuntimeLogger.Instance, sender);

        using var repeat = service.Repeat(
            MouseButton.Right,
            new MouseRepeatOptions(10, MouseInputMethod.WindowMessage));

        Assert.True(sender.WaitForClickCount(2, TimeSpan.FromSeconds(2)));
        repeat.Dispose();

        Assert.All(sender.InputMethods, method => Assert.Equal(MouseInputMethod.WindowMessage, method));
    }

    [Fact]
    public void SetIntervalMsChangesRateWithoutReplacingTheSession()
    {
        var sender = new CapturingMouseInputSender();
        var timers = new CapturingMouseRepeatTimerFactory();
        var service = new MouseInputService(NullRuntimeLogger.Instance, sender, timers);

        using var repeat = service.Repeat(MouseButton.Right, new MouseRepeatOptions(20));
        Assert.Equal(20, repeat.IntervalMs);
        Assert.Equal((20, 20), timers.Timer.LastSchedule);

        repeat.SetIntervalMs(5);
        Assert.Equal(5, repeat.IntervalMs);
        Assert.Equal((5, 5), timers.Timer.LastSchedule);

        timers.Timer.Fire();
        timers.Timer.Fire();

        Assert.Equal(3, sender.Clicks.Count);
    }

    [Fact]
    public void StopActiveRepeatStopsOrphanedSessions()
    {
        var sender = new CapturingMouseInputSender();
        var service = new MouseInputService(NullRuntimeLogger.Instance, sender);

        var orphan = service.Repeat(MouseButton.Right, new MouseRepeatOptions(10));
        Assert.True(sender.WaitForClickCount(2, TimeSpan.FromSeconds(2)));

        service.StopActiveRepeat();
        var countAfterStop = sender.Clicks.Count;
        Thread.Sleep(50);

        Assert.Equal(countAfterStop, sender.Clicks.Count);
        orphan.Dispose();
    }

    private sealed class CapturingMouseInputSender : IMouseInputSender
    {
        private readonly object _gate = new();
        private readonly ManualResetEventSlim _clickSent = new();

        public List<MouseButton> Clicks { get; } = [];

        public List<MouseInputMethod> InputMethods { get; } = [];

        public void SendClick(MouseButton button, MouseInputMethod inputMethod, IRuntimeLogger? logger = null)
        {
            lock (_gate)
            {
                Clicks.Add(button);
                InputMethods.Add(inputMethod);
            }

            _clickSent.Set();
        }

        public bool WaitForClickCount(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                lock (_gate)
                {
                    if (Clicks.Count >= count)
                    {
                        return true;
                    }
                }

                _clickSent.Wait(TimeSpan.FromMilliseconds(25));
                _clickSent.Reset();
            }

            return false;
        }
    }

    private sealed class CapturingMouseRepeatTimerFactory : IMouseRepeatTimerFactory
    {
        public CapturingMouseRepeatTimer Timer { get; } = new();

        public IMouseRepeatTimer Create(Action tick)
        {
            Timer.Tick = tick;
            return Timer;
        }
    }

    private sealed class CapturingMouseRepeatTimer : IMouseRepeatTimer
    {
        public Action? Tick { get; set; }

        public (int DueTimeMs, int PeriodMs) LastSchedule { get; private set; }

        public void Change(int dueTimeMs, int periodMs)
        {
            LastSchedule = (dueTimeMs, periodMs);
        }

        public void Fire()
        {
            Assert.IsType<Action>(Tick)();
        }

        public void Dispose()
        {
        }
    }
}
