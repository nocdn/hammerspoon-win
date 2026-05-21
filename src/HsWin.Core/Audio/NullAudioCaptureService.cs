namespace HsWin.Core.Audio;

public sealed class NullAudioCaptureService : IAudioCaptureService
{
    public static NullAudioCaptureService Instance { get; } = new();

    private NullAudioCaptureService()
    {
    }

    public IAudioRecordingSession Record(AudioRecordingOptions options, Action<AudioCaptureEvent> callback)
    {
        throw new NotSupportedException("Audio capture is not available in this runtime.");
    }

    public IDisposable WatchLevels(AudioLevelWatchOptions options, Action<AudioCaptureEvent> callback)
    {
        throw new NotSupportedException("Audio capture is not available in this runtime.");
    }
}
