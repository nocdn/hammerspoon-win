using System.Diagnostics;
using HsWin.App.Keyboard;
using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;

namespace HsWin.App.Input;

internal sealed class KeyboardInputService : IKeyboardInputService
{
    private readonly IRuntimeLogger _logger;
    private readonly IKeyboardInputSender _inputSender;
    private readonly IKeyboardEventService _keyboardEvents;
    private readonly object _repeatStartGate = new();
    private readonly object _repeatGate = new();
    private KeyboardRepeatHandle? _activeRepeat;

    public KeyboardInputService(IRuntimeLogger logger)
        : this(logger, NativeKeyboardInputSender.Instance, NullKeyboardEventService.Instance)
    {
    }

    public KeyboardInputService(IRuntimeLogger logger, IKeyboardEventService keyboardEvents)
        : this(logger, NativeKeyboardInputSender.Instance, keyboardEvents)
    {
    }

    internal KeyboardInputService(
        IRuntimeLogger logger,
        IKeyboardInputSender inputSender,
        IKeyboardEventService? keyboardEvents = null)
    {
        _logger = logger;
        _inputSender = inputSender;
        _keyboardEvents = keyboardEvents ?? NullKeyboardEventService.Instance;
    }

    public void KeyDown(uint virtualKey)
    {
        var description = FormatInputAction("keyDown", virtualKey);
        if (KeyboardHookDispatchScope.TryDefer(
            () => _inputSender.SendKeyDown(virtualKey, KeyboardInputMethod.SendInput, _logger),
            description))
        {
            return;
        }

        _inputSender.SendKeyDown(virtualKey, KeyboardInputMethod.SendInput, _logger);
    }

    public void KeyUp(uint virtualKey)
    {
        var description = FormatInputAction("keyUp", virtualKey);
        if (KeyboardHookDispatchScope.TryDefer(
            () => _inputSender.SendKeyUp(virtualKey, KeyboardInputMethod.SendInput, _logger),
            description))
        {
            return;
        }

        _inputSender.SendKeyUp(virtualKey, KeyboardInputMethod.SendInput, _logger);
    }

    public void Tap(uint virtualKey, KeyboardTapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var suppressedModifiers = GetCurrentlyDownModifiers(options.SuppressPhysicalModifiers);
        var modifiers = KeyboardKeyRules.GetModifierVirtualKeys(options.Modifiers);
        var inputMethod = options.InputMethod;
        var description = FormatInputAction("tap", virtualKey);
        if (KeyboardHookDispatchScope.TryDefer(
            () => _inputSender.SendTap(virtualKey, suppressedModifiers, modifiers, inputMethod, _logger),
            description))
        {
            return;
        }

        _inputSender.SendTap(virtualKey, suppressedModifiers, modifiers, inputMethod, _logger);
    }

    public IKeyboardRepeatSession Repeat(uint virtualKey, KeyboardRepeatOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRepeatOptions(options);

        lock (_repeatStartGate)
        {
            KeyboardRepeatHandle? previousRepeat;
            lock (_repeatGate)
            {
                previousRepeat = _activeRepeat;
                _activeRepeat = null;
            }

            if (previousRepeat is not null)
            {
                _logger.Warning("Replacing active keyboard repeat before starting a new repeat.");
                previousRepeat.Dispose();
            }

            var suppressedModifiers = GetCurrentlyDownModifiers(options.SuppressPhysicalModifiers);
            var repeater = new KeyboardRepeatHandle(
                virtualKey,
                options,
                suppressedModifiers,
                _logger,
                _inputSender,
                _keyboardEvents,
                ClearActiveRepeat);
            lock (_repeatGate)
            {
                _activeRepeat = repeater;
            }

            try
            {
                repeater.Start();
                return repeater;
            }
            catch
            {
                ClearActiveRepeat(repeater);
                repeater.Dispose();
                throw;
            }
        }
    }

    public void StopActiveRepeat()
    {
        KeyboardRepeatHandle? active;
        lock (_repeatGate)
        {
            active = _activeRepeat;
            _activeRepeat = null;
        }

        if (active is null)
        {
            return;
        }

        _logger.Info("Stopping active keyboard repeat via StopActiveRepeat().");
        active.Dispose();
    }

    private void ClearActiveRepeat(KeyboardRepeatHandle repeat)
    {
        lock (_repeatGate)
        {
            if (ReferenceEquals(_activeRepeat, repeat))
            {
                _activeRepeat = null;
            }
        }
    }

    private IReadOnlyList<uint> GetCurrentlyDownModifiers(HotkeyModifiers modifiers)
    {
        // Release the *physical* L/R keys that are down, not only the generic VK_SHIFT/VK_CONTROL
        // codes. Games and the OS track left/right modifiers separately.
        var down = new List<uint>(4);

        if ((modifiers & HotkeyModifiers.Control) != 0)
        {
            AddIfDown(down, KeyboardKeyRules.VkLeftControl);
            AddIfDown(down, KeyboardKeyRules.VkRightControl);
            if (down.Count == 0)
            {
                AddIfDown(down, KeyboardKeyRules.VkControl);
            }
        }

        if ((modifiers & HotkeyModifiers.Shift) != 0)
        {
            var before = down.Count;
            AddIfDown(down, KeyboardKeyRules.VkLeftShift);
            AddIfDown(down, KeyboardKeyRules.VkRightShift);
            if (down.Count == before)
            {
                AddIfDown(down, KeyboardKeyRules.VkShift);
            }
        }

        if ((modifiers & HotkeyModifiers.Alt) != 0)
        {
            var before = down.Count;
            AddIfDown(down, KeyboardKeyRules.VkLeftMenu);
            AddIfDown(down, KeyboardKeyRules.VkRightMenu);
            if (down.Count == before)
            {
                AddIfDown(down, KeyboardKeyRules.VkMenu);
            }
        }

        if ((modifiers & HotkeyModifiers.Win) != 0)
        {
            AddIfDown(down, KeyboardKeyRules.VkLeftWin);
            AddIfDown(down, KeyboardKeyRules.VkRightWin);
        }

        return down;
    }

    private void AddIfDown(List<uint> keys, uint virtualKey)
    {
        if (_keyboardEvents.IsKeyDown(virtualKey))
        {
            keys.Add(virtualKey);
        }
    }

    private static string FormatInputAction(string action, uint virtualKey)
    {
        return $"{action} key='{KeyboardKeyRules.GetDisplayName(virtualKey)}' vk=0x{virtualKey:X2}";
    }

    private static void ValidateRepeatOptions(KeyboardRepeatOptions options)
    {
        if (options.IntervalMs is < KeyboardRepeatOptions.MinimumIntervalMs or > KeyboardRepeatOptions.MaximumIntervalMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"Keyboard repeat interval must be between {KeyboardRepeatOptions.MinimumIntervalMs} and {KeyboardRepeatOptions.MaximumIntervalMs} milliseconds.");
        }

        if (options.KeyDownMs < 0 || options.KeyDownMs >= options.IntervalMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"Keyboard repeat keyDownMs must be at least 0 and less than intervalMs ({options.IntervalMs}).");
        }
    }

    private sealed class KeyboardRepeatHandle : IKeyboardRepeatSession
    {
        private readonly uint _virtualKey;
        private readonly HotkeyModifiers _suppressPhysicalModifiers;
        private readonly KeyboardInputMethod _inputMethod;
        private readonly int _keyDownMs;
        private readonly IReadOnlyList<uint> _suppressedModifiers;
        private readonly IRuntimeLogger _logger;
        private readonly IKeyboardInputSender _inputSender;
        private readonly IKeyboardEventService _keyboardEvents;
        private readonly Action<KeyboardRepeatHandle> _clearActiveRepeat;
        private readonly object _gate = new();
        private readonly Stopwatch _stopwatch = new();
        private readonly System.Threading.Timer _timer;

        private int _intervalMs;
        private bool _disposed;
        private bool _repeatKeyIsDown;
        private long _tickCount;
        private long _lastLoggedTickCount;
        private IDisposable? _physicalModifierSuppression;

        public KeyboardRepeatHandle(
            uint virtualKey,
            KeyboardRepeatOptions options,
            IReadOnlyList<uint> suppressedModifiers,
            IRuntimeLogger logger,
            IKeyboardInputSender inputSender,
            IKeyboardEventService keyboardEvents,
            Action<KeyboardRepeatHandle> clearActiveRepeat)
        {
            _virtualKey = virtualKey;
            _intervalMs = options.IntervalMs;
            _suppressPhysicalModifiers = options.SuppressPhysicalModifiers;
            _inputMethod = options.InputMethod;
            _keyDownMs = options.KeyDownMs;
            _suppressedModifiers = suppressedModifiers;
            _logger = logger;
            _inputSender = inputSender;
            _keyboardEvents = keyboardEvents;
            _clearActiveRepeat = clearActiveRepeat;
            _timer = new System.Threading.Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public int IntervalMs
        {
            get
            {
                lock (_gate)
                {
                    return _intervalMs;
                }
            }
        }

        public void Start()
        {
            int intervalMs;
            lock (_gate)
            {
                intervalMs = _intervalMs;
            }

            _stopwatch.Start();
            RegisterPhysicalModifierSuppression();
            _logger.Info(
                $"Keyboard repeat started vk=0x{_virtualKey:X2} intervalMs={intervalMs} " +
                $"keyDownMs={_keyDownMs} " +
                $"suppressModifiers=0x{(uint)_suppressPhysicalModifiers:X} " +
                $"inputMethod={KeyboardInputMethodParser.GetDisplayName(_inputMethod)}.");
            ReleaseSuppressedModifiers();
            if (_keyDownMs == 0)
            {
                _inputSender.SendTap(_virtualKey, inputMethod: _inputMethod, logger: _logger);
                _timer.Change(intervalMs, intervalMs);
            }
            else
            {
                _inputSender.SendKeyDown(_virtualKey, _inputMethod, _logger);
                _repeatKeyIsDown = true;
                _timer.Change(_keyDownMs, Timeout.Infinite);
            }

            _tickCount++;
        }

        public void SetIntervalMs(int intervalMs)
        {
            if (intervalMs is < KeyboardRepeatOptions.MinimumIntervalMs or > KeyboardRepeatOptions.MaximumIntervalMs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intervalMs),
                    $"Keyboard repeat interval must be between {KeyboardRepeatOptions.MinimumIntervalMs} and {KeyboardRepeatOptions.MaximumIntervalMs} milliseconds.");
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (_intervalMs == intervalMs)
                {
                    return;
                }

                if (intervalMs <= _keyDownMs)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(intervalMs),
                        $"Keyboard repeat interval must be greater than keyDownMs ({_keyDownMs}).");
                }

                _intervalMs = intervalMs;
                if (_keyDownMs == 0)
                {
                    _timer.Change(intervalMs, intervalMs);
                }
                else if (!_repeatKeyIsDown)
                {
                    _timer.Change(intervalMs - _keyDownMs, Timeout.Infinite);
                }

                _logger.Info($"Keyboard repeat interval changed vk=0x{_virtualKey:X2} intervalMs={intervalMs}.");
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                _timer.Dispose();
                ReleaseRepeatKeys();
                _physicalModifierSuppression?.Dispose();
                _physicalModifierSuppression = null;
                _stopwatch.Stop();
                LogStop();
                _clearActiveRepeat(this);
            }
        }

        private void Tick()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    if (_keyDownMs == 0)
                    {
                        _inputSender.SendTap(_virtualKey, inputMethod: _inputMethod, logger: _logger);
                        _tickCount++;
                        LogProgressIfNeeded();
                        return;
                    }

                    if (_repeatKeyIsDown)
                    {
                        _inputSender.SendKeyUp(_virtualKey, _inputMethod, _logger);
                        _repeatKeyIsDown = false;
                        _timer.Change(_intervalMs - _keyDownMs, Timeout.Infinite);
                        return;
                    }

                    _inputSender.SendKeyDown(_virtualKey, _inputMethod, _logger);
                    _repeatKeyIsDown = true;
                    _tickCount++;
                    LogProgressIfNeeded();
                    _timer.Change(_keyDownMs, Timeout.Infinite);
                }
                catch (Exception exception)
                {
                    _logger.Error($"Keyboard repeat tick failed vk=0x{_virtualKey:X2} tick={_tickCount}.", exception);
                    Dispose();
                }
            }
        }

        private void ReleaseSuppressedModifiers()
        {
            foreach (var modifierVirtualKey in _suppressedModifiers)
            {
                // SendInput updates global key state; a matching window message also clears state
                // already observed by a game that ignores injected SendInput events.
                _inputSender.SendKeyUp(modifierVirtualKey, KeyboardInputMethod.SendInput, _logger);
                if (_inputMethod == KeyboardInputMethod.WindowMessage)
                {
                    _inputSender.SendKeyUp(modifierVirtualKey, KeyboardInputMethod.WindowMessage, _logger);
                }
            }
        }

        private void ReleaseRepeatKeys()
        {
            try
            {
                _inputSender.SendKeyUp(_virtualKey, _inputMethod, _logger);
                _repeatKeyIsDown = false;
                foreach (var modifierVirtualKey in _suppressedModifiers)
                {
                    _inputSender.SendKeyUp(modifierVirtualKey, KeyboardInputMethod.SendInput, _logger);
                    if (_inputMethod == KeyboardInputMethod.WindowMessage)
                    {
                        _inputSender.SendKeyUp(modifierVirtualKey, KeyboardInputMethod.WindowMessage, _logger);
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.Warning($"Keyboard repeat cleanup failed vk=0x{_virtualKey:X2}. {exception.Message}");
            }
        }

        private void RegisterPhysicalModifierSuppression()
        {
            if (_suppressPhysicalModifiers == HotkeyModifiers.None)
            {
                return;
            }

            var keyFilter = BuildPhysicalModifierKeyFilter(_suppressPhysicalModifiers);
            _physicalModifierSuppression = _keyboardEvents.Watch(
                new KeyboardEventWatchOptions(
                    IncludeInjected: false,
                    Blocking: true,
                    KeyFilter: keyFilter),
                _ => true);
        }

        private static IReadOnlySet<uint> BuildPhysicalModifierKeyFilter(HotkeyModifiers modifiers)
        {
            var keys = new HashSet<uint>();
            if ((modifiers & HotkeyModifiers.Control) != 0)
            {
                keys.Add(KeyboardKeyRules.VkControl);
                keys.Add(KeyboardKeyRules.VkLeftControl);
                keys.Add(KeyboardKeyRules.VkRightControl);
            }

            if ((modifiers & HotkeyModifiers.Shift) != 0)
            {
                keys.Add(KeyboardKeyRules.VkShift);
                keys.Add(KeyboardKeyRules.VkLeftShift);
                keys.Add(KeyboardKeyRules.VkRightShift);
            }

            if ((modifiers & HotkeyModifiers.Alt) != 0)
            {
                keys.Add(KeyboardKeyRules.VkMenu);
                keys.Add(KeyboardKeyRules.VkLeftMenu);
                keys.Add(KeyboardKeyRules.VkRightMenu);
            }

            if ((modifiers & HotkeyModifiers.Win) != 0)
            {
                keys.Add(KeyboardKeyRules.VkLeftWin);
                keys.Add(KeyboardKeyRules.VkRightWin);
            }

            return keys;
        }

        private void LogProgressIfNeeded()
        {
            if (_tickCount - _lastLoggedTickCount < 250)
            {
                return;
            }

            _lastLoggedTickCount = _tickCount;
            var elapsedMs = Math.Max(1, _stopwatch.Elapsed.TotalMilliseconds);
            var effectiveIntervalMs = elapsedMs / _tickCount;
            _logger.Info(
                $"Keyboard repeat progress vk=0x{_virtualKey:X2} ticks={_tickCount} elapsedMs={elapsedMs:F0} effectiveIntervalMs={effectiveIntervalMs:F2}.");
        }

        private void LogStop()
        {
            var elapsedMs = Math.Max(1, _stopwatch.Elapsed.TotalMilliseconds);
            var effectiveIntervalMs = elapsedMs / Math.Max(1, _tickCount);
            _logger.Info(
                $"Keyboard repeat stopped vk=0x{_virtualKey:X2} ticks={_tickCount} elapsedMs={elapsedMs:F0} requestedIntervalMs={_intervalMs} effectiveIntervalMs={effectiveIntervalMs:F2}.");
        }
    }
}
