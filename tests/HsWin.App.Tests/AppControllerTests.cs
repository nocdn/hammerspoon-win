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
        Assert.Equal(2000, request.DurationMs);
    }
}
