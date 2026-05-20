using System.Windows;
using WpfApplication = System.Windows.Application;

namespace HsWin.App;

public partial class App : WpfApplication
{
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _controller = new AppController();
            _controller.Start();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"Could not start {AppBranding.DisplayName}.\n\n{exception.Message}",
                AppBranding.DisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
