using HsWin.Core.Audio;

namespace HsWin.Core.Scripting;

public sealed class AudioRecordingScriptHandle : IDisposable
{
    private readonly IAudioRecordingSession _session;
    private readonly Action _markDisposed;
    private bool _disposed;

    public AudioRecordingScriptHandle(IAudioRecordingSession session, Action markDisposed)
    {
        _session = session;
        _markDisposed = markDisposed;
    }

    public string Path => _session.Path;

    public string path => Path;

    public bool IsRecording => !_disposed && _session.IsRecording;

    public bool isRecording => IsRecording;

    public bool IsDisposed => _disposed;

    public void Stop()
    {
        _session.Stop();
    }

    public void stop()
    {
        Stop();
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

        _markDisposed();
        _session.Dispose();
        _disposed = true;
    }
}
