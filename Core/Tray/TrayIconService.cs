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
        // 32x32 crisp system tray icon
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Monitor Stand
        using var standBrush = new SolidBrush(System.Drawing.Color.FromArgb(160, 168, 180));
        g.FillRectangle(standBrush, 14, 21, 4, 4);
        g.FillRectangle(standBrush, 9, 25, 14, 2);

        // Monitor Outer Bezel
        using var bezelBrush = new SolidBrush(System.Drawing.Color.FromArgb(38, 42, 50));
        using var bezelPen = new Pen(System.Drawing.Color.FromArgb(200, 205, 215), 1.5f);
        g.FillRectangle(bezelBrush, 3, 3, 26, 17);
        g.DrawRectangle(bezelPen, 3, 3, 26, 17);

        // Inner Screen
        using var screenBrush = new SolidBrush(System.Drawing.Color.FromArgb(18, 22, 28));
        g.FillRectangle(screenBrush, 5, 5, 22, 13);

        if (withForbiddenOverlay)
        {
            // Requirement 1: Red circle with DIAGONAL line (Prohibition sign 🚫)
            int circleX = 11;
            int circleY = 4;
            int size = 16;

            // Semi-translucent white backplate for high contrast on any taskbar color
            using var plateBrush = new SolidBrush(System.Drawing.Color.FromArgb(235, 255, 255, 255));
            g.FillEllipse(plateBrush, circleX, circleY, size, size);

            // Red circle ring
            using var redPen = new Pen(System.Drawing.Color.FromArgb(225, 29, 72), 2.5f);
            g.DrawEllipse(redPen, circleX + 1, circleY + 1, size - 2, size - 2);

            // Red diagonal line from top-left (135 deg) to bottom-right (315 deg)
            g.DrawLine(redPen, circleX + 4, circleY + 4, circleX + size - 4, circleY + size - 4);
        }
        else
        {
            // When toggled off, forbidden overlay is completely removed; subtle paused indicator
            using var pausedBrush = new SolidBrush(System.Drawing.Color.FromArgb(90, 105, 125));
            g.FillRectangle(pausedBrush, 9, 9, 14, 5);
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
