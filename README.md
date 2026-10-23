# NOTM (Not on That Monitor) 🛡️

[![.NET Version](https://img.shields.io/badge/.NET-8.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6?logo=windows)](https://microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**NOTM** is a focused, ultra-lightweight Windows system tray utility designed to restrict which monitors can actually be used for applications—without disconnecting or detaching them in Windows.

---

### The Problem It Solves

When an HDMI audio device, soundbar (such as a **Panasonic SoundSlayer**), AV receiver, or dummy plug is connected to your Windows PC, Windows creates an active virtual/physical monitor for it.

If you disable or disconnect the monitor in Windows Display Settings, **audio stops working**. But if you leave it enabled:
- Applications, dialogues, and installer wizards randomly launch or wander off onto the soundbar screen.
- Maximized windows or dragged windows get lost off-screen.
- Mouse cursors slide off into nowhere.

**NOTM solves this with a single, clear purpose**: It leaves your soundbar active for audio, but strictly forbids any application window from residing on it. If an application is moved to or launched on an off-limits monitor, NOTM instantly redirects it back to your primary monitor.

---

## Key Features & Polish

### 1. Replicates Windows 11 "System > Display" Settings
- Interactive visual monitor arrangement showing your displays drawn to their **actual physical relative resolution and positions**.
- Numbered display blocks (`1`, `2`) mirroring Windows Display Settings.
- Click any display block to select it and configure its role:
  - **Primary Target Display**: All off-limits windows are redirected here.
  - **Block Display (Not in Use)**: Forbids application windows from staying on this screen.
- **Identify Displays Button**: Positioned directly under the visual canvas (just like Windows Settings) to flash large, translucent, click-through numbered cards (`[ 1 ]`, `[ 2 ]`) on your screens.

### 2. Custom System Tray & Application Icon
- **When Active**: Displays a crisp minimalist monitor glyph with a **bold vivid red diagonal ban stripe** cutting across the display.
- **When Toggled Off**: The diagonal ban is removed, leaving a clean minimalist monitor glyph.
- **Consistent Branding**: The exact same icon is used across the system tray, the Windows taskbar, the window title bar, and the in-app header.
- **Clicking the Tray Icon**: Left-clicking or right-clicking displays two primary actions:
  1. **Open NOTM** (launches the dashboard)
  2. **Guard: Active / Paused (Toggle On/Off)** (switches protection on or off instantly)
- Zero GDI memory leaks: Automatically releases native `HICON` handles.

### 3. Automatic Hardware Change Detection (`WM_DISPLAYCHANGE`)
- Listens to Windows OS display broadcast messages (`WM_DISPLAYCHANGE`).
- When your soundbar powers on/off, wakes from sleep, or display cables are reconnected, NOTM automatically refreshes and maintains your configuration without needing a restart.

### 4. Non-Intrusive Win32 Window Guard
- Listens to native Win32 window events (`EVENT_SYSTEM_MOVESIZEEND`, `EVENT_OBJECT_SHOW`, `EVENT_SYSTEM_FOREGROUND`).
- Zero CPU usage when idle (~25 MB RAM).
- Includes a low-overhead background safeguard sweep to catch stubborn background windows.

---

## Installation & Uninstallation

NOTM is designed to be trivial to compile, install, and uninstall without requiring third-party tools or administrative privileges.

### 1. One-Click Install
Double-click **`install.cmd`** (or run `powershell -ExecutionPolicy Bypass -File install.ps1`):
1. Compiles the standalone release executable `NOTM.exe`.
2. Installs to `%LocalAppData%\Programs\NOTM\`.
3. Creates a Start Menu shortcut ("NOTM").
4. Registers NOTM in **Windows Settings > Apps > Installed Apps** (Add/Remove Programs) with version, publisher, and icon.
5. Launches NOTM immediately.

### 2. One-Click Uninstall
- Go to **Windows Settings > Apps > Installed Apps > NOTM > Uninstall**, OR
- Double-click **`uninstall.cmd`**:
  - Closes any running NOTM processes.
  - Removes the Start Menu shortcut.
  - Removes the Windows Startup registry entry.
  - Removes the Windows Installed Apps entry.
  - Deletes `%LocalAppData%\Programs\NOTM\`.
  - Leaves 0 leftover files.

---

## Compilation & Development Scripts

| Script | Purpose |
| :--- | :--- |
| **`run.cmd`** | Runs NOTM in debug mode directly from source (`dotnet run`). |
| **`build.cmd`** | Compiles the Visual Studio solution (`NOTM.sln`) in Release mode and validates 0 errors. |
| **`publish.cmd`** | Compiles a standalone, single-file `dist\NOTM.exe` (no .NET runtime required to run). |
| **`install.cmd`** | Compiles and installs NOTM to your Windows system with Start Menu shortcut. |
| **`uninstall.cmd`** | Completely and cleanly removes NOTM from your Windows system. |
| **`installer.iss`** | Optional Inno Setup script to generate a classic setup wizard installer (`.exe`). |

---

## Project Structure

```
NOTM/
├── Core/
│   ├── Native/
│   │   └── Win32.cs               # Win32 P/Invoke signatures (SetWinEventHook, DWM, WM_DISPLAYCHANGE)
│   ├── Display/
│   │   ├── DisplayInfo.cs         # Model representing monitor geometry, status, and friendly names
│   │   └── DisplayService.cs      # Queries monitors via EnumDisplayMonitors + WMI (WmiMonitorID EDID)
│   ├── Engine/
│   │   └── WindowRelocationEngine.cs # WinEvent hooks and safe repositioning logic
│   ├── Config/
│   │   ├── AppSettings.cs         # Settings data contract
│   │   └── ConfigService.cs       # JSON serialization to %LocalAppData%\NOTM\settings.json
│   ├── Startup/
│   │   └── StartupService.cs      # Windows registry Run key manager
│   └── Tray/
│       └── TrayIconService.cs     # System tray icon with forbidden overlay & 2-option menu
├── UI/
│   ├── MainWindow.xaml / .cs      # Windows 11 Display Settings style dashboard
│   ├── MainWindowViewModel.cs     # MVVM ViewModel coordinating UI, engine, and tray
│   ├── MonitorVisualMap.xaml / .cs # Scaled physical topology preview with selection
│   ├── IdentifyOverlayWindow.xaml / .cs # Click-through screen identification cards
│   └── Converters.cs              # WPF Value Converters
├── Assets/
│   └── app.ico                    # High-res application icon
├── NOTM.sln                       # Visual Studio Solution
├── NOTM.csproj                    # Project configuration
├── install.cmd / install.ps1      # Automated per-user installer
├── uninstall.cmd / uninstall.ps1  # Automated uninstaller
├── run.cmd                        # One-click debug launcher
├── build.cmd                      # One-click solution compiler
├── publish.cmd / publish.ps1      # Single-file standalone publisher
└── installer.iss                  # Optional Inno Setup wizard compiler
```

---

## License

This project is licensed under the [MIT License](LICENSE).
