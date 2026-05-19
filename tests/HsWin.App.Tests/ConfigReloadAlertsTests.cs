using System.ComponentModel;
using HsWin.Core.Alerts;

namespace HsWin.App.Tests;

public sealed class ConfigReloadAlertsTests
{
    [Fact]
    public void ReloadingAlertStaysVisibleUntilReplaced()
    {
        var request = ConfigReloadAlerts.CreateReloadingAlert();

        Assert.Equal("Reloading config…", request.Text);
        Assert.Equal(AlertKind.Normal, request.Kind);
        Assert.Equal(ConfigReloadAlerts.ReloadingDurationMs, request.DurationMs);
        Assert.Equal(250, ConfigReloadAlerts.MinimumReloadingVisibleMs);
    }

    [Fact]
    public void ReloadedAlertUsesLongerSuccessDuration()
    {
        var request = ConfigReloadAlerts.CreateReloadedAlert();

        Assert.Equal("Config reloaded", request.Text);
        Assert.Equal(AlertKind.Success, request.Kind);
        Assert.Equal(ConfigReloadAlerts.ReloadedDurationMs, request.DurationMs);
    }

    [Fact]
    public void ReloadFailedAlertUsesReadableMessageAndLongerDuration()
    {
        var request = ConfigReloadAlerts.CreateReloadFailedAlert(
            new Win32Exception("Hotkey already in use: Alt, Control+R."));

        Assert.Contains("Hotkey already in use", request.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("config.js", request.Text, StringComparison.Ordinal);
        Assert.Equal(AlertKind.Error, request.Kind);
        Assert.Equal(ConfigReloadAlerts.ReloadFailedDurationMs, request.DurationMs);
    }
}
