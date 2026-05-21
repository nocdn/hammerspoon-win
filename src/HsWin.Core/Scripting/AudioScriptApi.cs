using System.Globalization;
using HsWin.Core.Audio;
using HsWin.Core.Logging;

namespace HsWin.Core.Scripting;

public sealed class AudioScriptApi
{
    private readonly IAudioDeviceController _audioDevices;
    private readonly IRuntimeLogger _logger;

    public AudioScriptApi(IAudioDeviceController audioDevices, IRuntimeLogger logger)
    {
        _audioDevices = audioDevices;
        _logger = logger;
    }

    public string GetDefaultOutputDeviceJson()
    {
        var device = _audioDevices.GetDefaultOutputDevice();
        _logger.Info($"Script hs.audiodevice.defaultOutputDevice() returned id='{device.Id}' name='{device.Name}'.");
        return ScriptJson.Serialize(device);
    }

    public string GetOutputDevicesJson()
    {
        var devices = _audioDevices.GetOutputDevices();
        _logger.Info($"Script hs.audiodevice.allOutputDevices() returned {devices.Count} devices.");
        return ScriptJson.Serialize(devices);
    }

    public string GetDefaultInputDeviceJson()
    {
        var device = _audioDevices.GetDefaultInputDevice();
        _logger.Info($"Script hs.audiodevice.defaultInputDevice() returned id='{device.Id}' name='{device.Name}'.");
        return ScriptJson.Serialize(device);
    }

    public string GetInputDevicesJson()
    {
        var devices = _audioDevices.GetInputDevices();
        _logger.Info($"Script hs.audiodevice.allInputDevices() returned {devices.Count} devices.");
        return ScriptJson.Serialize(devices);
    }

    public string GetVolumeJson(object? deviceId = null)
    {
        var normalizedDeviceId = ScriptArgumentReader.OptionalString(deviceId);
        var result = _audioDevices.GetVolume(normalizedDeviceId);
        _logger.Info($"Script hs.audiodevice.getVolume() returned id='{result.Id}' volume={result.Volume.ToString(CultureInfo.InvariantCulture)} muted={result.Muted}.");
        return ScriptJson.Serialize(result);
    }

    public string SetVolumeJson(object? deviceId, object? volume)
    {
        var normalizedDeviceId = ScriptArgumentReader.OptionalString(deviceId);
        var normalizedVolume = ConvertAudioVolume(volume);
        var result = _audioDevices.SetVolume(normalizedDeviceId, normalizedVolume);
        _logger.Info($"Script hs.audiodevice.setVolume() set id='{result.Id}' volume={result.Volume.ToString(CultureInfo.InvariantCulture)} muted={result.Muted}.");
        return ScriptJson.Serialize(result);
    }

    public string SetMutedJson(object? deviceId, object? muted)
    {
        var normalizedDeviceId = ScriptArgumentReader.OptionalString(deviceId);
        var normalizedMuted = ScriptArgumentReader.RequireBoolean(muted, "muted");
        var result = _audioDevices.SetMuted(normalizedDeviceId, normalizedMuted);
        _logger.Info($"Script hs.audiodevice.setMuted() set id='{result.Id}' volume={result.Volume.ToString(CultureInfo.InvariantCulture)} muted={result.Muted}.");
        return ScriptJson.Serialize(result);
    }

    public string ToggleMuteJson(object? deviceId = null)
    {
        var normalizedDeviceId = ScriptArgumentReader.OptionalString(deviceId);
        var result = _audioDevices.ToggleMute(normalizedDeviceId);
        _logger.Info($"Script hs.audiodevice.toggleMute() toggled id='{result.Id}' volume={result.Volume.ToString(CultureInfo.InvariantCulture)} muted={result.Muted}.");
        return ScriptJson.Serialize(result);
    }

    public string GetInputVolumeJson(object? deviceId = null)
    {
        var normalizedDeviceId = ScriptArgumentReader.OptionalString(deviceId);
        var result = _audioDevices.GetInputVolume(normalizedDeviceId);
        _logger.Info($"Script hs.audiodevice.getInputVolume() returned id='{result.Id}' volume={result.Volume.ToString(CultureInfo.InvariantCulture)} muted={result.Muted}.");
        return ScriptJson.Serialize(result);
    }

    public string SetInputVolumeJson(object? deviceId, object? volume)
    {
        var normalizedDeviceId = ScriptArgumentReader.OptionalString(deviceId);
        var normalizedVolume = ConvertAudioVolume(volume);
        var result = _audioDevices.SetInputVolume(normalizedDeviceId, normalizedVolume);
        _logger.Info($"Script hs.audiodevice.setInputVolume() set id='{result.Id}' volume={result.Volume.ToString(CultureInfo.InvariantCulture)} muted={result.Muted}.");
        return ScriptJson.Serialize(result);
    }

    public string SetInputMutedJson(object? deviceId, object? muted)
    {
        var normalizedDeviceId = ScriptArgumentReader.OptionalString(deviceId);
        var normalizedMuted = ScriptArgumentReader.RequireBoolean(muted, "muted");
        var result = _audioDevices.SetInputMuted(normalizedDeviceId, normalizedMuted);
        _logger.Info($"Script hs.audiodevice.setInputMuted() set id='{result.Id}' volume={result.Volume.ToString(CultureInfo.InvariantCulture)} muted={result.Muted}.");
        return ScriptJson.Serialize(result);
    }

    public string ToggleInputMuteJson(object? deviceId = null)
    {
        var normalizedDeviceId = ScriptArgumentReader.OptionalString(deviceId);
        var result = _audioDevices.ToggleInputMute(normalizedDeviceId);
        _logger.Info($"Script hs.audiodevice.toggleInputMute() toggled id='{result.Id}' volume={result.Volume.ToString(CultureInfo.InvariantCulture)} muted={result.Muted}.");
        return ScriptJson.Serialize(result);
    }

    private static double ConvertAudioVolume(object? value)
    {
        var volume = ScriptArgumentReader.RequireDouble(value, "volume", "a number between 0 and 100");
        if (volume is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0 and 100.");
        }

        return volume;
    }
}
