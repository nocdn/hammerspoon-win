using HsWin.Core.Logging;
using HsWin.Core.Media;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Media.Control;

namespace HsWin.App.Media;

internal sealed partial class NativeMediaController : IMediaController
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VkMediaNextTrack = 0xB0;
    private const ushort VkMediaPreviousTrack = 0xB1;
    private const ushort VkMediaPlayPause = 0xB3;

    internal static int NativeInputSize => Marshal.SizeOf<Input>();

    private readonly IRuntimeLogger _logger;

    public NativeMediaController(IRuntimeLogger logger)
    {
        _logger = logger;
    }

    public MediaCommandResult PlayPause()
    {
        return TryControlCurrentSession(
            "playPause",
            session => session.TryTogglePlayPauseAsync().AsTask().GetAwaiter().GetResult(),
            fallbackVirtualKey: VkMediaPlayPause,
            fallbackActionName: "play/pause",
            inferAction: InferPlayPauseAction);
    }

    public MediaCommandResult PreviousTrack()
    {
        return TryControlCurrentSession(
            "previousTrack",
            session => session.TrySkipPreviousAsync().AsTask().GetAwaiter().GetResult(),
            fallbackVirtualKey: VkMediaPreviousTrack,
            fallbackActionName: "previous track",
            inferAction: static (_, _) => "previousTrack");
    }

    public MediaCommandResult NextTrack()
    {
        return TryControlCurrentSession(
            "nextTrack",
            session => session.TrySkipNextAsync().AsTask().GetAwaiter().GetResult(),
            fallbackVirtualKey: VkMediaNextTrack,
            fallbackActionName: "next track",
            inferAction: static (_, _) => "nextTrack");
    }

    private MediaCommandResult TryControlCurrentSession(
        string command,
        Func<GlobalSystemMediaTransportControlsSession, bool> sessionAction,
        ushort fallbackVirtualKey,
        string fallbackActionName,
        Func<string, string, string> inferAction)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var manager = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().GetAwaiter().GetResult();
            var managerReadyAt = Stopwatch.GetTimestamp();
            var session = manager.GetCurrentSession();
            var sessionReadyAt = Stopwatch.GetTimestamp();
            if (session is not null)
            {
                var statusBefore = GetPlaybackStatus(session);
                var beforeStatusReadyAt = Stopwatch.GetTimestamp();
                var success = sessionAction(session);
                var actionReadyAt = Stopwatch.GetTimestamp();
                var statusAfter = GetPlaybackStatus(session);
                var afterStatusReadyAt = Stopwatch.GetTimestamp();
                var action = inferAction(statusBefore, statusAfter);
                _logger.Info(
                    $"Media session timing command='{command}' action='{action}' success={success} statusBefore='{statusBefore}' statusAfter='{statusAfter}' " +
                    $"requestManagerMs={ElapsedMs(startedAt, managerReadyAt):F3} getSessionMs={ElapsedMs(managerReadyAt, sessionReadyAt):F3} " +
                    $"statusBeforeMs={ElapsedMs(sessionReadyAt, beforeStatusReadyAt):F3} actionMs={ElapsedMs(beforeStatusReadyAt, actionReadyAt):F3} " +
                    $"statusAfterMs={ElapsedMs(actionReadyAt, afterStatusReadyAt):F3} totalMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
                return new MediaCommandResult(command, success, action, statusBefore, statusAfter, "mediaSession");
            }

            _logger.Info($"No current media session found for command='{command}' elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}. Falling back to media key.");
        }
        catch (Exception exception)
        {
            _logger.Warning($"Media session command failed for command='{command}' elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}. Falling back to media key. {exception.Message}");
        }

        SendMediaKey(fallbackVirtualKey, fallbackActionName);
        _logger.Info($"Media fallback timing command='{command}' backend='sendInput' totalMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
        return MediaCommandResult.Sent(command, "sendInput");
    }

    private static double ElapsedMs(long startTimestamp, long endTimestamp)
    {
        return Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;
    }

    private static string GetPlaybackStatus(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return NormalizePlaybackStatus(session.GetPlaybackInfo().PlaybackStatus);
        }
        catch
        {
            return "unknown";
        }
    }

    internal static string InferPlayPauseAction(string statusBefore, string statusAfter)
    {
        if (statusAfter is "playing")
        {
            return "played";
        }

        if (statusAfter is "paused" or "stopped")
        {
            return "paused";
        }

        if (statusBefore is "playing")
        {
            return "paused";
        }

        if (statusBefore is "paused" or "stopped")
        {
            return "played";
        }

        return "toggled";
    }

    private static string NormalizePlaybackStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
    {
        return status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed => "closed",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => "opened",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => "changing",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => "stopped",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => "playing",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => "paused",
            _ => "unknown"
        };
    }

    private unsafe void SendMediaKey(ushort virtualKey, string actionName)
    {
        Span<Input> inputs =
        [
            CreateKeyboardInput(virtualKey, keyUp: false),
            CreateKeyboardInput(virtualKey, keyUp: true)
        ];

        fixed (Input* inputPointer = inputs)
        {
            var sentCount = User32.SendInput((uint)inputs.Length, inputPointer, NativeInputSize);
            if (sentCount != inputs.Length)
            {
                var exception = new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not send media key for {actionName}.");
                _logger.Error($"Media key send failed action='{actionName}' vk=0x{virtualKey:X2} inputSize={NativeInputSize}.", exception);
                throw exception;
            }
        }

        _logger.Info($"Media key sent action='{actionName}' vk=0x{virtualKey:X2}.");
    }

    private static Input CreateKeyboardInput(ushort virtualKey, bool keyUp)
    {
        return new Input
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = keyUp ? KeyEventKeyUp : 0
            }
        };
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        // INPUT is 40 bytes on 64-bit Windows because the native union is sized for MOUSEINPUT.
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;

        public ushort ScanCode;

        public uint Flags;

        public uint Time;

        public UIntPtr ExtraInfo;
    }

    private static partial class User32
    {
        [LibraryImport("user32.dll", SetLastError = true)]
        public static unsafe partial uint SendInput(uint inputCount, Input* inputs, int inputSize);
    }
}
