namespace HsWin.Core.Audio;

public interface IAudioCaptureService
{
    IAudioRecordingSession Record(AudioRecordingOptions options, Action<AudioCaptureEvent> callback);

    IDisposable WatchLevels(AudioLevelWatchOptions options, Action<AudioCaptureEvent> callback);
}
