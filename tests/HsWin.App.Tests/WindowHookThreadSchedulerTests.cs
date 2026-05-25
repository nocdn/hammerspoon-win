using HsWin.App.Windows;

namespace HsWin.App.Tests;

public sealed class WindowHookThreadSchedulerTests
{
    [Fact]
    public void RunExecutesDirectlyWhenNoContextIsCaptured()
    {
        var scheduler = new WindowHookThreadScheduler(null);
        var calls = 0;

        scheduler.Run(() => calls++);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void RunUsesCapturedContextWhenCurrentContextDiffers()
    {
        var context = new CapturingSynchronizationContext();
        var scheduler = new WindowHookThreadScheduler(context);
        var calls = 0;

        scheduler.Run(() => calls++);

        Assert.Equal(1, calls);
        Assert.Equal(1, context.SendCount);
    }

    [Fact]
    public void RunExecutesDirectlyWhenAlreadyOnCapturedContext()
    {
        var previous = SynchronizationContext.Current;
        var context = new CapturingSynchronizationContext();

        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            var scheduler = new WindowHookThreadScheduler(context);
            var calls = 0;

            scheduler.Run(() => calls++);

            Assert.Equal(1, calls);
            Assert.Equal(0, context.SendCount);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class CapturingSynchronizationContext : SynchronizationContext
    {
        public int SendCount { get; private set; }

        public override void Send(SendOrPostCallback callback, object? state)
        {
            SendCount++;
            callback(state);
        }
    }
}
