using System.ComponentModel;
using System.Runtime.InteropServices;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;

namespace HsWin.App.Keyboard;

/// <summary>
/// Global keyboard event source backed by WH_KEYBOARD_LL. Callback code must stay small because
/// Windows runs this hook before posting keyboard input to the target thread.
/// </summary>
internal sealed partial class NativeKeyboardEventService : IKeyboardEventService, IDisposable
{
    private const int HcAction = 0;
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint LlkhfInjected = 0x10;
    private const uint LlkhfUp = 0x80;
    private const short KeyPressedMask = unchecked((short)0x8000);

    private readonly object _gate = new();
    private readonly IRuntimeLogger _logger;
    private readonly HookProcedure _hookProcedure;
    private readonly KeyboardModifierTracker _modifierTracker = new();
    private readonly List<Subscription> _subscriptions = [];

    private IntPtr _hookHandle;
    private bool _disposed;
    private long _nextSubscriptionId;

    public NativeKeyboardEventService(IRuntimeLogger logger)
    {
        _logger = logger;
        _hookProcedure = HookCallback;
    }

    public IDisposable Watch(KeyboardEventWatchOptions options, Func<KeyboardEventSnapshot, bool> callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(callback);

        lock (_gate)
        {
            EnsureHookInstalled();

            var subscription = new Subscription(
                Interlocked.Increment(ref _nextSubscriptionId),
                options,
                callback,
                RemoveSubscription);
            _subscriptions.Add(subscription);
            _logger.Info(
                $"Keyboard watch registered id={subscription.Id} includeInjected={options.IncludeInjected} count={_subscriptions.Count}.");
            return subscription;
        }
    }

    public bool IsKeyDown(uint virtualKey)
    {
        return (User32.GetAsyncKeyState((int)virtualKey) & KeyPressedMask) != 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            foreach (var subscription in _subscriptions.ToArray())
            {
                subscription.MarkDisposed();
            }

            _subscriptions.Clear();
            _modifierTracker.Reset();
            UninstallHook();
            _disposed = true;
        }
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code != HcAction)
        {
            return User32.CallNextHookEx(_hookHandle, code, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var hookData = Marshal.PtrToStructure<KeyboardHookStruct>(lParam);
        var isKeyUp = IsKeyUpMessage(message, hookData.Flags);
        var isInjected = IsInjectedEvent(hookData);
        Subscription[] subscriptions;
        KeyboardEventSnapshot snapshot;

        lock (_gate)
        {
            if (!isInjected)
            {
                _modifierTracker.Apply(hookData.VkCode, isKeyUp);
            }

            if (_subscriptions.Count == 0)
            {
                return User32.CallNextHookEx(_hookHandle, code, wParam, lParam);
            }

            snapshot = CreateSnapshot(hookData.VkCode, isKeyUp, isInjected);
            subscriptions = [.. _subscriptions];
        }

        var shouldSwallow = false;
        foreach (var subscription in subscriptions)
        {
            if (subscription.IsDisposed || (isInjected && !subscription.Options.IncludeInjected))
            {
                continue;
            }

            try
            {
                shouldSwallow |= subscription.Callback(snapshot);
            }
            catch (Exception exception)
            {
                _logger.Error($"Keyboard watch callback failed id={subscription.Id}.", exception);
            }
        }

        return shouldSwallow
            ? new IntPtr(1)
            : User32.CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private KeyboardEventSnapshot CreateSnapshot(uint virtualKey, bool isKeyUp, bool isInjected)
    {
        var pressedModifiers = _modifierTracker.Pressed;
        return new KeyboardEventSnapshot(
            Type: isKeyUp ? "keyup" : "keydown",
            KeyCode: virtualKey,
            Key: KeyboardKeyRules.GetDisplayName(virtualKey),
            Modifiers: KeyboardKeyRules.GetModifierNames(pressedModifiers),
            ModifierFlags: (uint)pressedModifiers,
            IsKeyDown: !isKeyUp,
            IsKeyUp: isKeyUp,
            IsModifier: KeyboardKeyRules.IsModifierVirtualKey(virtualKey),
            IsInjected: isInjected,
            IsExtended: KeyboardKeyRules.IsExtendedVirtualKey(virtualKey));
    }

    private void RemoveSubscription(Subscription subscription)
    {
        lock (_gate)
        {
            if (!_subscriptions.Remove(subscription))
            {
                return;
            }

            _logger.Info($"Keyboard watch disposed id={subscription.Id} count={_subscriptions.Count}.");
            if (_subscriptions.Count == 0)
            {
                _modifierTracker.Reset();
                UninstallHook();
            }
        }
    }

    private void EnsureHookInstalled()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _hookHandle = User32.SetWindowsHookEx(WhKeyboardLl, _hookProcedure, Kernel32.GetModuleHandle(null), 0);
        if (_hookHandle == IntPtr.Zero)
        {
            var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not install WH_KEYBOARD_LL hook.");
            _logger.Error("Low-level keyboard hook installation failed.", exception);
            throw exception;
        }

        _logger.Info($"WH_KEYBOARD_LL hook installed. Hook=0x{_hookHandle.ToInt64():X}");
    }

    private void UninstallHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        if (User32.UnhookWindowsHookEx(_hookHandle))
        {
            _logger.Info("WH_KEYBOARD_LL hook uninstalled.");
        }
        else
        {
            var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not uninstall WH_KEYBOARD_LL hook.");
            _logger.Error("Low-level keyboard hook uninstall failed.", exception);
        }

        _hookHandle = IntPtr.Zero;
    }

    private static bool IsKeyUpMessage(int message, uint flags)
    {
        return message is WmKeyUp or WmSysKeyUp || (flags & LlkhfUp) != 0;
    }

    private static bool IsInjectedEvent(KeyboardHookStruct hookData)
    {
        return (hookData.Flags & LlkhfInjected) != 0
            || hookData.ExtraInfo == HsWin.App.Input.KeyboardInputSender.InjectedExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookStruct
    {
        public uint VkCode;

        public uint ScanCode;

        public uint Flags;

        public uint Time;

        public UIntPtr ExtraInfo;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action<Subscription> _dispose;
        private bool _disposed;

        public Subscription(
            long id,
            KeyboardEventWatchOptions options,
            Func<KeyboardEventSnapshot, bool> callback,
            Action<Subscription> dispose)
        {
            Id = id;
            Options = options;
            Callback = callback;
            _dispose = dispose;
        }

        public long Id { get; }

        public KeyboardEventWatchOptions Options { get; }

        public Func<KeyboardEventSnapshot, bool> Callback { get; }

        public bool IsDisposed => _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _dispose(this);
        }

        public void MarkDisposed()
        {
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
