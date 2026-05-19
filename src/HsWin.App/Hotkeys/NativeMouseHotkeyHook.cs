using HsWin.Core.Hotkeys;
using HsWin.Core.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HsWin.App.Hotkeys;

internal sealed partial class NativeMouseHotkeyHook : IDisposable
{
    private const int HcAction = 0;
    private const int WhMouseLl = 14;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const int XButton1 = 0x0001;
    private const int XButton2 = 0x0002;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const short KeyPressedMask = unchecked((short)0x8000);

    private readonly object _gate = new();
    private readonly IRuntimeLogger _logger;
    private readonly SynchronizationContext? _callbackContext;
    private readonly Dictionary<int, RegistrationState> _registrations = [];
    private readonly HashSet<HotkeyMouseButton> _consumedButtons = [];
    private readonly HookProcedure _hookProcedure;
    private IntPtr _hookHandle;
    private int _nextId = 1;
    private bool _disposed;

    public NativeMouseHotkeyHook(IRuntimeLogger logger)
    {
        _logger = logger;
        _callbackContext = SynchronizationContext.Current;
        _hookProcedure = HookCallback;
    }

    public IDisposable Register(HotkeyDefinition hotkey, Action pressed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(pressed);

        if (hotkey.InputKind != HotkeyInputKind.MouseButton || hotkey.MouseButton is null)
        {
            throw new ArgumentException("Mouse hotkeys require a mouse-button definition.", nameof(hotkey));
        }

        lock (_gate)
        {
            if (HasDuplicateRegistration(hotkey))
            {
                throw new InvalidOperationException($"Mouse hotkey {hotkey} is already registered.");
            }

            EnsureHookInstalled();

            var id = _nextId++;
            _registrations[id] = new RegistrationState(id, hotkey, pressed);
            _logger.Info($"Mouse hotkey registered id={id} modifiers=0x{(uint)hotkey.Modifiers:X} button={hotkey.MouseButton}.");
            return new MouseHotkeyRegistration(this, id);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            _registrations.Clear();
            _consumedButtons.Clear();
            UninstallHook();
            _disposed = true;
        }
    }

    internal static bool TryGetMouseButtonEvent(int message, uint mouseData, out MouseButtonEvent mouseButtonEvent)
    {
        mouseButtonEvent = default;

        if (message == WmMButtonDown)
        {
            mouseButtonEvent = new MouseButtonEvent(HotkeyMouseButton.Middle, IsDown: true);
            return true;
        }

        if (message == WmMButtonUp)
        {
            mouseButtonEvent = new MouseButtonEvent(HotkeyMouseButton.Middle, IsDown: false);
            return true;
        }

        if (message is WmXButtonDown or WmXButtonUp)
        {
            var xButton = (int)((mouseData >> 16) & 0xFFFF);
            var isDown = message == WmXButtonDown;
            if (xButton == XButton1)
            {
                mouseButtonEvent = new MouseButtonEvent(HotkeyMouseButton.XButton1, isDown);
                return true;
            }

            if (xButton == XButton2)
            {
                mouseButtonEvent = new MouseButtonEvent(HotkeyMouseButton.XButton2, isDown);
                return true;
            }
        }

        return false;
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code == HcAction)
        {
            var hookData = Marshal.PtrToStructure<MouseHookStruct>(lParam);
            if (TryGetMouseButtonEvent(wParam.ToInt32(), hookData.MouseData, out var mouseButtonEvent)
                && TryHandleMouseButtonEvent(mouseButtonEvent))
            {
                return new IntPtr(1);
            }
        }

        return User32.CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private bool TryHandleMouseButtonEvent(MouseButtonEvent mouseButtonEvent)
    {
        if (!mouseButtonEvent.IsDown)
        {
            lock (_gate)
            {
                if (!_consumedButtons.Remove(mouseButtonEvent.Button))
                {
                    return false;
                }
            }

            _logger.Info($"Mouse hotkey button-up consumed button={mouseButtonEvent.Button}.");
            return true;
        }

        var pressedModifiers = ReadPressedModifiers();
        RegistrationState? match;
        lock (_gate)
        {
            match = _registrations.Values.FirstOrDefault(registration =>
                registration.Hotkey.MouseButton == mouseButtonEvent.Button
                && registration.Hotkey.Modifiers == pressedModifiers);

            if (match is null)
            {
                return false;
            }

            _consumedButtons.Add(mouseButtonEvent.Button);
        }

        _logger.Info($"Mouse hotkey dispatched id={match.Id} modifiers=0x{(uint)pressedModifiers:X} button={mouseButtonEvent.Button}.");
        DispatchCallback(match.Pressed);
        return true;
    }

    private void DispatchCallback(Action callback)
    {
        if (_callbackContext is not null)
        {
            var queuedAt = Stopwatch.GetTimestamp();
            _callbackContext.Post(_ =>
            {
                var startedAt = Stopwatch.GetTimestamp();
                _logger.Info($"Mouse hotkey callback started dispatchDelayMs={Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:F3}.");
                callback();
                _logger.Info($"Mouse hotkey callback returned elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
            }, null);
            return;
        }

        var directStartedAt = Stopwatch.GetTimestamp();
        callback();
        _logger.Info($"Mouse hotkey callback returned elapsedMs={Stopwatch.GetElapsedTime(directStartedAt).TotalMilliseconds:F3}.");
    }

    private bool HasDuplicateRegistration(HotkeyDefinition hotkey)
    {
        return _registrations.Values.Any(registration =>
            registration.Hotkey.Modifiers == hotkey.Modifiers
            && registration.Hotkey.MouseButton == hotkey.MouseButton);
    }

    private void EnsureHookInstalled()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _hookHandle = User32.SetWindowsHookEx(WhMouseLl, _hookProcedure, Kernel32.GetModuleHandle(null), 0);
        if (_hookHandle == IntPtr.Zero)
        {
            var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not install low-level mouse hook.");
            _logger.Error("Low-level mouse hook installation failed.", exception);
            throw exception;
        }

        _logger.Info($"Low-level mouse hook installed. Hook=0x{_hookHandle.ToInt64():X}");
    }

    private void Unregister(int id)
    {
        lock (_gate)
        {
            if (!_registrations.Remove(id))
            {
                return;
            }

            _logger.Info($"Mouse hotkey unregistered id={id}.");
            if (_registrations.Count == 0)
            {
                _consumedButtons.Clear();
                UninstallHook();
            }
        }
    }

    private void UninstallHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        if (User32.UnhookWindowsHookEx(_hookHandle))
        {
            _logger.Info("Low-level mouse hook uninstalled.");
        }
        else
        {
            var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not uninstall low-level mouse hook.");
            _logger.Error("Low-level mouse hook uninstall failed.", exception);
        }

        _hookHandle = IntPtr.Zero;
    }

    private static HotkeyModifiers ReadPressedModifiers()
    {
        var modifiers = HotkeyModifiers.None;

        if (IsKeyPressed(VkControl))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (IsKeyPressed(VkMenu))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (IsKeyPressed(VkShift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (IsKeyPressed(VkLWin) || IsKeyPressed(VkRWin))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        return modifiers;
    }

    private static bool IsKeyPressed(int virtualKey)
    {
        return (User32.GetAsyncKeyState(virtualKey) & KeyPressedMask) != 0;
    }

    internal readonly record struct MouseButtonEvent(HotkeyMouseButton Button, bool IsDown);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookStruct
    {
        public Point Point;

        public uint MouseData;

        public uint Flags;

        public uint Time;

        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;

        public int Y;
    }

    private sealed record RegistrationState(int Id, HotkeyDefinition Hotkey, Action Pressed);

    private sealed class MouseHotkeyRegistration : IDisposable
    {
        private readonly NativeMouseHotkeyHook _owner;
        private readonly int _id;
        private bool _disposed;

        public MouseHotkeyRegistration(NativeMouseHotkeyHook owner, int id)
        {
            _owner = owner;
            _id = id;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _owner.Unregister(_id);
            _disposed = true;
        }
    }

    private delegate IntPtr HookProcedure(int code, IntPtr wParam, IntPtr lParam);

    private static partial class User32
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProcedure hookProcedure, IntPtr moduleHandle, uint threadId);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnhookWindowsHookEx(IntPtr hookHandle);

        [LibraryImport("user32.dll")]
        public static partial IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll")]
        public static partial short GetAsyncKeyState(int virtualKey);
    }

    private static partial class Kernel32
    {
        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr GetModuleHandle(string? moduleName);
    }
}
