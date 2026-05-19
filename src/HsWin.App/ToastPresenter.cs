using HsWin.Core.Alerts;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Point = System.Windows.Point;
using WpfApplication = System.Windows.Application;

namespace HsWin.App;

internal sealed class ToastPresenter : IAlertPresenter, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Func<IToastView> _createWindow;
    private readonly Action<IToastView> _positionWindow;
    private IToastView? _window;
    private DispatcherTimer? _timer;
    private bool _disposed;

    public ToastPresenter()
        : this(WpfApplication.Current.Dispatcher)
    {
    }

    public ToastPresenter(Dispatcher dispatcher)
        : this(dispatcher, static () => new ToastWindow(), Position)
    {
    }

    internal ToastPresenter(
        Dispatcher dispatcher,
        Func<IToastView> createWindow,
        Action<IToastView> positionWindow)
    {
        _dispatcher = dispatcher;
        _createWindow = createWindow;
        _positionWindow = positionWindow;
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

        _dispatcher.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                ShowOnDispatcher(request);
            }
        });
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
        StopTimer();

        if (request.DurationMs == 0)
        {
            HideCurrentToast();
            return;
        }

        var window = _window ??= _createWindow();
        window.UpdateRequest(request);

        if (!window.IsVisible)
        {
            window.Left = -10_000;
            window.Top = -10_000;
            window.Show();
        }

        window.UpdateLayout();
        _positionWindow(window);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(request.DurationMs)
        };
        _timer.Tick += HideTimerTick;
        _timer.Start();
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
        _disposed = true;
    }

    private void HideCurrentToast()
    {
        StopTimer();
        _window?.Hide();
    }

    private void HideTimerTick(object? sender, EventArgs e)
    {
        HideCurrentToast();
    }

    private void StopTimer()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= HideTimerTick;
        _timer = null;
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
}
