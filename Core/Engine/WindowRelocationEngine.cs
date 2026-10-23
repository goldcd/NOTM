using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using NOTM.Core.Display;
using NOTM.Core.Native;
using NOTM.Models;

namespace NOTM.Core.Engine;

public class WindowRelocationEngine : IDisposable
{
    private readonly uint _currentProcessId;
    private readonly ConcurrentDictionary<IntPtr, DateTime> _recentlyRelocated = new();

    private readonly List<IntPtr> _hookHandles = new();
    private Win32.WinEventDelegate? _winEventDelegate;
    private System.Timers.Timer? _sweepTimer;

    private readonly object _lock = new();
    private List<DisplayInfo> _monitors = new();
    private bool _isGuardEnabled = true;
    private int _sweepIntervalMs = 1000;

    public event Action<RelocationLogItem>? OnWindowRelocated;

    public WindowRelocationEngine()
    {
        _currentProcessId = (uint)Process.GetCurrentProcess().Id;
    }

    public void Start()
    {
        lock (_lock)
        {
            Stop();

            _winEventDelegate = HookCallback;

            // Hook window drag/resize completion (drop event)
            IntPtr hookMoveEnd = Win32.SetWinEventHook(
                Win32.EVENT_SYSTEM_MOVESIZEEND,
                Win32.EVENT_SYSTEM_MOVESIZEEND,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);

            // Hook window foreground change
            IntPtr hookForeground = Win32.SetWinEventHook(
                Win32.EVENT_SYSTEM_FOREGROUND,
                Win32.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);

            // Hook new window shown
            IntPtr hookShow = Win32.SetWinEventHook(
                Win32.EVENT_OBJECT_SHOW,
                Win32.EVENT_OBJECT_SHOW,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);

            if (hookMoveEnd != IntPtr.Zero) _hookHandles.Add(hookMoveEnd);
            if (hookForeground != IntPtr.Zero) _hookHandles.Add(hookForeground);
            if (hookShow != IntPtr.Zero) _hookHandles.Add(hookShow);

            // Start safeguard sweep timer
            if (_sweepIntervalMs > 0)
            {
                _sweepTimer = new System.Timers.Timer(_sweepIntervalMs);
                _sweepTimer.Elapsed += (s, e) => SweepAllWindows();
                _sweepTimer.AutoReset = true;
                _sweepTimer.Start();
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_sweepTimer != null)
            {
                _sweepTimer.Stop();
                _sweepTimer.Dispose();
                _sweepTimer = null;
            }

            foreach (var hook in _hookHandles)
            {
                if (hook != IntPtr.Zero)
                {
                    Win32.UnhookWinEvent(hook);
                }
            }
            _hookHandles.Clear();
            _winEventDelegate = null;
        }
    }

    public void UpdateConfiguration(List<DisplayInfo> monitors, bool isGuardEnabled, int sweepIntervalMs)
    {
        lock (_lock)
        {
            _monitors = monitors.ToList();
            _isGuardEnabled = isGuardEnabled;

            if (_sweepIntervalMs != sweepIntervalMs)
            {
                _sweepIntervalMs = sweepIntervalMs;
                if (_sweepTimer != null)
                {
                    _sweepTimer.Interval = Math.Max(250, _sweepIntervalMs);
                }
            }
        }
    }

    public void SweepAllWindows()
    {
        if (!_isGuardEnabled) return;

        Win32.EnumWindows((hWnd, lParam) =>
        {
            CheckAndRelocateWindow(hWnd);
            return true;
        }, IntPtr.Zero);
    }

    private void HookCallback(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (!_isGuardEnabled || hwnd == IntPtr.Zero) return;

        // idObject: 0 = OBJID_WINDOW
        if (idObject != 0) return;

        CheckAndRelocateWindow(hwnd);
    }

    public bool CheckAndRelocateWindow(IntPtr hWnd)
    {
        if (!_isGuardEnabled || hWnd == IntPtr.Zero) return false;

        // Debounce: don't process the same window repeatedly within 500ms
        if (_recentlyRelocated.TryGetValue(hWnd, out var lastTime))
        {
            if ((DateTime.UtcNow - lastTime).TotalMilliseconds < 500)
            {
                return false;
            }
        }

        // Fast validation: must be a valid, visible, non-minimized window
        if (!Win32.IsWindow(hWnd) || !Win32.IsWindowVisible(hWnd) || Win32.IsIconic(hWnd))
        {
            return false;
        }

        // Ignore our own process
        Win32.GetWindowThreadProcessId(hWnd, out uint processId);
        if (processId == _currentProcessId || processId == 0)
        {
            return false;
        }

        // Ignore desktop and shell windows
        if (hWnd == Win32.GetDesktopWindow() || hWnd == Win32.GetShellWindow())
        {
            return false;
        }

        // Check window style
        long style = (long)Win32.GetWindowLongPtr(hWnd, Win32.GWL_STYLE);
        long exStyle = (long)Win32.GetWindowLongPtr(hWnd, Win32.GWL_EXSTYLE);

        // Ignore child windows
        if ((style & Win32.WS_CHILD) != 0)
        {
            return false;
        }

        // Ignore pure toolwindows (floating toolbars, tooltips)
        if ((exStyle & Win32.WS_EX_TOOLWINDOW) != 0 && (style & Win32.WS_POPUP) != 0)
        {
            return false;
        }

        // Check class name for known shell elements to avoid moving
        var classNameBuilder = new StringBuilder(256);
        Win32.GetClassName(hWnd, classNameBuilder, 256);
        string className = classNameBuilder.ToString();

        if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "WorkerW" or "Progman" or
            "XamlExplorerHostIslandWindow" or "Windows.UI.Core.CoreWindow" or "DV2ControlHost")
        {
            return false;
        }

        // Get window actual rectangle
        if (!Win32.GetWindowActualRect(hWnd, out Win32.RECT rect) || rect.Width <= 10 || rect.Height <= 10)
        {
            return false;
        }

        int centerX = rect.Left + rect.Width / 2;
        int centerY = rect.Top + rect.Height / 2;

        List<DisplayInfo> activeMonitors;
        lock (_lock)
        {
            activeMonitors = _monitors.ToList();
        }

        if (activeMonitors.Count == 0) return false;

        var excludedMonitors = activeMonitors.Where(m => m.IsExcluded).ToList();
        if (excludedMonitors.Count == 0) return false;

        var targetPrimary = activeMonitors.FirstOrDefault(m => m.IsTargetPrimary)
                            ?? activeMonitors.FirstOrDefault(m => m.IsOsPrimary)
                            ?? activeMonitors.FirstOrDefault(m => !m.IsExcluded);

        if (targetPrimary == null) return false;

        // Check which monitor the window is currently on
        IntPtr hCurrentMonitor = Win32.MonitorFromWindow(hWnd, Win32.MONITOR_DEFAULTTONEAREST);

        DisplayInfo? sourceExcludedMonitor = null;

        // 1. Check if the nearest HMonitor is in our excluded list
        sourceExcludedMonitor = excludedMonitors.FirstOrDefault(m => m.HMonitor == hCurrentMonitor);

        // 2. Check if center of window is inside an excluded monitor
        if (sourceExcludedMonitor == null)
        {
            sourceExcludedMonitor = excludedMonitors.FirstOrDefault(m => m.ContainsPoint(centerX, centerY));
        }

        // 3. Check if significant overlap (>35% area) with an excluded monitor
        if (sourceExcludedMonitor == null)
        {
            double windowArea = rect.Width * rect.Height;
            foreach (var exc in excludedMonitors)
            {
                double overlap = exc.OverlapArea(rect.Left, rect.Top, rect.Right, rect.Bottom);
                if (overlap > 0 && (overlap / windowArea) > 0.35)
                {
                    sourceExcludedMonitor = exc;
                    break;
                }
            }
        }

        if (sourceExcludedMonitor == null)
        {
            return false; // Window is not on an excluded monitor
        }

        // RELOCATE to Target Primary!
        _recentlyRelocated[hWnd] = DateTime.UtcNow;

        // Clean old cache entries
        if (_recentlyRelocated.Count > 100)
        {
            var expired = _recentlyRelocated.Where(kvp => (DateTime.UtcNow - kvp.Value).TotalSeconds > 10).Select(k => k.Key).ToList();
            foreach (var key in expired) _recentlyRelocated.TryRemove(key, out _);
        }

        // Get window title and process name for logging
        string windowTitle = GetWindowTitle(hWnd);
        string processName = GetProcessName(processId);

        bool isZoomed = Win32.IsZoomed(hWnd);

        // Calculate target dimensions
        int targetWidth = Math.Min(rect.Width, targetPrimary.WorkWidth);
        int targetHeight = Math.Min(rect.Height, targetPrimary.WorkHeight);

        // Center on target primary working area
        int targetX = targetPrimary.WorkLeft + Math.Max(0, (targetPrimary.WorkWidth - targetWidth) / 2);
        int targetY = targetPrimary.WorkTop + Math.Max(0, (targetPrimary.WorkHeight - targetHeight) / 2);

        if (isZoomed)
        {
            // Restore, move into target monitor, then re-maximize
            Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
            Win32.SetWindowPos(hWnd, IntPtr.Zero, targetX, targetY, targetWidth, targetHeight,
                Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
            Win32.ShowWindow(hWnd, Win32.SW_MAXIMIZE);
        }
        else
        {
            Win32.SetWindowPos(
                hWnd,
                IntPtr.Zero,
                targetX,
                targetY,
                targetWidth,
                targetHeight,
                Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED | Win32.SWP_SHOWWINDOW);
        }

        var logItem = new RelocationLogItem
        {
            Timestamp = DateTime.Now,
            ProcessName = processName,
            WindowTitle = windowTitle,
            SourceMonitor = sourceExcludedMonitor.DisplayTitle,
            TargetMonitor = targetPrimary.DisplayTitle
        };

        OnWindowRelocated?.Invoke(logItem);
        return true;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = Win32.GetWindowTextLength(hWnd);
        if (length == 0) return "Untitled";
        var sb = new StringBuilder(length + 1);
        Win32.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            using var p = Process.GetProcessById((int)processId);
            return p.ProcessName;
        }
        catch
        {
            return "Application";
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
