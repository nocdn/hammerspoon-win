using HsWin.Core.Alerts;

namespace HsWin.App.Tests;

public sealed class AppControllerTests
{
    [Fact]
    public void CreateConfigReloadedAlertUsesSuccessToastText()
    {
        var request = AppController.CreateConfigReloadedAlert();

        Assert.Equal("Config reloaded", request.Text);
        Assert.Equal(AlertKind.Success, request.Kind);
        Assert.Equal(AlertIcon.Dot, request.EffectiveIcon);
        Assert.Equal(ConfigReloadAlerts.ReloadedDurationMs, request.DurationMs);
    }

    [Fact]
    public void CreateConfigReloadingAlertUsesNormalKind()
    {
        var request = AppController.CreateConfigReloadingAlert();

        Assert.Equal("Reloading config…", request.Text);
        Assert.Equal(AlertKind.Normal, request.Kind);
        Assert.Equal(AlertIcon.Loader, request.EffectiveIcon);
    }
}
