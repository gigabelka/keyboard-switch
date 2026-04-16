using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardSwitch.Views;

/// <summary>
/// Thin wrapper around <see cref="System.Windows.Forms.NotifyIcon"/> for use from a WPF app.
/// Uses Windows Forms NotifyIcon directly (enabled via UseWindowsForms). No NuGet dependencies.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly ToolStripMenuItem _toggleEnabledItem;

    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<bool>? EnabledToggled;

    public TrayIcon(string tooltip)
    {
        _notify = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = tooltip,
            Visible = true
        };

        var toggle = new ToolStripMenuItem("Включено")
        {
            Checked = true,
            CheckOnClick = false
        };
        toggle.Click += (_, _) =>
        {
            toggle.Checked = !toggle.Checked;
            EnabledToggled?.Invoke(this, toggle.Checked);
        };
        _toggleEnabledItem = toggle;

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Настройки…", null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(_toggleEnabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Выход", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));
        _notify.ContextMenuStrip = menu;

        _notify.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        };
        _notify.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetEnabledState(bool enabled) => _toggleEnabledItem.Checked = enabled;

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int timeoutMs = 2000)
    {
        _notify.BalloonTipTitle = title;
        _notify.BalloonTipText = text;
        _notify.BalloonTipIcon = icon;
        _notify.ShowBalloonTip(timeoutMs);
    }

    private static Icon LoadIcon()
    {
        // Try to load a bundled .ico next to the exe; fall back to a system icon.
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "tray.ico");
            if (System.IO.File.Exists(path)) return new Icon(path);
        }
        catch { }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
    }
}
