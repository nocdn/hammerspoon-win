namespace HsWin.Core.Support;

internal sealed class DisposableAction : IDisposable
{
    private readonly Action _dispose;
    private bool _disposed;

    public DisposableAction(Action dispose)
    {
        _dispose = dispose;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _dispose();
        _disposed = true;
    }
}
