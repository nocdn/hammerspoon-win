using System.Diagnostics;
using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Input;

internal sealed class MouseInputService : IMouseInputService
{
    private readonly IRuntimeLogger _logger;
    private readonly IMouseInputSender _inputSender;
    private readonly IMouseRepeatTimerFactory _timerFactory;
    private readonly object _repeatStartGate = new();
    private readonly object _repeatGate = new();
    private MouseRepeatHandle? _activeRepeat;

    public MouseInputService(IRuntimeLogger logger)
        : this(logger, NativeMouseInputSender.Instance)
    {
    }

    internal MouseInputService(IRuntimeLogger logger, IMouseInputSender inputSender)
        : this(logger, inputSender, SystemMouseRepeatTimerFactory.Instance)
    {
    }

    internal MouseInputService(
        IRuntimeLogger logger,
        IMouseInputSender inputSender,
        IMouseRepeatTimerFactory timerFactory)
    {
        _logger = logger;
        _inputSender = inputSender;
        _timerFactory = timerFactory;
    }

    public void Click(MouseButton button)
    {
        _inputSender.SendClick(button, MouseInputMethod.SendInput, _logger);
    }

    public IMouseRepeatSession Repeat(MouseButton button, MouseRepeatOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_repeatStartGate)
        {
            MouseRepeatHandle? previousRepeat;
            lock (_repeatGate)
            {
                previousRepeat = _activeRepeat;
                _activeRepeat = null;
            }

            if (previousRepeat is not null)
            {
                _logger.Warning("Replacing active mouse repeat before starting a new repeat.");
                previousRepeat.Dispose();
            }

            var repeater = new MouseRepeatHandle(
                button,
                options,
                _logger,
                _inputSender,
                _timerFactory,
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
        MouseRepeatHandle? active;
        lock (_repeatGate)
        {
            active = _activeRepeat;
            _activeRepeat = null;
        }

        if (active is null)
        {
            return;
        }

        _logger.Info("Stopping active mouse repeat via StopActiveRepeat().");
        active.Dispose();
    }

    private void ClearActiveRepeat(MouseRepeatHandle repeat)
    {
        lock (_repeatGate)
        {
            if (ReferenceEquals(_activeRepeat, repeat))
            {
                _activeRepeat = null;
            }
        }
    }

    private sealed class MouseRepeatHandle : IMouseRepeatSession
    {
        private readonly MouseButton _button;
        private readonly IRuntimeLogger _logger;
        private readonly IMouseInputSender _inputSender;
        private readonly Action<MouseRepeatHandle> _clearActiveRepeat;
        private readonly object _gate = new();
        private readonly Stopwatch _stopwatch = new();
        private readonly IMouseRepeatTimer _timer;

        private MouseInputMethod _inputMethod;
        private int _intervalMs;
        private bool _disposed;
        private long _clickCount;
        private long _lastLoggedClickCount;

        public MouseRepeatHandle(
            MouseButton button,
            MouseRepeatOptions options,
            IRuntimeLogger logger,
            IMouseInputSender inputSender,
            IMouseRepeatTimerFactory timerFactory,
            Action<MouseRepeatHandle> clearActiveRepeat)
        {
            _button = button;
            _intervalMs = options.IntervalMs;
            _inputMethod = options.InputMethod;
            _logger = logger;
            _inputSender = inputSender;
            _clearActiveRepeat = clearActiveRepeat;
            _timer = timerFactory.Create(Tick);
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
            MouseInputMethod inputMethod;
            lock (_gate)
            {
                intervalMs = _intervalMs;
                inputMethod = _inputMethod;
            }

            _stopwatch.Start();
            _logger.Info(
                $"Mouse repeat started button={MouseButtonParser.GetDisplayName(_button)} " +
                $"intervalMs={intervalMs} inputMethod={MouseInputMethodParser.GetDisplayName(inputMethod)}.");
            _inputSender.SendClick(_button, inputMethod, _logger);
            _clickCount++;
            _timer.Change(intervalMs, intervalMs);
        }

        public void SetIntervalMs(int intervalMs)
        {
            if (intervalMs is < MouseRepeatOptions.MinimumIntervalMs or > MouseRepeatOptions.MaximumIntervalMs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intervalMs),
                    $"Mouse repeat interval must be between {MouseRepeatOptions.MinimumIntervalMs} and {MouseRepeatOptions.MaximumIntervalMs} milliseconds.");
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

                _intervalMs = intervalMs;
                _timer.Change(intervalMs, intervalMs);
                _logger.Info(
                    $"Mouse repeat interval changed button={MouseButtonParser.GetDisplayName(_button)} intervalMs={intervalMs}.");
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
                _stopwatch.Stop();
                LogStop();
                _clearActiveRepeat(this);
            }
        }

        private void Tick()
        {
            MouseInputMethod inputMethod;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                inputMethod = _inputMethod;
            }

            try
            {
                _inputSender.SendClick(_button, inputMethod, _logger);
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _clickCount++;
                    LogProgressIfNeeded();
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    $"Mouse repeat tick failed button={MouseButtonParser.GetDisplayName(_button)} click={_clickCount}.",
                    exception);
                Dispose();
            }
        }

        private void LogProgressIfNeeded()
        {
            if (_clickCount - _lastLoggedClickCount < 50)
            {
                return;
            }

            _lastLoggedClickCount = _clickCount;
            var elapsedMs = Math.Max(1, _stopwatch.Elapsed.TotalMilliseconds);
            var effectiveIntervalMs = elapsedMs / _clickCount;
            _logger.Info(
                $"Mouse repeat progress button={MouseButtonParser.GetDisplayName(_button)} clicks={_clickCount} " +
                $"elapsedMs={elapsedMs:F0} effectiveIntervalMs={effectiveIntervalMs:F2}.");
        }

        private void LogStop()
        {
            var elapsedMs = Math.Max(1, _stopwatch.Elapsed.TotalMilliseconds);
            var effectiveIntervalMs = elapsedMs / Math.Max(1, _clickCount);
            _logger.Info(
                $"Mouse repeat stopped button={MouseButtonParser.GetDisplayName(_button)} clicks={_clickCount} " +
                $"elapsedMs={elapsedMs:F0} requestedIntervalMs={_intervalMs} effectiveIntervalMs={effectiveIntervalMs:F2}.");
        }
    }

    private sealed class NativeMouseInputSender : IMouseInputSender
    {
        public static NativeMouseInputSender Instance { get; } = new();

        private NativeMouseInputSender()
        {
        }

        public void SendClick(MouseButton button, MouseInputMethod inputMethod, IRuntimeLogger? logger = null)
        {
            if (inputMethod == MouseInputMethod.WindowMessage)
            {
                WindowMessageMouseInputSender.SendClick(button, logger);
                return;
            }

            MouseInputSender.SendClick(button, logger);
        }
    }
}
