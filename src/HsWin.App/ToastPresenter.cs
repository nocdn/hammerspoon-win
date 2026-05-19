using HsWin.Core.Alerts;
using HsWin.Core.Logging;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Point = System.Windows.Point;
using WpfApplication = System.Windows.Application;

namespace HsWin.App;

internal sealed class ToastPresenter : IAlertPresenter, IDisposable
{
    private const double HiddenWindowCoordinate = -1_000_000;

    private readonly Dispatcher _dispatcher;
    private readonly Func<IToastView> _createWindow;
    private readonly Action<IToastView> _positionWindow;
    private readonly IRuntimeLogger _logger;
    private readonly DispatcherTimer _timer;
    private IToastView? _window;
    private bool _disposed;

    public ToastPresenter()
        : this(WpfApplication.Current.Dispatcher, NullRuntimeLogger.Instance)
    {
    }

    public ToastPresenter(IRuntimeLogger logger)
        : this(WpfApplication.Current.Dispatcher, logger)
    {
    }

    public ToastPresenter(Dispatcher dispatcher)
        : this(dispatcher, NullRuntimeLogger.Instance)
    {
    }

    private ToastPresenter(Dispatcher dispatcher, IRuntimeLogger logger)
        : this(dispatcher, static () => new ToastWindow(), Position, logger)
    {
    }

    internal ToastPresenter(
        Dispatcher dispatcher,
        Func<IToastView> createWindow,
        Action<IToastView> positionWindow)
        : this(dispatcher, createWindow, positionWindow, NullRuntimeLogger.Instance)
    {
    }

    internal ToastPresenter(
        Dispatcher dispatcher,
        Func<IToastView> createWindow,
        Action<IToastView> positionWindow,
        IRuntimeLogger logger)
    {
        _dispatcher = dispatcher;
        _createWindow = createWindow;
        _positionWindow = positionWindow;
        _logger = logger;
        _timer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher);
        _timer.Tick += HideTimerTick;
    }

    public void Show(AlertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_dispatcher.CheckAccess())
        {
            ShowOnDispatcher(request);
            return;
        }

        var queuedAt = Stopwatch.GetTimestamp();
        _logger.Info($"Toast show queued text='{FormatTextForLog(request.Text)}' kind='{request.Kind}' durationMs={request.DurationMs}.");
        _dispatcher.InvokeAsync(() =>
        {
            if (!_disposed)
            {
                _logger.Info($"Toast show dequeued text='{FormatTextForLog(request.Text)}' dispatchDelayMs={Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:F3}.");
                ShowOnDispatcher(request);
            }
        }, DispatcherPriority.Send);
    }

    public void Prewarm()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_dispatcher.CheckAccess())
        {
            PrewarmOnDispatcher();
            return;
        }

        _dispatcher.InvokeAsync(PrewarmOnDispatcher, DispatcherPriority.Send);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            DisposeOnDispatcher();
            return;
        }

        _dispatcher.Invoke(DisposeOnDispatcher);
    }

    private void ShowOnDispatcher(AlertRequest request)
    {
        var startedAt = Stopwatch.GetTimestamp();
        StopTimer();

        if (request.DurationMs == 0)
        {
            HideCurrentToast();
            _logger.Info($"Toast hide timing text='{FormatTextForLog(request.Text)}' totalMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
            return;
        }

        var window = _window ??= _createWindow();
        MoveOffscreen(window);
        var movedAt = Stopwatch.GetTimestamp();
        window.UpdateRequest(request);
        var updatedAt = Stopwatch.GetTimestamp();

        var wasVisible = window.IsVisible;
        if (!window.IsVisible)
        {
            window.Show();
        }
        var shownAt = Stopwatch.GetTimestamp();

        window.UpdateLayout();
        var layoutAt = Stopwatch.GetTimestamp();
        _positionWindow(window);
        var positionedAt = Stopwatch.GetTimestamp();
        StartTimer(request.DurationMs);

        _logger.Info(
            $"Toast show timing text='{FormatTextForLog(request.Text)}' kind='{request.Kind}' alreadyVisible={wasVisible} " +
            $"moveMs={ElapsedMs(startedAt, movedAt):F3} updateMs={ElapsedMs(movedAt, updatedAt):F3} " +
            $"showMs={ElapsedMs(updatedAt, shownAt):F3} layoutMs={ElapsedMs(shownAt, layoutAt):F3} " +
            $"positionMs={ElapsedMs(layoutAt, positionedAt):F3} totalMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
    }

    private void DisposeOnDispatcher()
    {
        if (_disposed)
        {
            return;
        }

        StopTimer();
        _window?.Close();
        _window = null;
        _timer.Tick -= HideTimerTick;
        _disposed = true;
    }

    private void HideCurrentToast()
    {
        StopTimer();
        if (_window is not null)
        {
            MoveOffscreen(_window);
        }
    }

    private void HideTimerTick(object? sender, EventArgs e)
    {
        HideCurrentToast();
    }

    private void StopTimer()
    {
        _timer.Stop();
    }

    private void StartTimer(int durationMs)
    {
        _timer.Interval = TimeSpan.FromMilliseconds(durationMs);
        _timer.Start();
    }

    private void PrewarmOnDispatcher()
    {
        if (_disposed || _window is not null)
        {
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var window = _window = _createWindow();
        var createdAt = Stopwatch.GetTimestamp();
        MoveOffscreen(window);
        window.UpdateRequest(AlertRequest.Create("Ready", AlertKind.Normal, 1));
        var updatedAt = Stopwatch.GetTimestamp();
        window.Show();
        var shownAt = Stopwatch.GetTimestamp();
        window.UpdateLayout();
        MoveOffscreen(window);
        var finishedAt = Stopwatch.GetTimestamp();

        _logger.Info(
            $"Toast prewarm timing createMs={ElapsedMs(startedAt, createdAt):F3} updateMs={ElapsedMs(createdAt, updatedAt):F3} " +
            $"showMs={ElapsedMs(updatedAt, shownAt):F3} layoutMs={ElapsedMs(shownAt, finishedAt):F3} totalMs={Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F3}.");
    }

    private static void MoveOffscreen(IToastView window)
    {
        window.Left = HiddenWindowCoordinate;
        window.Top = HiddenWindowCoordinate;
    }

    private static void Position(IToastView window)
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var workingArea = screen.WorkingArea;
        var source = PresentationSource.FromVisual(window.PlacementVisual);
        var transformFromDevice = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;

        var topLeft = transformFromDevice.Transform(new Point(workingArea.Left, workingArea.Top));
        var bottomRight = transformFromDevice.Transform(new Point(workingArea.Right, workingArea.Bottom));
        const double bottomPadding = 48;

        window.Left = topLeft.X + ((bottomRight.X - topLeft.X - window.ActualWidth) / 2);
        window.Top = bottomRight.Y - window.ActualHeight - bottomPadding;
    }

    private static double ElapsedMs(long startTimestamp, long endTimestamp)
    {
        return Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;
    }

    private static string FormatTextForLog(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
