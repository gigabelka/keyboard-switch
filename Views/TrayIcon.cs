using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
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
    private readonly Icon _iconEnabled;
    private readonly Icon _iconDisabled;

    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<bool>? EnabledToggled;

    public TrayIcon(string tooltip)
    {
        _iconEnabled = LoadIcon(true);
        _iconDisabled = LoadIcon(false);

        _notify = new NotifyIcon
        {
            Icon = _iconEnabled,
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
            _notify.Icon = toggle.Checked ? _iconEnabled : _iconDisabled;
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

    public void SetEnabledState(bool enabled)
    {
        _toggleEnabledItem.Checked = enabled;
        _notify.Icon = enabled ? _iconEnabled : _iconDisabled;
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int timeoutMs = 2000)
    {
        _notify.BalloonTipTitle = title;
        _notify.BalloonTipText = text;
        _notify.BalloonTipIcon = icon;
        _notify.ShowBalloonTip(timeoutMs);
    }

    private static Icon LoadIcon(bool enabled)
    {
        // Prefer a bundled tray.ico if a user has dropped one in; otherwise draw one.
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "tray.ico");
            if (File.Exists(path)) return new Icon(path);
        }
        catch { }
        return DrawIcon(enabled);
    }

    private static Icon DrawIcon(bool enabled)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // Left = EN (blue), right = RU (red). Desaturated when disabled.
            var leftColor = enabled
                ? Color.FromArgb(255, 52, 120, 246)
                : Color.FromArgb(255, 130, 130, 130);
            var rightColor = enabled
                ? Color.FromArgb(255, 220, 68, 74)
                : Color.FromArgb(255, 95, 95, 95);

            var rect = new Rectangle(1, 1, size - 2, size - 2);
            using var path = RoundedRect(rect, 6);

            g.SetClip(path);
            int mid = rect.X + rect.Width / 2;
            using (var lb = new SolidBrush(leftColor))
                g.FillRectangle(lb, rect.X, rect.Y, mid - rect.X, rect.Height);
            using (var rb = new SolidBrush(rightColor))
                g.FillRectangle(rb, mid, rect.Y, rect.Right - mid, rect.Height);

            // Subtle top-to-bottom highlight for a bit of depth.
            using (var gloss = new LinearGradientBrush(
                rect,
                Color.FromArgb(55, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical))
                g.FillRectangle(gloss, rect);
            g.ResetClip();

            // Thin outer border for contrast on both light and dark taskbars.
            using (var pen = new Pen(Color.FromArgb(110, 0, 0, 0), 1f))
                g.DrawPath(pen, path);

            // Letters.
            var leftRect = new RectangleF(rect.X, rect.Y - 1, mid - rect.X, rect.Height);
            var rightRect = new RectangleF(mid, rect.Y - 1, rect.Right - mid, rect.Height);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            using var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
            using var text = new SolidBrush(Color.White);

            var leftShadow = new RectangleF(leftRect.X, leftRect.Y + 1, leftRect.Width, leftRect.Height);
            var rightShadow = new RectangleF(rightRect.X, rightRect.Y + 1, rightRect.Width, rightRect.Height);
            g.DrawString("A", font, shadow, leftShadow, sf);
            g.DrawString("Я", font, shadow, rightShadow, sf);
            g.DrawString("A", font, text, leftRect, sf);
            g.DrawString("Я", font, text, rightRect, sf);
        }

        // GetHicon returns a handle we must destroy; reload into a standalone,
        // self-contained Icon via an ICO stream so the GDI handle can be freed.
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var handleIcon = Icon.FromHandle(hIcon);
            using var ms = new MemoryStream();
            handleIcon.Save(ms);
            ms.Position = 0;
            return new Icon(ms);
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _iconEnabled.Dispose();
        _iconDisabled.Dispose();
    }
}
