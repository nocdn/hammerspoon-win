namespace HsWin.Core.Audio;

public interface IAudioDeviceController
{
    AudioDeviceSnapshot GetDefaultOutputDevice();

    IReadOnlyList<AudioDeviceSnapshot> GetOutputDevices();

    AudioDeviceVolumeSnapshot GetVolume(string? deviceId);

    AudioDeviceVolumeSnapshot SetVolume(string? deviceId, double volume);

    AudioDeviceVolumeSnapshot SetMuted(string? deviceId, bool muted);

    AudioDeviceVolumeSnapshot ToggleMute(string? deviceId);
}
