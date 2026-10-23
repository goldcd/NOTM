using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NOTM.Core.Native;
using NOTM.Models;

namespace NOTM.Core.Tray;

public class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _guardToggleItem;
    private bool _isDisposed;

    public event Action? OnOpenRequested;
    public event Action<bool>? OnGuardToggled;
    public event Action? OnIdentifyRequested;
    public event Action? OnResetPrimaryRequested;
    public event Action? OnExitRequested;

    public TrayIconService()
    {
        _contextMenu = new ContextMenuStrip();

        // 1. Launch GUI option
        var openItem = new ToolStripMenuItem("Open NOTM", null, (s, e) => OnOpenRequested?.Invoke())
        {
            Font = new Font(_contextMenu.Font ?? SystemFonts.DefaultFont, FontStyle.Bold)
        };

        // 2. Toggle App On/Off option
        _guardToggleItem = new ToolStripMenuItem("Guard: Active (Click to Toggle)", null, (s, e) =>
        {
            if (s is ToolStripMenuItem item)
            {
                bool newState = !item.Checked;
                SetGuardState(newState);
                OnGuardToggled?.Invoke(newState);
            }
        })
        {
            Checked = true
        };

        var identifyItem = new ToolStripMenuItem("Identify Displays", null, (s, e) => OnIdentifyRequested?.Invoke());
        var resetItem = new ToolStripMenuItem("Reset Target to Windows Primary", null, (s, e) => OnResetPrimaryRequested?.Invoke());
        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => OnExitRequested?.Invoke());

        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(_guardToggleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(identifyItem);
        _contextMenu.Items.Add(resetItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = "NOTM - Monitor Guard",
            Visible = true,
            ContextMenuStrip = _contextMenu,
            Icon = CreateMonitorIcon(true)
        };

        // Clicking the tray icon presents the options immediately
        _notifyIcon.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _contextMenu.Show(System.Windows.Forms.Cursor.Position);
            }
        };

        _notifyIcon.DoubleClick += (s, e) => OnOpenRequested?.Invoke();
    }

    public void SetGuardState(bool isEnabled)
    {
        _guardToggleItem.Checked = isEnabled;
        _guardToggleItem.Text = isEnabled ? "Guard: Active (Click to Pause)" : "Guard: Paused (Click to Enable)";
        UpdateIconState(isEnabled);
        _notifyIcon.Text = isEnabled ? "NOTM - Monitor Guard (Active)" : "NOTM - Monitor Guard (Paused)";
    }

    private void UpdateIconState(bool active)
    {
        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = CreateMonitorIcon(active);
        oldIcon?.Dispose();
    }

    public void ShowRelocationNotification(RelocationLogItem item)
    {
        _notifyIcon.ShowBalloonTip(
            2500,
            "NOTM: Window Relocated",
            $"Moved \"{item.ProcessName}\" from {item.SourceMonitor} to {item.TargetMonitor}",
            ToolTipIcon.Info);
    }

    private static Icon CreateMonitorIcon(bool withForbiddenOverlay)
    {
        // 32x32 crisp vector glyph
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Monitor Glyph: Stand
        using var standPen = new Pen(System.Drawing.Color.FromArgb(220, 225, 235), 2.5f);
        g.DrawLine(standPen, 16, 21, 16, 25);
        g.DrawLine(standPen, 10, 26, 22, 26);

        // Monitor Glyph: Screen Bezel & Fill
        using var screenBg = new SolidBrush(System.Drawing.Color.FromArgb(200, 24, 26, 32));
        using var bezelPen = new Pen(System.Drawing.Color.FromArgb(225, 230, 240), 2f);
        
        // Rounded screen rectangle
        var screenRect = new Rectangle(2, 2, 27, 18);
        g.FillRectangle(screenBg, screenRect);
        g.DrawRectangle(bezelPen, screenRect);

        if (withForbiddenOverlay)
        {
            // Requirement 1: Forbidden overlay (clean vector with transparency)
            // Centered overlay with diameter 17
            float cx = 13.5f;
            float cy = 2.5f;
            float diam = 16f;

            // Subtle dark contrast halo so the red ring pops cleanly on white or dark taskbars
            using var haloPen = new Pen(System.Drawing.Color.FromArgb(160, 0, 0, 0), 3.5f);
            g.DrawEllipse(haloPen, cx, cy, diam, diam);
            g.DrawLine(haloPen, cx + 3.5f, cy + 3.5f, cx + diam - 3.5f, cy + diam - 3.5f);

            // Red ring (vivid crimson vector)
            using var redPen = new Pen(System.Drawing.Color.FromArgb(239, 68, 68), 2.2f);
            g.DrawEllipse(redPen, cx, cy, diam, diam);

            // Red diagonal line (top-left 135 deg to bottom-right 315 deg)
            g.DrawLine(redPen, cx + 3.5f, cy + 3.5f, cx + diam - 3.5f, cy + diam - 3.5f);
        }
        else
        {
            // When toggled off: subtle green or neutral status tick on screen
            using var powerBrush = new SolidBrush(System.Drawing.Color.FromArgb(148, 163, 184));
            g.FillEllipse(powerBrush, 14, 16, 3, 3);
        }

        IntPtr hIcon = bmp.GetHicon();
        using var tempIcon = Icon.FromHandle(hIcon);
        var icon = (Icon)tempIcon.Clone();

        // Release unmanaged GDI icon handle to prevent leaks
        Win32.DestroyIcon(hIcon);

        return icon;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _isDisposed = true;
        }
    }
}
