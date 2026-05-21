using HsWin.Core.Audio;
using HsWin.Core.Logging;
using System.Runtime.InteropServices;

namespace HsWin.App.Audio;

internal sealed partial class NativeAudioDeviceController : IAudioDeviceController
{
    private static readonly Guid AudioEndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid DeviceFriendlyNameFormatId = new("A45C254E-DF1C-4EFD-8020-67D146A850E0");
    private static readonly PropertyKey DeviceFriendlyNameKey = new(DeviceFriendlyNameFormatId, 14);
    private static readonly Guid EventContext = Guid.Empty;

    private readonly IRuntimeLogger _logger;

    public NativeAudioDeviceController(IRuntimeLogger logger)
    {
        _logger = logger;
    }

    public AudioDeviceSnapshot GetDefaultOutputDevice()
    {
        var defaultDeviceId = GetDefaultDeviceId(AudioDataFlow.Render);
        return WithDevice(defaultDeviceId, device => CreateDeviceSnapshot(device, defaultDeviceId));
    }

    public IReadOnlyList<AudioDeviceSnapshot> GetOutputDevices()
    {
        return GetDevices(AudioDataFlow.Render, "output");
    }

    public AudioDeviceSnapshot GetDefaultInputDevice()
    {
        var defaultDeviceId = GetDefaultDeviceId(AudioDataFlow.Capture);
        return WithDevice(defaultDeviceId, device => CreateDeviceSnapshot(device, defaultDeviceId));
    }

    public IReadOnlyList<AudioDeviceSnapshot> GetInputDevices()
    {
        return GetDevices(AudioDataFlow.Capture, "input");
    }

    public AudioDeviceVolumeSnapshot GetVolume(string? deviceId)
    {
        return WithDevice(deviceId, AudioDataFlow.Render, CreateVolumeSnapshot);
    }

    public AudioDeviceVolumeSnapshot SetVolume(string? deviceId, double volume)
    {
        return SetVolume(deviceId, AudioDataFlow.Render, volume, "Audio volume");
    }

    public AudioDeviceVolumeSnapshot SetMuted(string? deviceId, bool muted)
    {
        return SetMuted(deviceId, AudioDataFlow.Render, muted, "Audio mute");
    }

    public AudioDeviceVolumeSnapshot ToggleMute(string? deviceId)
    {
        return ToggleMute(deviceId, AudioDataFlow.Render, "Audio mute");
    }

    public AudioDeviceVolumeSnapshot GetInputVolume(string? deviceId)
    {
        return WithDevice(deviceId, AudioDataFlow.Capture, CreateVolumeSnapshot);
    }

    public AudioDeviceVolumeSnapshot SetInputVolume(string? deviceId, double volume)
    {
        return SetVolume(deviceId, AudioDataFlow.Capture, volume, "Audio input volume");
    }

    public AudioDeviceVolumeSnapshot SetInputMuted(string? deviceId, bool muted)
    {
        return SetMuted(deviceId, AudioDataFlow.Capture, muted, "Audio input mute");
    }

    public AudioDeviceVolumeSnapshot ToggleInputMute(string? deviceId)
    {
        return ToggleMute(deviceId, AudioDataFlow.Capture, "Audio input mute");
    }

    private IReadOnlyList<AudioDeviceSnapshot> GetDevices(AudioDataFlow dataFlow, string description)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = CreateDeviceEnumerator();
            enumerator.EnumAudioEndpoints(dataFlow, DeviceState.Active, out collection);
            collection.GetCount(out var count);
            var defaultDeviceId = GetDefaultDeviceId(enumerator, dataFlow);
            var devices = new List<AudioDeviceSnapshot>((int)count);

            for (uint index = 0; index < count; index++)
            {
                IMMDevice? device = null;
                try
                {
                    collection.Item(index, out device);
                    devices.Add(CreateDeviceSnapshot(device, defaultDeviceId));
                }
                finally
                {
                    ReleaseComObject(device);
                }
            }

            _logger.Info($"Audio {description} devices enumerated count={devices.Count}.");
            return devices;
        }
        finally
        {
            ReleaseComObject(collection);
            ReleaseComObject(enumerator);
        }
    }

    private AudioDeviceVolumeSnapshot SetVolume(string? deviceId, AudioDataFlow dataFlow, double volume, string description)
    {
        return WithDevice(deviceId, dataFlow, device =>
        {
            IAudioEndpointVolume? endpoint = null;
            try
            {
                endpoint = ActivateEndpointVolume(device);
                endpoint.SetMasterVolumeLevelScalar((float)(volume / 100), EventContext);
                var snapshot = CreateVolumeSnapshot(device, endpoint);
                _logger.Info($"{description} set id='{snapshot.Id}' volume={snapshot.Volume} muted={snapshot.Muted}.");
                return snapshot;
            }
            finally
            {
                ReleaseComObject(endpoint);
            }
        });
    }

    private AudioDeviceVolumeSnapshot SetMuted(string? deviceId, AudioDataFlow dataFlow, bool muted, string description)
    {
        return WithDevice(deviceId, dataFlow, device =>
        {
            IAudioEndpointVolume? endpoint = null;
            try
            {
                endpoint = ActivateEndpointVolume(device);
                endpoint.SetMute(muted, EventContext);
                var snapshot = CreateVolumeSnapshot(device, endpoint);
                _logger.Info($"{description} set id='{snapshot.Id}' volume={snapshot.Volume} muted={snapshot.Muted}.");
                return snapshot;
            }
            finally
            {
                ReleaseComObject(endpoint);
            }
        });
    }

    private AudioDeviceVolumeSnapshot ToggleMute(string? deviceId, AudioDataFlow dataFlow, string description)
    {
        return WithDevice(deviceId, dataFlow, device =>
        {
            IAudioEndpointVolume? endpoint = null;
            try
            {
                endpoint = ActivateEndpointVolume(device);
                endpoint.GetMute(out var muted);
                endpoint.SetMute(!muted, EventContext);
                var snapshot = CreateVolumeSnapshot(device, endpoint);
                _logger.Info($"{description} toggled id='{snapshot.Id}' volume={snapshot.Volume} muted={snapshot.Muted}.");
                return snapshot;
            }
            finally
            {
                ReleaseComObject(endpoint);
            }
        });
    }

    private T WithDevice<T>(string? deviceId, Func<IMMDevice, T> action)
    {
        return WithDevice(deviceId, AudioDataFlow.Render, action);
    }

    private T WithDevice<T>(string? deviceId, AudioDataFlow dataFlow, Func<IMMDevice, T> action)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        try
        {
            enumerator = CreateDeviceEnumerator();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                enumerator.GetDefaultAudioEndpoint(dataFlow, AudioRole.Multimedia, out device);
            }
            else
            {
                enumerator.GetDevice(deviceId, out device);
            }

            return action(device);
        }
        finally
        {
            ReleaseComObject(device);
            ReleaseComObject(enumerator);
        }
    }

    private string GetDefaultDeviceId(AudioDataFlow dataFlow)
    {
        IMMDeviceEnumerator? enumerator = null;
        try
        {
            enumerator = CreateDeviceEnumerator();
            return GetDefaultDeviceId(enumerator, dataFlow);
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    private static string GetDefaultDeviceId(IMMDeviceEnumerator enumerator, AudioDataFlow dataFlow)
    {
        IMMDevice? defaultDevice = null;
        try
        {
            enumerator.GetDefaultAudioEndpoint(dataFlow, AudioRole.Multimedia, out defaultDevice);
            defaultDevice.GetId(out var defaultDeviceId);
            return defaultDeviceId;
        }
        finally
        {
            ReleaseComObject(defaultDevice);
        }
    }

    private static AudioDeviceSnapshot CreateDeviceSnapshot(IMMDevice device, string defaultDeviceId)
    {
        var volume = CreateVolumeSnapshot(device);
        return new AudioDeviceSnapshot(
            volume.Id,
            volume.Name,
            string.Equals(volume.Id, defaultDeviceId, StringComparison.OrdinalIgnoreCase),
            volume.Volume,
            volume.Muted);
    }

    private static AudioDeviceVolumeSnapshot CreateVolumeSnapshot(IMMDevice device)
    {
        IAudioEndpointVolume? endpoint = null;
        try
        {
            endpoint = ActivateEndpointVolume(device);
            return CreateVolumeSnapshot(device, endpoint);
        }
        finally
        {
            ReleaseComObject(endpoint);
        }
    }

    private static AudioDeviceVolumeSnapshot CreateVolumeSnapshot(IMMDevice device, IAudioEndpointVolume endpoint)
    {
        device.GetId(out var id);
        endpoint.GetMasterVolumeLevelScalar(out var level);
        endpoint.GetMute(out var muted);
        return new AudioDeviceVolumeSnapshot(id, ReadFriendlyName(device) ?? id, Math.Round(level * 100, 2), muted);
    }

    private static string? ReadFriendlyName(IMMDevice device)
    {
        IPropertyStore? properties = null;
        try
        {
            device.OpenPropertyStore(StorageAccess.Read, out properties);
            var key = DeviceFriendlyNameKey;
            properties.GetValue(ref key, out var propertyValue);
            try
            {
                return propertyValue.GetString();
            }
            finally
            {
                _ = Ole32.PropVariantClear(ref propertyValue);
            }
        }
        finally
        {
            ReleaseComObject(properties);
        }
    }

    private static IAudioEndpointVolume ActivateEndpointVolume(IMMDevice device)
    {
        var interfaceId = AudioEndpointVolumeId;
        device.Activate(ref interfaceId, ClassContext.All, IntPtr.Zero, out var endpoint);
        return (IAudioEndpointVolume)endpoint;
    }

    private static IMMDeviceEnumerator CreateDeviceEnumerator()
    {
        return (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            _ = Marshal.ReleaseComObject(instance);
        }
    }

    private enum AudioDataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    private enum AudioRole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [Flags]
    private enum DeviceState : uint
    {
        Active = 0x00000001
    }

    [Flags]
    private enum ClassContext : uint
    {
        InprocServer = 0x1,
        InprocHandler = 0x2,
        LocalServer = 0x4,
        RemoteServer = 0x10,
        All = InprocServer | InprocHandler | LocalServer | RemoteServer
    }

    private enum StorageAccess
    {
        Read = 0
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumerator
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(AudioDataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);

        void GetDefaultAudioEndpoint(AudioDataFlow dataFlow, AudioRole role, out IMMDevice endpoint);

        void GetDevice(
            [MarshalAs(UnmanagedType.LPWStr)] string id,
            out IMMDevice device);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-C0B2C6F3CAD4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        void GetCount(out uint count);

        void Item(uint deviceNumber, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(
            ref Guid interfaceId,
            ClassContext classContext,
            IntPtr activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);

        void OpenPropertyStore(StorageAccess access, out IPropertyStore properties);

        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        void GetState(out DeviceState state);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);

        void GetAt(uint propertyIndex, out PropertyKey key);

        void GetValue(ref PropertyKey key, out PropVariant value);

        void SetValue(ref PropertyKey key, ref PropVariant value);

        void Commit();
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr notify);

        void UnregisterControlChangeNotify(IntPtr notify);

        void GetChannelCount(out uint channelCount);

        void SetMasterVolumeLevel(float levelDb, Guid eventContext);

        void SetMasterVolumeLevelScalar(float level, Guid eventContext);

        void GetMasterVolumeLevel(out float levelDb);

        void GetMasterVolumeLevelScalar(out float level);

        void SetChannelVolumeLevel(uint channelNumber, float levelDb, Guid eventContext);

        void SetChannelVolumeLevelScalar(uint channelNumber, float level, Guid eventContext);

        void GetChannelVolumeLevel(uint channelNumber, out float levelDb);

        void GetChannelVolumeLevelScalar(uint channelNumber, out float level);

        void SetMute(
            [MarshalAs(UnmanagedType.Bool)] bool muted,
            Guid eventContext);

        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);

        void GetVolumeStepInfo(out uint step, out uint stepCount);

        void VolumeStepUp(Guid eventContext);

        void VolumeStepDown(Guid eventContext);

        void QueryHardwareSupport(out uint hardwareSupportMask);

        void GetVolumeRange(out float volumeMinDb, out float volumeMaxDb, out float volumeIncrementDb);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }

        public Guid FormatId;

        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        private readonly ushort _valueType;
        private readonly ushort _reserved1;
        private readonly ushort _reserved2;
        private readonly ushort _reserved3;
        private readonly IntPtr _valuePointer;
        private readonly int _valueData1;
        private readonly int _valueData2;

        public string? GetString()
        {
            return _valueType switch
            {
                30 => Marshal.PtrToStringAnsi(_valuePointer),
                31 => Marshal.PtrToStringUni(_valuePointer),
                _ => null
            };
        }
    }

    private static partial class Ole32
    {
        [LibraryImport("ole32.dll")]
        public static partial int PropVariantClear(ref PropVariant propVariant);
    }
}
