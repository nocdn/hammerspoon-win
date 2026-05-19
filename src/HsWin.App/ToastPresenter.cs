using HsWin.Core.Alerts;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Point = System.Windows.Point;
using WpfApplication = System.Windows.Application;

namespace HsWin.App;

internal sealed class ToastPresenter : IAlertPresenter
{
    private readonly Dispatcher _dispatcher;
    private ToastWindow? _window;
    private DispatcherTimer? _timer;

    public ToastPresenter()
        : this(WpfApplication.Current.Dispatcher)
    {
    }

    public ToastPresenter(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Show(AlertRequest request)
    {
        if (_dispatcher.CheckAccess())
        {
            ShowOnDispatcher(request);
            return;
        }

        _dispatcher.BeginInvoke(() => ShowOnDispatcher(request));
    }

    private void ShowOnDispatcher(AlertRequest request)
    {
        CloseCurrentToast();

        if (request.DurationMs == 0)
        {
            return;
        }

        var window = new ToastWindow(request)
        {
            Left = -10_000,
            Top = -10_000
        };

        _window = window;
        window.Show();
        window.UpdateLayout();
        Position(window);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(request.DurationMs)
        };
        _timer.Tick += (_, _) => CloseCurrentToast();
        _timer.Start();
    }

    private void CloseCurrentToast()
    {
        _timer?.Stop();
        _timer = null;

        if (_window is null)
        {
            return;
        }

        _window.Close();
        _window = null;
    }

    private static void Position(Window window)
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var workingArea = screen.WorkingArea;
        var source = PresentationSource.FromVisual(window);
        var transformFromDevice = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;

        var topLeft = transformFromDevice.Transform(new Point(workingArea.Left, workingArea.Top));
        var bottomRight = transformFromDevice.Transform(new Point(workingArea.Right, workingArea.Bottom));
        const double bottomPadding = 48;

        window.Left = topLeft.X + ((bottomRight.X - topLeft.X - window.ActualWidth) / 2);
        window.Top = bottomRight.Y - window.ActualHeight - bottomPadding;
    }
}
