namespace HsWin.Core.Scripting;

public class ScriptResourceHandle : IDisposable
{
    private readonly IDisposable _inner;
    private bool _disposed;

    public ScriptResourceHandle(IDisposable inner)
    {
        _inner = inner;
    }

    public bool IsDisposed => _disposed;

    public void Stop()
    {
        Dispose();
    }

    public void stop()
    {
        Dispose();
    }

    public void Delete()
    {
        Dispose();
    }

    public void delete()
    {
        Dispose();
    }

    public void dispose()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _inner.Dispose();
        _disposed = true;
    }
}
