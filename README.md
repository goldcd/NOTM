# NOTM (Not On This Monitor) 🛡️

[![.NET Version](https://img.shields.io/badge/.NET-8.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6?logo=windows)](https://microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**NOTM** is a lightweight, background Windows system tray utility that restricts which monitors can be used for applications—without disconnecting or disabling them in Windows.

---

### The Problem It Solves

When connecting a soundbar, AV receiver, or TV via HDMI (such as a **Panasonic SoundSlayer**, Dolby Atmos sound system, or capture card), Windows treats the HDMI audio connection as an active monitor. 

If you disable or disconnect the monitor in Windows Display Settings, **the audio stops working**. But if you leave it enabled:
- Windows applications and dialog boxes randomly open on the soundbar display.
- Maximized apps or windows get dragged onto the off-limits screen and disappear.
- Mouse cursors slide off into nowhere.

**NOTM solves this completely:** It keeps your soundbar or secondary display active for audio, but strictly prevents any window from residing on it. The moment an application opens on or is moved to an off-limits display, NOTM automatically bounces it back to your primary screen.

---

## Features

- 🛡️ **Instant Window Guard**: Listens to native Win32 window events (`EVENT_SYSTEM_MOVESIZEEND`, `EVENT_OBJECT_SHOW`, `EVENT_SYSTEM_FOREGROUND`) with zero CPU overhead. If any window lands on an off-limits monitor, it is relocated to your primary screen immediately upon mouse release or launch.
- 🕒 **Safeguard Sweep Engine**: A background safeguard periodically checks all visible top-level windows to catch stubborn or background apps that quietly restored their positions off-screen.
- 👁️ **Visual Monitor Identification**: Click **Identify Displays** to flash large, translucent, click-through numbered cards (`[ 1 ]`, `[ 2 ]`) on all screens—displaying friendly hardware names (e.g., `AW3423DWF`, `SOUNDSLAYER`) and resolutions.
- 🗺️ **Interactive Topology Map**: A mini scaled visual preview of your monitors arranged according to their real physical coordinates and aspect ratios. Click on any screen in the preview to toggle its blocked/allowed status.
- 📌 **System Tray Integration**: Lives silently in your Windows notification area. Closing or minimizing the window keeps NOTM running in the tray. Double-click the tray icon to open, or right-click for quick actions.
- 📋 **Live Activity Feed**: Tracks window redirects in real-time, displaying process names, window titles, timestamps, and source/target displays.
- 🚀 **Auto-Start with Windows**: Toggleable right from the UI, registering clean startup entries in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- 💾 **Persistent Settings**: Remembers your designated primary and blocked monitors across reboots, matched by hardware ID so changes persist even if display cables are replugged.

---

## Getting Started

### Prerequisites

- Windows 10 (version 1809+) or Windows 11 (64-bit).
- [.NET 8.0 SDK / Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (if building from source).

---

### Quick Start (Running from Source)

1. Clone the repository:
   ```powershell
   git clone https://github.com/goldcd/NOTM.git
   cd NOTM
   ```
2. Run the application:
   - Double-click **`run.cmd`**, or run:
     ```powershell
     dotnet run
     ```

---

### Building a Standalone Single-File `.exe`

To produce a single, portable executable that can be moved anywhere and requires **no external runtimes or installation**:

1. Double-click **`publish.cmd`**, or run:
   ```powershell
   powershell -ExecutionPolicy Bypass -File publish.ps1
   ```
2. Find your single-file executable in the `dist/` directory:
   ```
   dist/NOTM.exe
   ```
3. Run `NOTM.exe`—it starts immediately and sits in your system tray!

---

## How to Use

1. **Launch NOTM**: The main dashboard displays all connected monitors with their hardware names (e.g. `AW3423DWF`, `SOUNDSLAYER`) and resolutions.
2. **Mark Your Primary**: Click **Set Primary** on your main display (e.g., your Dell Alienware monitor). All blocked applications will be redirected here.
3. **Block Unused Monitors**: Click **Block Display** on your soundbar, dummy plug, or any display you do not want windows to appear on.
   - The status badge will change to <span style="color:red">**OFF-LIMITS (BLOCKED)**</span>.
   - Any windows currently resting on that monitor will be swept to the primary monitor immediately.
4. **Minimize to Tray**: Click the window close button (`✕`). NOTM will minimize to the system tray, continuing to guard your displays silently in the background.

---

## Architecture & Code Structure

NOTM is built with modern C# (.NET 8 WPF) following clean architecture principles and native Win32 interop:

```
NOTM/
├── Core/
│   ├── Native/
│   │   └── Win32.cs               # Win32 P/Invoke signatures (SetWinEventHook, EnumDisplayMonitors, DWM)
│   ├── Display/
│   │   ├── DisplayInfo.cs         # Model representing monitor geometry, status, and friendly names
│   │   └── DisplayService.cs      # Queries monitors via EnumDisplayMonitors + WMI (WmiMonitorID EDID)
│   ├── Engine/
│   │   └── WindowRelocationEngine.cs # WinEvent hooks and smart repositioning logic
│   ├── Config/
│   │   ├── AppSettings.cs         # Settings data contract
│   │   └── ConfigService.cs       # JSON serialization to %LocalAppData%\NOTM\settings.json
│   ├── Startup/
│   │   └── StartupService.cs      # Windows registry Run key manager
│   └── Tray/
│       └── TrayIconService.cs     # Windows notification area icon & context menu
├── UI/
│   ├── MainWindow.xaml / .cs      # Fluent dark UI dashboard
│   ├── MainWindowViewModel.cs     # MVVM ViewModel coordinating UI, engine, and tray
│   ├── MonitorVisualMap.xaml / .cs # Custom visual monitor topology preview
│   ├── IdentifyOverlayWindow.xaml / .cs # Full-screen translucent click-through identification cards
│   └── Converters.cs              # WPF Value Converters for buttons and status indicators
├── Assets/
│   └── app.ico                    # High-res application icon
├── NOTM.sln                       # Visual Studio Solution
├── NOTM.csproj                    # Project configuration
├── run.cmd                        # One-click launcher
└── publish.cmd                    # One-click standalone build script
```

### Why Native C# WPF?
- **Near-Zero Resource Usage**: Consumes ~25 MB RAM and 0% CPU at idle, compared to 150MB+ for Electron-based utilities.
- **Deep Windows API Integration**: Uses `SetWinEventHook` with out-of-context callbacks for instant drop detection, `DwmGetWindowAttribute` for pixel-perfect window frame calculations (accounting for drop shadows), and WMI `WmiMonitorID` for EDID-level display identification.
- **Click-Through Overlays**: Identification badges use `WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW` so you can continue typing and clicking while monitors are identified.

---

## License

This project is licensed under the [MIT License](LICENSE).
