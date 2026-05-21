namespace HsWin.Core.Audio;

public interface IAudioDeviceController
{
    AudioDeviceSnapshot GetDefaultOutputDevice();

    IReadOnlyList<AudioDeviceSnapshot> GetOutputDevices();

    AudioDeviceSnapshot GetDefaultInputDevice();

    IReadOnlyList<AudioDeviceSnapshot> GetInputDevices();

    AudioDeviceVolumeSnapshot GetVolume(string? deviceId);

    AudioDeviceVolumeSnapshot SetVolume(string? deviceId, double volume);

    AudioDeviceVolumeSnapshot SetMuted(string? deviceId, bool muted);

    AudioDeviceVolumeSnapshot ToggleMute(string? deviceId);

    AudioDeviceVolumeSnapshot GetInputVolume(string? deviceId);

    AudioDeviceVolumeSnapshot SetInputVolume(string? deviceId, double volume);

    AudioDeviceVolumeSnapshot SetInputMuted(string? deviceId, bool muted);

    AudioDeviceVolumeSnapshot ToggleInputMute(string? deviceId);
}
