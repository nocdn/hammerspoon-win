namespace HsWin.App.Windows;

internal sealed class WindowHookThreadScheduler
{
    private readonly SynchronizationContext? _context;

    public WindowHookThreadScheduler(SynchronizationContext? context)
    {
        _context = context;
    }

    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_context is null || ReferenceEquals(SynchronizationContext.Current, _context))
        {
            action();
            return;
        }

        _context.Send(_ => action(), null);
    }
}
