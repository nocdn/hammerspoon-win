using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Input;

/// <summary>
/// Posts native mouse-button messages directly to the focused window.
/// This is useful for GLFW applications that do not consume injected SendInput events.
/// </summary>
internal static partial class WindowMessageMouseInputSender
{
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMButtonUp = 0x0208;
    private const uint WmXButtonDown = 0x020B;
    private const uint WmXButtonUp = 0x020C;
    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;

    private static readonly object TargetLogGate = new();
    private static nint _lastLoggedTarget;

    internal static void SendClick(MouseButton button, IRuntimeLogger? logger = null)
    {
        var target = User32.GetForegroundWindow();
        if (target == 0)
        {
            throw new InvalidOperationException("Could not find a focused window for mouse-message input.");
        }

        LogTargetIfChanged(target, logger);

        var (downMessage, upMessage, buttonData) = GetButtonMessages(button);
        PostMessageOrThrow(target, downMessage, buttonData, button, "down", logger);
        PostMessageOrThrow(target, upMessage, buttonData, button, "up", logger);
    }

    private static (uint DownMessage, uint UpMessage, nuint ButtonData) GetButtonMessages(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => (WmLButtonDown, WmLButtonUp, 0),
            MouseButton.Right => (WmRButtonDown, WmRButtonUp, 0),
            MouseButton.Middle => (WmMButtonDown, WmMButtonUp, 0),
            MouseButton.XButton1 => (WmXButtonDown, WmXButtonUp, XButton1 << 16),
            MouseButton.XButton2 => (WmXButtonDown, WmXButtonUp, XButton2 << 16),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unsupported mouse button.")
        };
    }

    private static void PostMessageOrThrow(
        nint target,
        uint message,
        nuint buttonData,
        MouseButton button,
        string phase,
        IRuntimeLogger? logger)
    {
        if (User32.PostMessage(target, message, buttonData, 0))
        {
            return;
        }

        var error = Marshal.GetLastPInvokeError();
        logger?.Warning(
            $"PostMessage failed phase={phase} button={MouseButtonParser.GetDisplayName(button)} " +
            $"hwnd=0x{target.ToInt64():X} win32=0x{error:X}.");
        throw new Win32Exception(error, "Could not post mouse input to the focused window.");
    }

    private static void LogTargetIfChanged(nint target, IRuntimeLogger? logger)
    {
        if (logger is null)
        {
            return;
        }

        lock (TargetLogGate)
        {
            if (_lastLoggedTarget == target)
            {
                return;
            }

            _lastLoggedTarget = target;
        }

        User32.GetWindowThreadProcessId(target, out var processId);
        var processName = GetProcessName(processId);
        var title = GetWindowTitle(target);
        logger.Info(
            $"Mouse window-message target hwnd=0x{target.ToInt64():X} processId={processId} " +
            $"processName='{processName ?? string.Empty}' title='{title}'.");
    }

    private static string? GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(unchecked((int)processId));
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static string GetWindowTitle(nint windowHandle)
    {
        var length = User32.GetWindowTextLength(windowHandle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        return User32.GetWindowText(windowHandle, builder, builder.Capacity) > 0
            ? builder.ToString()
            : string.Empty;
    }

    private static partial class User32
    {
        [LibraryImport("user32.dll")]
        public static partial nint GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(nint windowHandle, uint message, nuint wParam, nint lParam);

        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(nint windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(nint windowHandle, StringBuilder text, int maxCount);
    }
}
