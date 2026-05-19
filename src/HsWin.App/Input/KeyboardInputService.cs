using System.Runtime.InteropServices;
using System.Diagnostics;
using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;

namespace HsWin.App.Input;

internal sealed partial class KeyboardInputService : IKeyboardInputService
{
    private const short KeyPressedMask = unchecked((short)0x8000);

    private readonly IRuntimeLogger _logger;

    public KeyboardInputService(IRuntimeLogger logger)
    {
        _logger = logger;
    }

    public void KeyDown(uint virtualKey)
    {
        KeyboardInputSender.SendKeyDown(virtualKey, _logger);
    }

    public void KeyUp(uint virtualKey)
    {
        KeyboardInputSender.SendKeyUp(virtualKey, _logger);
    }

    public void Tap(uint virtualKey, KeyboardTapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var suppressedModifiers = GetCurrentlyDownModifiers(options.SuppressPhysicalModifiers);
        KeyboardInputSender.SendTap(virtualKey, suppressedModifiers, _logger);
    }

    public IDisposable Repeat(uint virtualKey, KeyboardRepeatOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var suppressedModifiers = GetCurrentlyDownModifiers(options.SuppressPhysicalModifiers);
        var repeater = new KeyboardRepeatHandle(virtualKey, options, suppressedModifiers, _logger);
        repeater.Start();
        return repeater;
    }

    private static IReadOnlyList<uint> GetCurrentlyDownModifiers(HotkeyModifiers modifiers)
    {
        var virtualKeys = KeyboardKeyRules.GetModifierVirtualKeys(modifiers);
        if (virtualKeys.Count == 0)
        {
            return [];
        }

        return [.. virtualKeys.Where(IsKeyDown)];
    }

    private static bool IsKeyDown(uint virtualKey)
    {
        return (User32.GetAsyncKeyState((int)virtualKey) & KeyPressedMask) != 0;
    }

    private static partial class User32
    {
        [LibraryImport("user32.dll")]
        public static partial short GetAsyncKeyState(int virtualKey);
    }

    private sealed class KeyboardRepeatHandle : IDisposable
    {
        private readonly uint _virtualKey;
        private readonly KeyboardRepeatOptions _options;
        private readonly IReadOnlyList<uint> _suppressedModifiers;
        private readonly IRuntimeLogger _logger;
        private readonly object _gate = new();
        private readonly Stopwatch _stopwatch = new();
        private readonly System.Threading.Timer _timer;

        private bool _disposed;
        private long _tickCount;
        private long _lastLoggedTickCount;

        public KeyboardRepeatHandle(
            uint virtualKey,
            KeyboardRepeatOptions options,
            IReadOnlyList<uint> suppressedModifiers,
            IRuntimeLogger logger)
        {
            _virtualKey = virtualKey;
            _options = options;
            _suppressedModifiers = suppressedModifiers;
            _logger = logger;
            _timer = new System.Threading.Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            _stopwatch.Start();
            _logger.Info(
                $"Keyboard repeat started vk=0x{_virtualKey:X2} intervalMs={_options.IntervalMs} suppressModifiers=0x{(uint)_options.SuppressPhysicalModifiers:X}.");
            KeyboardInputSender.SendTap(_virtualKey, _suppressedModifiers, _logger);
            _tickCount++;
            _timer.Change(_options.IntervalMs, _options.IntervalMs);
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
                _stopwatch.Stop();
                LogStop();
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
                    KeyboardInputSender.SendTap(_virtualKey, _suppressedModifiers, _logger);
                    _tickCount++;
                    LogProgressIfNeeded();
                }
                catch (Exception exception)
                {
                    _logger.Error($"Keyboard repeat tick failed vk=0x{_virtualKey:X2} tick={_tickCount}.", exception);
                    Dispose();
                }
            }
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
                $"Keyboard repeat stopped vk=0x{_virtualKey:X2} ticks={_tickCount} elapsedMs={elapsedMs:F0} requestedIntervalMs={_options.IntervalMs} effectiveIntervalMs={effectiveIntervalMs:F2}.");
        }
    }
}
