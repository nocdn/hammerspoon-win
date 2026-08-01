using System.Diagnostics;
using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Input;

internal sealed class MouseInputService : IMouseInputService
{
    private readonly IRuntimeLogger _logger;
    private readonly IMouseInputSender _inputSender;
    private readonly object _repeatStartGate = new();
    private readonly object _repeatGate = new();
    private MouseRepeatHandle? _activeRepeat;

    public MouseInputService(IRuntimeLogger logger)
        : this(logger, NativeMouseInputSender.Instance)
    {
    }

    internal MouseInputService(IRuntimeLogger logger, IMouseInputSender inputSender)
    {
        _logger = logger;
        _inputSender = inputSender;
    }

    public void Click(MouseButton button)
    {
        _inputSender.SendClick(button, MouseInputMethod.SendInput, _logger);
    }

    public IDisposable Repeat(MouseButton button, MouseRepeatOptions options)
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

            var repeater = new MouseRepeatHandle(button, options, _logger, _inputSender, ClearActiveRepeat);
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

    private sealed class MouseRepeatHandle : IDisposable
    {
        private readonly MouseButton _button;
        private readonly MouseRepeatOptions _options;
        private readonly IRuntimeLogger _logger;
        private readonly IMouseInputSender _inputSender;
        private readonly Action<MouseRepeatHandle> _clearActiveRepeat;
        private readonly object _gate = new();
        private readonly Stopwatch _stopwatch = new();
        private readonly System.Threading.Timer _timer;

        private bool _disposed;
        private long _clickCount;
        private long _lastLoggedClickCount;

        public MouseRepeatHandle(
            MouseButton button,
            MouseRepeatOptions options,
            IRuntimeLogger logger,
            IMouseInputSender inputSender,
            Action<MouseRepeatHandle> clearActiveRepeat)
        {
            _button = button;
            _options = options;
            _logger = logger;
            _inputSender = inputSender;
            _clearActiveRepeat = clearActiveRepeat;
            _timer = new System.Threading.Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            _stopwatch.Start();
            _logger.Info(
                $"Mouse repeat started button={MouseButtonParser.GetDisplayName(_button)} " +
                $"intervalMs={_options.IntervalMs} inputMethod={MouseInputMethodParser.GetDisplayName(_options.InputMethod)}.");
            _inputSender.SendClick(_button, _options.InputMethod, _logger);
            _clickCount++;
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
                    _inputSender.SendClick(_button, _options.InputMethod, _logger);
                    _clickCount++;
                    LogProgressIfNeeded();
                }
                catch (Exception exception)
                {
                    _logger.Error(
                        $"Mouse repeat tick failed button={MouseButtonParser.GetDisplayName(_button)} click={_clickCount}.",
                        exception);
                    Dispose();
                }
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
                $"elapsedMs={elapsedMs:F0} requestedIntervalMs={_options.IntervalMs} effectiveIntervalMs={effectiveIntervalMs:F2}.");
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
