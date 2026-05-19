using HammerspoonWin.Core.Hotkeys;
using HammerspoonWin.Core.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HammerspoonWin.App.Hotkeys;

internal sealed partial class NativeHotkeyService : IHotkeyRegistrar, IDisposable
{
    private const int MaximumApplicationHotkeyId = 0xBFFF;
    private const int FirstHotkeyId = 1;
    private const int WmHotkey = 0x0312;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly MessageWindow _window;
    private readonly IRuntimeLogger _logger;
    private readonly Dictionary<int, Action> _callbacks = [];
    private int _nextId = FirstHotkeyId;
    private bool _disposed;

    public NativeHotkeyService()
        : this(NullRuntimeLogger.Instance)
    {
    }

    public NativeHotkeyService(IRuntimeLogger logger)
    {
        _logger = logger;
        _window = new MessageWindow(DispatchHotkey);
        _logger.Info($"Hotkey message window created. HWND=0x{_window.Handle.ToInt64():X}");
    }

    public IDisposable Register(HotkeyDefinition hotkey, Action pressed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(pressed);

        var id = AllocateId();
        var modifierFlags = (uint)(hotkey.Modifiers | HotkeyModifiers.NoRepeat);
        _logger.Info($"Registering hotkey id={id} modifiers=0x{modifierFlags:X} vk=0x{hotkey.VirtualKey:X2}.");

        if (!User32.RegisterHotKey(_window.Handle, id, modifierFlags, hotkey.VirtualKey))
        {
            var exception = new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not register hotkey {hotkey}.");
            _logger.Error($"Hotkey registration failed for id={id}.", exception);
            throw exception;
        }

        _callbacks[id] = pressed;
        _logger.Info($"Hotkey registered id={id}.");
        return new HotkeyRegistration(this, id);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var id in _callbacks.Keys.ToArray())
        {
            Unregister(id);
        }

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
            _logger.Info($"Hotkey dispatched id={id}.");
            callback();
            return;
        }

        _logger.Warning($"Received WM_HOTKEY for unknown id={id}.");
    }

    private void Unregister(int id)
    {
        if (!_callbacks.Remove(id))
        {
            return;
        }

        if (User32.UnregisterHotKey(_window.Handle, id))
        {
            _logger.Info($"Hotkey unregistered id={id}.");
            return;
        }

        var exception = new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not unregister hotkey id={id}.");
        _logger.Error($"Hotkey unregister failed id={id}.", exception);
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly Action<int> _dispatchHotkey;

        public MessageWindow(Action<int> dispatchHotkey)
        {
            _dispatchHotkey = dispatchHotkey;
            CreateHandle(new CreateParams
            {
                Caption = "HammerspoonWin Hotkey Message Window",
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
