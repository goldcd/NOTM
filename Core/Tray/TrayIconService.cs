using System;
using System.Drawing;
using System.Windows.Forms;
using NOTM.Models;

namespace NOTM.Core.Tray;

public class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _guardToggleItem;
    private bool _isDisposed;

    public event Action? OnOpenRequested;
    public event Action<bool>? OnGuardToggled;
    public event Action? OnIdentifyRequested;
    public event Action? OnSweepRequested;
    public event Action? OnExitRequested;

    public TrayIconService()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "NOTM - Monitor Guard",
            Visible = true,
            Icon = CreateAppIcon(true)
        };

        var contextMenu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open NOTM", null, (s, e) => OnOpenRequested?.Invoke())
        {
            Font = new Font(contextMenu.Font ?? SystemFonts.DefaultFont, FontStyle.Bold)
        };

        _guardToggleItem = new ToolStripMenuItem("Guard Active", null, (s, e) =>
        {
            if (s is ToolStripMenuItem item)
            {
                item.Checked = !item.Checked;
                UpdateIconState(item.Checked);
                OnGuardToggled?.Invoke(item.Checked);
            }
        })
        {
            Checked = true
        };

        var identifyItem = new ToolStripMenuItem("Identify Monitors", null, (s, e) => OnIdentifyRequested?.Invoke());
        var sweepItem = new ToolStripMenuItem("Relocate Off-Limits Windows Now", null, (s, e) => OnSweepRequested?.Invoke());
        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => OnExitRequested?.Invoke());

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_guardToggleItem);
        contextMenu.Items.Add(identifyItem);
        contextMenu.Items.Add(sweepItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => OnOpenRequested?.Invoke();
    }

    public void SetGuardState(bool isEnabled)
    {
        _guardToggleItem.Checked = isEnabled;
        UpdateIconState(isEnabled);
        _notifyIcon.Text = isEnabled ? "NOTM - Monitor Guard (Active)" : "NOTM - Monitor Guard (Paused)";
    }

    private void UpdateIconState(bool active)
    {
        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = CreateAppIcon(active);
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

    private static Icon CreateAppIcon(bool active)
    {
        // Dynamically render a sharp, modern monitor icon
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Monitor Outer Frame
        using var borderPen = new Pen(System.Drawing.Color.FromArgb(220, 220, 230), 2f);
        using var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(30, 32, 38));
        g.FillRectangle(bgBrush, 3, 4, 26, 17);
        g.DrawRectangle(borderPen, 3, 4, 26, 17);

        // Monitor Stand
        using var standBrush = new SolidBrush(System.Drawing.Color.FromArgb(160, 165, 175));
        g.FillRectangle(standBrush, 14, 22, 4, 4);
        g.FillRectangle(standBrush, 9, 26, 14, 2);

        // Inner Screen Glow / Indicator
        var dotColor = active ? System.Drawing.Color.FromArgb(16, 185, 129) : System.Drawing.Color.FromArgb(239, 68, 68);
        using var dotBrush = new SolidBrush(dotColor);
        g.FillEllipse(dotBrush, 13, 9, 6, 6);

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _isDisposed = true;
        }
    }
}
