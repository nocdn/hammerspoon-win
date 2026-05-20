using HsWin.App.Scripting;
using System.Windows.Threading;

namespace HsWin.App.Tests;

public sealed class DispatcherScriptCallbackSchedulerTests
{
    [Fact]
    public void ScheduleRunsImmediatelyWhenAlreadyOnDispatcher()
    {
        var scheduler = new DispatcherScriptCallbackScheduler(Dispatcher.CurrentDispatcher);
        var ran = false;

        scheduler.Schedule(() => ran = true);

        Assert.True(ran);
    }
}
