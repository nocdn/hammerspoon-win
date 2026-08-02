using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;

namespace HsWin.App.Input;

/// <summary>
/// Posts native key-down/key-up messages directly to the focused window.
/// Useful for GLFW/game apps that ignore injected SendInput keyboard events.
/// </summary>
internal static partial class WindowMessageKeyboardInputSender
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint MapVkToVsc = 0;
    private const uint MapVkToVscEx = 4;

    // Hardware scancodes for left/right modifiers (same table as SendInput path).
    private const ushort ScanLeftShift = 0x2A;
    private const ushort ScanRightShift = 0x36;
    private const ushort ScanLeftControl = 0x1D;
    private const ushort ScanRightControl = 0x1D;
    private const ushort ScanLeftAlt = 0x38;
    private const ushort ScanRightAlt = 0x38;

    private static readonly object TargetLogGate = new();
    private static nint _lastLoggedTarget;

    internal static void SendKeyDown(uint virtualKey, IRuntimeLogger? logger = null)
    {
        PostKey(virtualKey, keyUp: false, logger);
    }

    internal static void SendKeyUp(uint virtualKey, IRuntimeLogger? logger = null)
    {
        PostKey(virtualKey, keyUp: true, logger);
    }

    internal static void SendTap(
        uint virtualKey,
        IReadOnlyList<uint>? suppressedModifierVirtualKeys = null,
        IReadOnlyList<uint>? modifierVirtualKeys = null,
        IRuntimeLogger? logger = null)
    {
        // Suppressed physical modifiers must update global key state (SendInput), not just the target window.
        if (suppressedModifierVirtualKeys is { Count: > 0 })
        {
            foreach (var modifierVirtualKey in suppressedModifierVirtualKeys)
            {
                KeyboardInputSender.SendKeyUp(modifierVirtualKey, logger);
            }
        }

        if (modifierVirtualKeys is { Count: > 0 })
        {
            foreach (var modifierVirtualKey in modifierVirtualKeys)
            {
                PostKey(modifierVirtualKey, keyUp: false, logger);
            }
        }

        try
        {
            PostKey(virtualKey, keyUp: false, logger);
            PostKey(virtualKey, keyUp: true, logger);
        }
        finally
        {
            if (modifierVirtualKeys is { Count: > 0 })
            {
                foreach (var modifierVirtualKey in modifierVirtualKeys.Reverse())
                {
                    PostKey(modifierVirtualKey, keyUp: true, logger);
                }
            }
        }
    }

    private static void PostKey(uint virtualKey, bool keyUp, IRuntimeLogger? logger)
    {
        var target = User32.GetForegroundWindow();
        if (target == 0)
        {
            throw new InvalidOperationException("Could not find a focused window for keyboard-message input.");
        }

        LogTargetIfChanged(target, logger);

        var scanCode = ResolveScanCode(virtualKey);
        var isExtended = KeyboardKeyRules.IsExtendedVirtualKey(virtualKey)
            || virtualKey is KeyboardKeyRules.VkRightControl or KeyboardKeyRules.VkRightMenu;
        var isAlt = virtualKey is KeyboardKeyRules.VkMenu
            or KeyboardKeyRules.VkLeftMenu
            or KeyboardKeyRules.VkRightMenu;

        var message = (keyUp, isAlt) switch
        {
            (false, true) => WmSysKeyDown,
            (true, true) => WmSysKeyUp,
            (false, false) => WmKeyDown,
            (true, false) => WmKeyUp
        };

        // Real WM_KEY* messages use the generic modifier VK in wParam and encode left/right in
        // the scan-code/extended bits. GLFW follows that convention when translating modifiers.
        nuint wParam = ResolveMessageVirtualKey(virtualKey);
        var lParam = BuildKeyLParam(scanCode, isExtended, keyUp);

        if (User32.PostMessage(target, message, wParam, lParam))
        {
            return;
        }

        var error = Marshal.GetLastPInvokeError();
        logger?.Warning(
            $"PostMessage keyboard failed phase={(keyUp ? "up" : "down")} vk=0x{virtualKey:X2} " +
            $"hwnd=0x{target.ToInt64():X} win32=0x{error:X}.");
        throw new Win32Exception(error, $"Could not post keyboard input for virtual key 0x{virtualKey:X2}.");
    }

    private static nint BuildKeyLParam(ushort scanCode, bool isExtended, bool keyUp)
    {
        // bits 0-15: repeat count = 1
        // bits 16-23: scan code
        // bit 24: extended
        // bit 30: previous key state (1 if already down → set on keyup)
        // bit 31: transition state (1 = keyup)
        var lParam = 1 | ((scanCode & 0xFF) << 16);
        if (isExtended)
        {
            lParam |= 1 << 24;
        }

        if (keyUp)
        {
            lParam |= 1 << 30;
            lParam |= 1 << 31;
        }

        return lParam;
    }

    internal static uint ResolveMessageVirtualKey(uint virtualKey)
    {
        return virtualKey switch
        {
            KeyboardKeyRules.VkLeftShift or KeyboardKeyRules.VkRightShift => KeyboardKeyRules.VkShift,
            KeyboardKeyRules.VkLeftControl or KeyboardKeyRules.VkRightControl => KeyboardKeyRules.VkControl,
            KeyboardKeyRules.VkLeftMenu or KeyboardKeyRules.VkRightMenu => KeyboardKeyRules.VkMenu,
            _ => virtualKey
        };
    }

    private static ushort ResolveScanCode(uint virtualKey)
    {
        return virtualKey switch
        {
            KeyboardKeyRules.VkLeftShift or KeyboardKeyRules.VkShift => ScanLeftShift,
            KeyboardKeyRules.VkRightShift => ScanRightShift,
            KeyboardKeyRules.VkLeftControl or KeyboardKeyRules.VkControl => ScanLeftControl,
            KeyboardKeyRules.VkRightControl => ScanRightControl,
            KeyboardKeyRules.VkLeftMenu or KeyboardKeyRules.VkMenu => ScanLeftAlt,
            KeyboardKeyRules.VkRightMenu => ScanRightAlt,
            _ => ResolveScanCodeViaMapVirtualKey(virtualKey)
        };
    }

    private static ushort ResolveScanCodeViaMapVirtualKey(uint virtualKey)
    {
        var mapped = User32.MapVirtualKey(virtualKey, MapVkToVscEx);
        if (mapped == 0)
        {
            mapped = User32.MapVirtualKey(virtualKey, MapVkToVsc);
        }

        return (ushort)(mapped & 0xFF);
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
            $"Keyboard window-message target hwnd=0x{target.ToInt64():X} processId={processId} " +
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

        [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
        public static partial uint MapVirtualKey(uint code, uint mapType);
    }
}
