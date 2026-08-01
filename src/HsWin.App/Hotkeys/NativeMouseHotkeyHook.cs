using HsWin.Core.Hotkeys;
using HsWin.Core.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HsWin.App.Hotkeys;

internal sealed class NativeMouseHotkeyHook : IDisposable
{
    private const int HcAction = 0;
    private const int WhMouseLl = 14;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const int WmQuit = 0x0012;
    private const int XButton1 = 0x0001;
    private const int XButton2 = 0x0002;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const short KeyPressedMask = unchecked((short)0x8000);
    private static readonly TimeSpan HookInstallTimeout = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly IRuntimeLogger _logger;
    private readonly IMouseHookPlatform _platform;
    private readonly SynchronizationContext? _callbackContext;
    private readonly Dictionary<int, RegistrationState> _registrations = [];
    private readonly Dictionary<HotkeyMouseButton, RegistrationState> _activeRegistrations = [];
    private readonly MouseHookProcedure _hookProcedure;
    private IntPtr _hookHandle;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private Exception? _hookInstallException;
    private int _nextId = 1;
    private bool _disposed;

    public NativeMouseHotkeyHook(IRuntimeLogger logger)
        : this(logger, Win32MouseHookPlatform.Instance, SynchronizationContext.Current)
    {
    }

    internal NativeMouseHotkeyHook(
        IRuntimeLogger logger,
        IMouseHookPlatform platform,
        SynchronizationContext? callbackContext)
    {
        _logger = logger;
        _platform = platform;
        _callbackContext = callbackContext;
        _hookProcedure = HookCallback;
    }

    public IDisposable Register(HotkeyDefinition hotkey, Action pressed)
    {
        return RegisterInternal(hotkey, pressed, released: null, blocking: true);
    }

    public IDisposable RegisterHeld(HotkeyDefinition hotkey, Action pressed, Action released, bool blocking)
    {
        return RegisterInternal(hotkey, pressed, released, blocking);
    }

    private IDisposable RegisterInternal(
        HotkeyDefinition hotkey,
        Action pressed,
        Action? released,
        bool blocking)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(pressed);
        if (released is null && !blocking)
        {
            throw new ArgumentException("A non-blocking mouse hotkey must provide a release callback.", nameof(released));
        }

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
            _registrations[id] = new RegistrationState(id, hotkey, pressed, released, blocking);
            _logger.Info(
                $"Mouse hotkey registered id={id} modifiers=0x{(uint)hotkey.Modifiers:X} button={hotkey.MouseButton} " +
                $"held={released is not null} blocking={blocking}.");
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
            _activeRegistrations.Clear();
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

        return _platform.CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private bool TryHandleMouseButtonEvent(MouseButtonEvent mouseButtonEvent)
    {
        if (!mouseButtonEvent.IsDown)
        {
            RegistrationState? activeRegistration;
            lock (_gate)
            {
                if (!_activeRegistrations.Remove(mouseButtonEvent.Button, out activeRegistration))
                {
                    return false;
                }
            }

            _logger.Info($"Mouse hotkey button-up consumed button={mouseButtonEvent.Button}.");
            if (activeRegistration.Released is not null)
            {
                DispatchCallback(activeRegistration.Released);
            }

            return activeRegistration.Blocking;
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

            _activeRegistrations[mouseButtonEvent.Button] = match;
        }

        _logger.Info($"Mouse hotkey dispatched id={match.Id} modifiers=0x{(uint)pressedModifiers:X} button={mouseButtonEvent.Button}.");
        DispatchCallback(match.Pressed);
        return match.Blocking;
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

        _hookInstallException = null;
        using var ready = new ManualResetEventSlim();
        _hookThread = new Thread(() => HookThreadMain(ready))
        {
            IsBackground = true,
            Name = "HsWin Mouse Hook"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        if (!ready.Wait(HookInstallTimeout))
        {
            var exception = new TimeoutException($"Timed out after {HookInstallTimeout.TotalMilliseconds:F0}ms while installing WH_MOUSE_LL hook.");
            _logger.Error("Low-level mouse hook installation timed out.", exception);
            throw exception;
        }

        if (_hookInstallException is not null)
        {
            throw _hookInstallException;
        }

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("WH_MOUSE_LL hook thread started without publishing a hook handle.");
        }
    }

    private void HookThreadMain(ManualResetEventSlim ready)
    {
        _hookThreadId = _platform.GetCurrentThreadId();

        var hookHandle = _platform.SetWindowsHookEx(WhMouseLl, _hookProcedure, _platform.GetModuleHandle(null), 0);
        if (hookHandle == IntPtr.Zero)
        {
            var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not install low-level mouse hook.");
            _hookInstallException = exception;
            _logger.Error("Low-level mouse hook installation failed.", exception);
            ready.Set();
            return;
        }

        _hookHandle = hookHandle;
        _logger.Info($"Low-level mouse hook installed. Hook=0x{hookHandle.ToInt64():X} threadId={_hookThreadId}.");
        ready.Set();

        while (_platform.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            _platform.TranslateMessage(ref message);
            _platform.DispatchMessage(ref message);
        }

        _logger.Info($"Mouse hook thread exited threadId={_hookThreadId}.");
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
                _activeRegistrations.Clear();
                UninstallHook();
            }
        }
    }

    private void UninstallHook()
    {
        var hookHandle = _hookHandle;
        if (hookHandle == IntPtr.Zero)
        {
            return;
        }

        if (_platform.UnhookWindowsHookEx(hookHandle))
        {
            _logger.Info("Low-level mouse hook uninstalled.");
        }
        else
        {
            var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not uninstall low-level mouse hook.");
            _logger.Error("Low-level mouse hook uninstall failed.", exception);
        }

        _hookHandle = IntPtr.Zero;
        if (_hookThreadId != 0)
        {
            if (!_platform.PostThreadMessage(_hookThreadId, WmQuit, IntPtr.Zero, IntPtr.Zero))
            {
                var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not stop the mouse hook thread.");
                _logger.Error("Mouse hook thread stop signal failed.", exception);
            }
        }

        _hookThreadId = 0;
        _hookThread = null;
    }

    private HotkeyModifiers ReadPressedModifiers()
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

    private bool IsKeyPressed(int virtualKey)
    {
        return (_platform.GetAsyncKeyState(virtualKey) & KeyPressedMask) != 0;
    }

    internal readonly record struct MouseButtonEvent(HotkeyMouseButton Button, bool IsDown);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookStruct
    {
        public MousePoint Point;

        public uint MouseData;

        public uint Flags;

        public uint Time;

        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MousePoint
    {
        public int X;

        public int Y;
    }

    private sealed record RegistrationState(
        int Id,
        HotkeyDefinition Hotkey,
        Action Pressed,
        Action? Released,
        bool Blocking);

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
}
