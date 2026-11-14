using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace Xianpu.Desktop;

/// <summary>
/// Manages the Windows system tray icon, context menu, and balloon notifications.
/// </summary>
public sealed class TrayService : IDisposable
{
    private const string IconResourceName = "Xianpu.Desktop.Assets.StringNote.ico";

    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ContextMenuStrip _contextMenu;
    private readonly Drawing.Icon _trayIcon;
    private readonly bool _ownsIcon;
    private bool _minimizedTipShown;
    private bool _disposed;

    public TrayService(Action onShowRequested, Action onCheckUpdateRequested, Action onExitRequested)
    {
        ArgumentNullException.ThrowIfNull(onShowRequested);
        ArgumentNullException.ThrowIfNull(onCheckUpdateRequested);
        ArgumentNullException.ThrowIfNull(onExitRequested);

        (_trayIcon, _ownsIcon) = LoadTrayIcon();

        _contextMenu = new WinForms.ContextMenuStrip();
        _contextMenu.Items.Add("打开弦笺", null, (_, _) => onShowRequested());
        _contextMenu.Items.Add("检查更新", null, (_, _) => onCheckUpdateRequested());
        _contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        _contextMenu.Items.Add("退出弦笺", null, (_, _) => onExitRequested());

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "弦笺 · StringNote",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                onShowRequested();
            }
        };
        _notifyIcon.DoubleClick += (_, _) => onShowRequested();
    }

    public void ShowMinimizedTipOnce()
    {
        if (_disposed || _minimizedTipShown)
            return;

        _minimizedTipShown = true;
        _notifyIcon.ShowBalloonTip(
            2500,
            "弦笺 · StringNote",
            "已最小化到系统托盘，点击右下角图标可随时打开。",
            WinForms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();

        if (_ownsIcon)
        {
            _trayIcon.Dispose();
        }
    }

    private static (Drawing.Icon Icon, bool OwnsIcon) LoadTrayIcon()
    {
        try
        {
            using var stream = typeof(TrayService).Assembly.GetManifestResourceStream(IconResourceName);
            if (stream is not null)
            {
                return (new Drawing.Icon(stream), true);
            }
        }
        catch
        {
            // Fall back to system application icon if embedded resource fails to load.
        }

        return (Drawing.SystemIcons.Application, false);
    }
}
