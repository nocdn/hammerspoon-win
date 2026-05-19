using System.Drawing;
using System.Windows.Forms;

namespace HammerspoonWin.App;

internal sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startAtLoginItem;
    private readonly Func<bool> _isStartAtLoginEnabled;
    private readonly Action<bool> _setStartAtLoginEnabled;
    private bool _disposed;

    public TrayIconService(
        Action openConfig,
        Action reloadConfig,
        Func<bool> isStartAtLoginEnabled,
        Action<bool> setStartAtLoginEnabled,
        Action quit)
    {
        _isStartAtLoginEnabled = isStartAtLoginEnabled;
        _setStartAtLoginEnabled = setStartAtLoginEnabled;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem("Open Config", image: null, onClick: (_, _) => openConfig()));
        contextMenu.Items.Add(new ToolStripMenuItem("Reload Config", image: null, onClick: (_, _) => reloadConfig()));

        _startAtLoginItem = new ToolStripMenuItem("Start at Login")
        {
            CheckOnClick = false
        };
        _startAtLoginItem.Click += (_, _) => ToggleStartAtLogin();
        contextMenu.Items.Add(_startAtLoginItem);

        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Quit", image: null, onClick: (_, _) => quit()));
        contextMenu.Opening += (_, _) => RefreshStartAtLoginState();

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = SystemIcons.Application,
            Text = "HammerspoonWin",
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
        RefreshStartAtLoginState();
    }

    private void RefreshStartAtLoginState()
    {
        _startAtLoginItem.Checked = _isStartAtLoginEnabled();
    }
}
