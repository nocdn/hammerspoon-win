using System.Reflection;
using System.Windows.Forms;

namespace HsWin.App.Tests;

public sealed class TrayIconServiceTests
{
    [Fact]
    public void ContextMenuShowsTheRunningAppVersion()
    {
        using var service = new TrayIconService(
            openConfig: () => { },
            reloadConfig: () => { },
            isStartAtLoginEnabled: () => false,
            setStartAtLoginEnabled: _ => { },
            isCliInstalled: () => true,
            installCli: () => { },
            quit: () => { });

        var notifyIcon = (NotifyIcon?)typeof(TrayIconService)
            .GetField("_notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(service);

        Assert.NotNull(notifyIcon);
        var contextMenu = notifyIcon!.ContextMenuStrip;
        Assert.NotNull(contextMenu);
        var versionItem = Assert.Single(
            contextMenu!.Items.OfType<ToolStripMenuItem>(),
            item => item?.Text?.StartsWith("Version ", StringComparison.Ordinal) == true);

        Assert.Equal($"Version {AppBranding.Version}", versionItem.Text);
        Assert.False(versionItem.Enabled);
    }
}
