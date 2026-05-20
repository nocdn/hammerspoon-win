using System.Windows.Threading;
using HsWin.Core.Scripting;

namespace HsWin.App.Scripting;

internal sealed class DispatcherScriptCallbackScheduler : IScriptCallbackScheduler
{
    private readonly Dispatcher _dispatcher;

    public DispatcherScriptCallbackScheduler(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Schedule(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (_dispatcher.CheckAccess())
        {
            callback();
            return;
        }

        _dispatcher.BeginInvoke(callback, DispatcherPriority.Normal);
    }
}
