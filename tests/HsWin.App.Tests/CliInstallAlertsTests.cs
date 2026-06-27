using HsWin.Core.Alerts;

namespace HsWin.App.Tests;

public sealed class CliInstallAlertsTests
{
    [Fact]
    public void InstallingAlertUsesLoaderAndLongDuration()
    {
        var request = CliInstallAlerts.CreateInstallingAlert();

        Assert.Equal("Installing hspn CLI...", request.Text);
        Assert.Equal(AlertKind.Normal, request.Kind);
        Assert.Equal(AlertIcon.Loader, request.EffectiveIcon);
        Assert.Equal(CliInstallAlerts.InstallingDurationMs, request.DurationMs);
    }

    [Fact]
    public void InstalledAlertKeepsExistingSuccessText()
    {
        var request = CliInstallAlerts.CreateInstalledAlert();

        Assert.Equal("hspn CLI installed. Open a new terminal to use it.", request.Text);
        Assert.Equal(AlertKind.Success, request.Kind);
        Assert.Equal(AlertIcon.Dot, request.EffectiveIcon);
        Assert.Equal(CliInstallAlerts.InstalledDurationMs, request.DurationMs);
    }
}
