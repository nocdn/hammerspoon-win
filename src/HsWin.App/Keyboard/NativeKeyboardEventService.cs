using System.ComponentModel;
using System.Runtime.InteropServices;
using HsWin.Core.Hotkeys;
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
    private const int WmQuit = 0x0012;
    private const uint LlkhfInjected = 0x10;
    private const uint LlkhfUp = 0x80;
    private static readonly TimeSpan HookInstallTimeout = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly IRuntimeLogger _logger;
    private readonly HookProcedure _hookProcedure;
    private readonly KeyboardWatchDispatcher _watchDispatcher;
    private readonly KeyboardModifierTracker _modifierTracker = new();
    private readonly List<KeyboardWatchSubscription> _subscriptions = [];

    private IntPtr _hookHandle;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private Exception? _hookInstallException;
    private bool _disposed;
    private long _nextSubscriptionId;
    private Func<KeyboardEventSnapshot, bool>? _hostPriorityHandler;

    public NativeKeyboardEventService(IRuntimeLogger logger)
        : this(
            logger,
            new KeyboardWatchDispatcher(
                logger,
                new SynchronizationContextKeyboardWatchCallbackScheduler(SynchronizationContext.Current)))
    {
    }

    internal NativeKeyboardEventService(IRuntimeLogger logger, KeyboardWatchDispatcher watchDispatcher)
    {
        _logger = logger;
        _watchDispatcher = watchDispatcher;
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

            var subscription = new KeyboardWatchSubscription(
                Interlocked.Increment(ref _nextSubscriptionId),
                options,
                callback,
                RemoveSubscription);
            if (options.Prepend)
            {
                _subscriptions.Insert(0, subscription);
            }
            else
            {
                _subscriptions.Add(subscription);
            }

            _logger.Info(
                $"Keyboard watch registered id={subscription.Id} includeInjected={options.IncludeInjected} blocking={options.Blocking} " +
                $"prepend={options.Prepend} keys={FormatKeyFilter(options.KeyFilter)} count={_subscriptions.Count}.");
            return subscription;
        }
    }

    /// <summary>
    /// Installs a host-only handler that runs on the keyboard hook path before any script watchers.
    /// Used for emergency stop so it cannot be ordered behind config remaps or blocked by script lock.
    /// Ensures the hook is installed even when no script watchers exist.
    /// </summary>
    public void SetHostPriorityHandler(Func<KeyboardEventSnapshot, bool>? handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            _hostPriorityHandler = handler;
            if (handler is not null)
            {
                EnsureHookInstalled();
            }
            else if (_subscriptions.Count == 0)
            {
                _modifierTracker.Reset();
                UninstallHook();
            }
        }

        _logger.Info(handler is null
            ? "Keyboard host priority handler cleared."
            : "Keyboard host priority handler registered.");
    }

    public bool IsKeyDown(uint virtualKey)
    {
        var modifier = KeyboardKeyRules.ModifierForVirtualKey(virtualKey);
        if (modifier != HotkeyModifiers.None)
        {
            lock (_gate)
            {
                // SendInput key-up events intentionally alter GetAsyncKeyState even though the
                // user still physically holds the modifier. The LL hook tracker ignores injected
                // events, so it remains the source of truth while our hook is installed.
                if (_hookHandle != IntPtr.Zero)
                {
                    return IsTrackedModifierDown(virtualKey, _modifierTracker.Pressed);
                }
            }
        }

        return NativeKeyStateReader.IsDown(virtualKey);
    }

    internal static bool IsTrackedModifierDown(uint virtualKey, HotkeyModifiers pressedModifiers)
    {
        var modifier = KeyboardKeyRules.ModifierForVirtualKey(virtualKey);
        return modifier != HotkeyModifiers.None && (pressedModifiers & modifier) != 0;
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
            _hostPriorityHandler = null;
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
        KeyboardWatchSubscription[] subscriptions;
        KeyboardEventSnapshot snapshot;
        Func<KeyboardEventSnapshot, bool>? hostPriorityHandler;

        lock (_gate)
        {
            if (!isInjected)
            {
                _modifierTracker.Apply(hookData.VkCode, isKeyUp);
            }

            hostPriorityHandler = _hostPriorityHandler;
            if (hostPriorityHandler is null && _subscriptions.Count == 0)
            {
                return User32.CallNextHookEx(_hookHandle, code, wParam, lParam);
            }

            snapshot = CreateSnapshot(hookData.VkCode, isKeyUp, isInjected);
            subscriptions = _subscriptions.Count == 0 ? [] : [.. _subscriptions];
        }

        // Host priority (emergency stop) always runs first, never behind script remaps/watchers.
        if (hostPriorityHandler is not null)
        {
            try
            {
                if (hostPriorityHandler(snapshot))
                {
                    return new IntPtr(1);
                }
            }
            catch (Exception exception)
            {
                _logger.Error("Keyboard host priority handler failed.", exception);
            }
        }

        if (subscriptions.Length == 0)
        {
            return User32.CallNextHookEx(_hookHandle, code, wParam, lParam);
        }

        bool shouldSwallow;
        using (KeyboardHookDispatchScope.Enter(_logger, FormatKeyboardEvent(snapshot, hookData, message)))
        {
            shouldSwallow = _watchDispatcher.Dispatch(snapshot, subscriptions);
            LogKeyboardEventDispatch(snapshot, hookData, message, shouldSwallow);
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

    private void RemoveSubscription(KeyboardWatchSubscription subscription)
    {
        lock (_gate)
        {
            if (!_subscriptions.Remove(subscription))
            {
                return;
            }

            _logger.Info($"Keyboard watch disposed id={subscription.Id} count={_subscriptions.Count}.");
            if (_subscriptions.Count == 0 && _hostPriorityHandler is null)
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

        _hookInstallException = null;
        using var ready = new ManualResetEventSlim();
        _hookThread = new Thread(() => HookThreadMain(ready))
        {
            IsBackground = true,
            Name = "HsWin Keyboard Hook"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        if (!ready.Wait(HookInstallTimeout))
        {
            // Best-effort: stop a late-finishing install thread so we do not leave an orphaned hook.
            if (_hookThreadId != 0)
            {
                _ = User32.PostThreadMessage(_hookThreadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
            }

            if (_hookHandle != IntPtr.Zero)
            {
                _ = User32.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            _hookThreadId = 0;
            _hookThread = null;
            var exception = new TimeoutException($"Timed out after {HookInstallTimeout.TotalMilliseconds:F0}ms while installing WH_KEYBOARD_LL hook.");
            _logger.Error("Low-level keyboard hook installation timed out.", exception);
            throw exception;
        }

        if (_hookInstallException is not null)
        {
            throw _hookInstallException;
        }

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("WH_KEYBOARD_LL hook thread started without publishing a hook handle.");
        }
    }

    private void HookThreadMain(ManualResetEventSlim ready)
    {
        _hookThreadId = Kernel32.GetCurrentThreadId();

        var hookHandle = User32.SetWindowsHookEx(WhKeyboardLl, _hookProcedure, Kernel32.GetModuleHandle(null), 0);
        if (hookHandle == IntPtr.Zero)
        {
            var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not install WH_KEYBOARD_LL hook.");
            _hookInstallException = exception;
            _logger.Error("Low-level keyboard hook installation failed.", exception);
            ready.Set();
            return;
        }

        _hookHandle = hookHandle;
        _logger.Info($"WH_KEYBOARD_LL hook installed. Hook=0x{hookHandle.ToInt64():X} threadId={_hookThreadId}.");
        ready.Set();

        while (User32.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            User32.TranslateMessage(ref message);
            User32.DispatchMessage(ref message);
        }

        _logger.Info($"Keyboard hook thread exited threadId={_hookThreadId}.");
    }

    private void UninstallHook()
    {
        var hookHandle = _hookHandle;
        if (hookHandle == IntPtr.Zero)
        {
            return;
        }

        if (User32.UnhookWindowsHookEx(hookHandle))
        {
            _logger.Info("WH_KEYBOARD_LL hook uninstalled.");
        }
        else
        {
            var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not uninstall WH_KEYBOARD_LL hook.");
            _logger.Error("Low-level keyboard hook uninstall failed.", exception);
        }

        _hookHandle = IntPtr.Zero;
        if (_hookThreadId != 0)
        {
            if (!User32.PostThreadMessage(_hookThreadId, WmQuit, IntPtr.Zero, IntPtr.Zero))
            {
                var exception = new Win32Exception(Marshal.GetLastPInvokeError(), "Could not stop the keyboard hook thread.");
                _logger.Error("Keyboard hook thread stop signal failed.", exception);
            }
        }

        _hookThreadId = 0;
        _hookThread = null;
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

    private void LogKeyboardEventDispatch(
        KeyboardEventSnapshot snapshot,
        KeyboardHookStruct hookData,
        int message,
        bool shouldSwallow)
    {
        if (!shouldSwallow && !IsNavigationDiagnosticKey(snapshot.KeyCode))
        {
            return;
        }

        _logger.Info(
            $"Keyboard event key='{snapshot.Key}' type='{snapshot.Type}' vk=0x{snapshot.KeyCode:X2} scan=0x{hookData.ScanCode:X2} " +
            $"flags=0x{hookData.Flags:X2} message=0x{message:X4} injected={snapshot.IsInjected} extended={snapshot.IsExtended} " +
            $"swallow={shouldSwallow} deferredActions={KeyboardHookDispatchScope.CurrentDeferredActionCount}.");
    }

    private static bool IsNavigationDiagnosticKey(uint virtualKey)
    {
        return virtualKey is 0x21 or 0x22 or 0x23 or 0x24;
    }

    private static string FormatKeyFilter(IReadOnlySet<uint>? keyFilter)
    {
        return keyFilter is { Count: > 0 }
            ? string.Join(",", keyFilter.Select(key => $"0x{key:X2}"))
            : "<all>";
    }

    private static string FormatKeyboardEvent(
        KeyboardEventSnapshot snapshot,
        KeyboardHookStruct hookData,
        int message)
    {
        return
            $"key='{snapshot.Key}' type='{snapshot.Type}' vk=0x{snapshot.KeyCode:X2} scan=0x{hookData.ScanCode:X2} " +
            $"flags=0x{hookData.Flags:X2} message=0x{message:X4} injected={snapshot.IsInjected} extended={snapshot.IsExtended}";
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

        [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
        public static partial int GetMessage(out NativeMessage message, IntPtr windowHandle, uint messageFilterMin, uint messageFilterMax);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool TranslateMessage(ref NativeMessage message);

        [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
        public static partial IntPtr DispatchMessage(ref NativeMessage message);

        [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PostThreadMessage(uint threadId, int message, IntPtr wParam, IntPtr lParam);
    }

    private static partial class Kernel32
    {
        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr GetModuleHandle(string? moduleName);

        [LibraryImport("kernel32.dll")]
        public static partial uint GetCurrentThreadId();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Hwnd;

        public uint Message;

        public IntPtr WParam;

        public IntPtr LParam;

        public uint Time;

        public NativePoint Point;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;

        public int Y;
    }
}
