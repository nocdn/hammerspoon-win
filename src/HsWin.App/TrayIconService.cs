using System.Drawing;
using System.Windows.Forms;

namespace HsWin.App;

internal sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startAtLoginItem;
    private readonly ToolStripMenuItem _installCliItem;
    private readonly Func<bool> _isStartAtLoginEnabled;
    private readonly Action<bool> _setStartAtLoginEnabled;
    private readonly Func<bool> _isCliInstalled;
    private readonly Action _installCli;
    private bool _disposed;

    public TrayIconService(
        Action openConfig,
        Action reloadConfig,
        Action emergencyStop,
        Func<bool> isStartAtLoginEnabled,
        Action<bool> setStartAtLoginEnabled,
        Func<bool> isCliInstalled,
        Action installCli,
        Action quit)
    {
        _isStartAtLoginEnabled = isStartAtLoginEnabled;
        _setStartAtLoginEnabled = setStartAtLoginEnabled;
        _isCliInstalled = isCliInstalled;
        _installCli = installCli;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem("Open Config", image: null, onClick: (_, _) => openConfig()));
        contextMenu.Items.Add(new ToolStripMenuItem("Reload Config", image: null, onClick: (_, _) => reloadConfig()));
        contextMenu.Items.Add(new ToolStripMenuItem(
            "Emergency Stop (Ctrl+Alt+Shift+Esc)",
            image: null,
            onClick: (_, _) => emergencyStop()));

        _startAtLoginItem = new ToolStripMenuItem("Start at Login")
        {
            CheckOnClick = false
        };
        _startAtLoginItem.Click += (_, _) => ToggleStartAtLogin();
        contextMenu.Items.Add(_startAtLoginItem);

        _installCliItem = new ToolStripMenuItem("Install hspn CLI");
        _installCliItem.Click += (_, _) => InstallCli();
        contextMenu.Items.Add(_installCliItem);

        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem($"Version {AppBranding.Version}")
        {
            Enabled = false
        });
        contextMenu.Items.Add(new ToolStripMenuItem("Quit", image: null, onClick: (_, _) => quit()));
        contextMenu.Opening += (_, _) => RefreshMenuState();

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = SystemIcons.Application,
            Text = AppBranding.DisplayName,
            Visible = false
        };
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
    }

    private void ToggleStartAtLogin()
    {
        _setStartAtLoginEnabled(!_isStartAtLoginEnabled());
        RefreshMenuState();
    }

    private void InstallCli()
    {
        _installCli();
        RefreshMenuState();
    }

    private void RefreshMenuState()
    {
        _startAtLoginItem.Checked = _isStartAtLoginEnabled();
        var cliInstalled = _isCliInstalled();
        _installCliItem.Enabled = !cliInstalled;
    }
}
