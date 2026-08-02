using HsWin.Core.Hotkeys;
using HsWin.Core.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;

namespace HsWin.App.Hotkeys;

internal sealed partial class NativeHotkeyService : IHotkeyRegistrar, IDisposable
{
    private const int MaximumApplicationHotkeyId = 0xBFFF;
    private const int FirstHotkeyId = 1;
    private const int WmHotkey = 0x0312;
    private const int ErrorHotkeyAlreadyRegistered = 1409;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly MessageWindow _window;
    private readonly IRuntimeLogger _logger;
    private readonly NativeMouseHotkeyHook _mouseHotkeys;
    private readonly IHotkeyThreadInvoker _threadInvoker;
    private readonly IHotkeyPlatform _platform;
    private readonly Dictionary<int, Action> _callbacks = [];
    private int _nextId = FirstHotkeyId;
    private bool _disposed;

    public NativeHotkeyService()
        : this(NullRuntimeLogger.Instance)
    {
    }

    public NativeHotkeyService(IRuntimeLogger logger)
        : this(logger, new DispatcherHotkeyThreadInvoker(Dispatcher.CurrentDispatcher), NativeHotkeyPlatform.Instance)
    {
    }

    internal NativeHotkeyService(
        IRuntimeLogger logger,
        IHotkeyThreadInvoker threadInvoker,
        IHotkeyPlatform platform)
        : this(logger, threadInvoker, platform, mouseHotkeys: null)
    {
    }

    internal NativeHotkeyService(
        IRuntimeLogger logger,
        IHotkeyThreadInvoker threadInvoker,
        IHotkeyPlatform platform,
        NativeMouseHotkeyHook? mouseHotkeys)
    {
        _logger = logger;
        _threadInvoker = threadInvoker;
        _platform = platform;
        _mouseHotkeys = mouseHotkeys ?? new NativeMouseHotkeyHook(_logger);
        _window = new MessageWindow(DispatchHotkey);
        _logger.Info($"Hotkey message window created. HWND=0x{_window.Handle.ToInt64():X}");
    }

    /// <summary>
    /// Shared low-level mouse hook used for button hotkeys and scroll watches.
    /// </summary>
    internal NativeMouseHotkeyHook MouseHook => _mouseHotkeys;

    public IDisposable Register(HotkeyDefinition hotkey, Action pressed)
    {
        if (!_threadInvoker.CheckAccess())
        {
            return _threadInvoker.Invoke(() => RegisterOnOwnerThread(hotkey, pressed));
        }

        return RegisterOnOwnerThread(hotkey, pressed);
    }

    public IDisposable RegisterHeld(HotkeyDefinition hotkey, Action pressed, Action released, bool blocking)
    {
        if (!_threadInvoker.CheckAccess())
        {
            return _threadInvoker.Invoke(() => RegisterHeldOnOwnerThread(hotkey, pressed, released, blocking));
        }

        return RegisterHeldOnOwnerThread(hotkey, pressed, released, blocking);
    }

    public void Dispose()
    {
        if (!_threadInvoker.CheckAccess())
        {
            _threadInvoker.Invoke(DisposeOnOwnerThread);
            return;
        }

        DisposeOnOwnerThread();
    }

    private IDisposable RegisterOnOwnerThread(HotkeyDefinition hotkey, Action pressed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(pressed);

        if (hotkey.InputKind == HotkeyInputKind.MouseButton)
        {
            return _mouseHotkeys.Register(hotkey, pressed);
        }

        var id = AllocateId();
        var modifierFlags = (uint)(hotkey.Modifiers | HotkeyModifiers.NoRepeat);
        _logger.Info($"Registering hotkey id={id} modifiers=0x{modifierFlags:X} vk=0x{hotkey.VirtualKey:X2}.");

        if (!_platform.RegisterHotKey(_window.Handle, id, modifierFlags, hotkey.VirtualKey, out var errorCode))
        {
            var message = CreateRegistrationFailureMessage(errorCode, hotkey);
            var exception = new Win32Exception(errorCode, message);
            _logger.Error($"Hotkey registration failed for id={id}.", exception);
            throw exception;
        }

        _callbacks[id] = pressed;
        _logger.Info($"Hotkey registered id={id}.");
        return new HotkeyRegistration(this, id);
    }

    private IDisposable RegisterHeldOnOwnerThread(
        HotkeyDefinition hotkey,
        Action pressed,
        Action released,
        bool blocking)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(released);

        if (hotkey.InputKind != HotkeyInputKind.MouseButton)
        {
            throw new ArgumentException("Held hotkey registration requires a mouse-button definition.", nameof(hotkey));
        }

        return _mouseHotkeys.RegisterHeld(hotkey, pressed, released, blocking);
    }

    private void DisposeOnOwnerThread()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var id in _callbacks.Keys.ToArray())
        {
            Unregister(id);
        }

        _mouseHotkeys.Dispose();
        _logger.Info("Mouse hotkey service disposed.");

        _window.DestroyHandle();
        _logger.Info("Hotkey message window destroyed.");
        _disposed = true;
    }

    private int AllocateId()
    {
        if (_nextId > MaximumApplicationHotkeyId)
        {
            throw new InvalidOperationException("No hotkey registration identifiers are available.");
        }

        return _nextId++;
    }

    private void DispatchHotkey(int id)
    {
        if (_callbacks.TryGetValue(id, out var callback))
        {
            var startedAt = Stopwatch.GetTimestamp();
            _logger.Info($"Hotkey dispatched id={id}.");
            callback();
            _logger.Info($"Hotkey callback returned id={id} elapsedMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
            return;
        }

        _logger.Warning($"Received WM_HOTKEY for unknown id={id}.");
    }

    private void Unregister(int id)
    {
        if (!_threadInvoker.CheckAccess())
        {
            _threadInvoker.Invoke(() => UnregisterOnOwnerThread(id));
            return;
        }

        UnregisterOnOwnerThread(id);
    }

    private void UnregisterOnOwnerThread(int id)
    {
        if (!_callbacks.Remove(id))
        {
            return;
        }

        if (_platform.UnregisterHotKey(_window.Handle, id, out var errorCode))
        {
            _logger.Info($"Hotkey unregistered id={id}.");
            return;
        }

        var exception = new Win32Exception(errorCode, $"Could not unregister hotkey id={id}.");
        _logger.Error($"Hotkey unregister failed id={id}.", exception);
    }

    internal static string CreateRegistrationFailureMessage(int errorCode, HotkeyDefinition hotkey)
    {
        return errorCode == ErrorHotkeyAlreadyRegistered
            ? $"Hotkey already in use: {hotkey}."
            : $"Could not register hotkey {hotkey}.";
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly Action<int> _dispatchHotkey;

        public MessageWindow(Action<int> dispatchHotkey)
        {
            _dispatchHotkey = dispatchHotkey;
            CreateHandle(new CreateParams
            {
                Caption = $"{AppBranding.DisplayName} Hotkey Message Window",
                Parent = HwndMessage
            });
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmHotkey)
            {
                _dispatchHotkey(message.WParam.ToInt32());
                return;
            }

            base.WndProc(ref message);
        }
    }

    private sealed class HotkeyRegistration : IDisposable
    {
        private readonly NativeHotkeyService _owner;
        private readonly int _id;
        private bool _disposed;

        public HotkeyRegistration(NativeHotkeyService owner, int id)
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

    internal interface IHotkeyThreadInvoker
    {
        bool CheckAccess();

        T Invoke<T>(Func<T> callback);

        void Invoke(Action callback);
    }

    private sealed class DispatcherHotkeyThreadInvoker : IHotkeyThreadInvoker
    {
        private readonly Dispatcher _dispatcher;

        public DispatcherHotkeyThreadInvoker(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public bool CheckAccess()
        {
            return _dispatcher.CheckAccess();
        }

        public T Invoke<T>(Func<T> callback)
        {
            return _dispatcher.Invoke(callback);
        }

        public void Invoke(Action callback)
        {
            _dispatcher.Invoke(callback);
        }
    }

    internal interface IHotkeyPlatform
    {
        bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey, out int errorCode);

        bool UnregisterHotKey(IntPtr windowHandle, int id, out int errorCode);
    }

    private sealed class NativeHotkeyPlatform : IHotkeyPlatform
    {
        public static NativeHotkeyPlatform Instance { get; } = new();

        public bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey, out int errorCode)
        {
            if (User32.RegisterHotKey(windowHandle, id, modifiers, virtualKey))
            {
                errorCode = 0;
                return true;
            }

            errorCode = Marshal.GetLastPInvokeError();
            return false;
        }

        public bool UnregisterHotKey(IntPtr windowHandle, int id, out int errorCode)
        {
            if (User32.UnregisterHotKey(windowHandle, id))
            {
                errorCode = 0;
                return true;
            }

            errorCode = Marshal.GetLastPInvokeError();
            return false;
        }
    }

    private static partial class User32
    {
        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
