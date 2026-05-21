namespace HsWin.Core.Audio;

public interface IAudioRecordingSession : IDisposable
{
    string Path { get; }

    bool IsRecording { get; }

    void Stop();
}
