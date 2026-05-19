namespace HsWin.Core.Audio;

public sealed class NullAudioDeviceController : IAudioDeviceController
{
    public static NullAudioDeviceController Instance { get; } = new();

    private NullAudioDeviceController()
    {
    }

    public AudioDeviceSnapshot GetDefaultOutputDevice()
    {
        throw new NotSupportedException("Audio device control is not available in this runtime.");
    }

    public IReadOnlyList<AudioDeviceSnapshot> GetOutputDevices()
    {
        throw new NotSupportedException("Audio device control is not available in this runtime.");
    }

    public AudioDeviceVolumeSnapshot GetVolume(string? deviceId)
    {
        throw new NotSupportedException("Audio device control is not available in this runtime.");
    }

    public AudioDeviceVolumeSnapshot SetVolume(string? deviceId, double volume)
    {
        throw new NotSupportedException("Audio device control is not available in this runtime.");
    }

    public AudioDeviceVolumeSnapshot SetMuted(string? deviceId, bool muted)
    {
        throw new NotSupportedException("Audio device control is not available in this runtime.");
    }

    public AudioDeviceVolumeSnapshot ToggleMute(string? deviceId)
    {
        throw new NotSupportedException("Audio device control is not available in this runtime.");
    }
}
