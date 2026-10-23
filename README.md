# NOTM (Not on That Monitor)

A lightweight Windows system tray utility that keeps secondary HDMI audio devices (such as soundbars or AV receivers) active for sound while strictly preventing any applications from appearing on them.

---

## 1. What It Does and Why

### The Problem
When you connect an HDMI soundbar (such as a Panasonic SoundSlayer), AV receiver, or HDMI audio extractor to a Windows PC, Windows treats the HDMI connection as a display monitor.

- If you **disable or disconnect** the monitor in Windows Display Settings, **HDMI audio stops working completely**.
- If you **leave it enabled**, Windows treats it as active desktop space: application windows, installer dialogs, and popups randomly open on the soundbar screen, windows get lost off-screen when dragged or moved via `Win + Shift + Arrow`, and mouse cursors slide off into nowhere.

### The Solution
**NOTM** keeps your HDMI soundbar enabled in Windows so audio continues playing without interruption, but actively guards the display:
- **Instant Redirection**: If any application is dragged, launched, or moved to a blocked display (e.g. via `Win + Shift + Arrow`), NOTM immediately bounces it back to your primary display.
- **Window Size & Position Memory**: When an app is pulled back from a blocked display, its previous dimensions, coordinates, and maximized state on your primary monitor are automatically restored.
- **Self-Protected**: NOTM protects its own window so it can never be trapped on a blocked display.
- **Audio Keep-Alive**: Prevents HDMI soundbars and AV receivers from falling into power-saving sleep or clipping the start of sounds when playback resumes.

---

## 2. How to Use It

### Quick Start
1. **Run or Install**:
   - **Standalone**: Launch `dist\NOTM.exe` directly, or
   - **Install to System**: Double-click `install.cmd` (installs to `%LocalAppData%\Programs\NOTM` and creates a Start Menu shortcut; no admin privileges required).
2. **Configure Displays**:
   - Open NOTM (it opens on start, or double-click its system tray icon).
   - The UI shows your monitors arranged to match their physical positions in Windows Display Settings.
   - Click **Identify Displays** to display large numbered labels on your screens.
   - Select your soundbar / secondary display and click **Block Display (Not in Use)**.
   - Ensure your main monitor is marked as **Primary**.
3. **Tray Controls**:
   - Closing the window minimizes NOTM to the system tray.
   - Clicking the tray icon gives two options:
     - **Open NOTM**: Opens the settings window.
     - **Guard: Active / Paused**: Instantly toggles window protection on or off.

To uninstall completely, double-click `uninstall.cmd` or uninstall from **Windows Settings > Apps > Installed Apps**.

---

## 3. Technical Details

For anyone interested in the implementation details:

### Architecture & Stack
- **Target Runtime**: .NET 8 LTS (`net8.0-windows`), Win-x64 standalone single-file binary.
- **UI Framework**: WPF (Windows Presentation Foundation) styled to match Windows 11 Fluent dark mode.
- **Resource Footprint**: ~25 MB RAM, 0% CPU at idle.

### Win32 Relocation Engine
- **Event Hooks**: Uses native `SetWinEventHook` to listen out-of-context for:
  - `EVENT_OBJECT_LOCATIONCHANGE`: Catches keyboard-driven moves (such as `Win + Shift + Arrow`) immediately without polling.
  - `EVENT_SYSTEM_MOVESIZEEND`: Catches mouse drag-and-drop operations the moment a window is released.
  - `EVENT_OBJECT_SHOW`: Catches newly created windows launching directly onto a blocked screen.
  - `EVENT_SYSTEM_FOREGROUND`: Ensures newly focused windows are on allowed screens.
- **Fallback Sweep**: A lightweight background timer periodically checks all top-level windows to catch any unhooked background processes.
- **Hardware Plug/Unplug Detection**: Hooks `WM_DISPLAYCHANGE` window messages. When a soundbar powers on/off, wakes from sleep, or a cable is reconnected, NOTM automatically re-enumerates displays and updates its layout without needing a restart.
- **Display Friendly Names**: Reads monitor model names (e.g., `AW3423DWF`, `SOUNDSLAYER`) directly via WMI (`WmiMonitorID` EDID parsing) rather than generic `\\.\DISPLAY#` strings.
- **Display Protection Guard**: Uses `Win32.MonitorFromWindow` to prevent designating an off-limits or invisible screen as primary.
- **DWM Redraw Cleanliness**: Dispatches `Win32.RedrawWindow` with `RDW_FRAME | RDW_INVALIDATE | RDW_UPDATENOW` after repositioning to prevent DirectX / hardware composition tearing.

### Audio Keep-Alive (WASAPI Engine)
- **Problem Solved**: Digital soundbars (e.g. Panasonic SoundSlayer), AV receivers, and DACs often enter a power-saving sleep state when silent. When audio starts playing, the first 1-2 seconds get clipped or delayed while the HDMI audio clock re-syncs.
- **Implementation**: Uses native Windows Audio Session API (`IAudioClient`) in shared mode to stream inaudible keep-alive samples (`Fluctuate` mode) with ~0% CPU. Alternates polarity (`0x34000001` / `0xB4000001` for float, `+1` / `-1` for 16-bit PCM) once per second to maintain zero DC bias while keeping the HDMI transmitter awake.
- **Acknowledgements & Credit**: The audio keep-alive feature and `Fluctuate` stream implementation are directly inspired by and based on the work of **VEG** in [SoundKeeper](https://github.com/vrubleg/soundkeeper).

### Build & Utility Scripts
| Script | Description |
| :--- | :--- |
| `run.cmd` | Runs the app from source (`dotnet run`). |
| `build.cmd` | Builds the solution in Release mode (`dotnet build`). |
| `publish.cmd` | Builds the self-contained single-file executable to `dist\NOTM.exe`. |
| `install.cmd` | Builds and installs NOTM to user profile with Start Menu shortcut. |
| `uninstall.cmd` | Cleanly removes NOTM, shortcuts, and registry entries. |
| `installer.iss` | Inno Setup script for building a standard Windows setup wizard installer. |

### Repository Structure
```
NOTM/
├── Core/
│   ├── Audio/
│   │   ├── AudioKeepAliveService.cs  # WASAPI background keep-alive worker
│   │   └── Wasapi.cs                 # Native WASAPI COM interop signatures
│   ├── Native/
│   │   └── Win32.cs                  # Win32 P/Invoke APIs (SetWinEventHook, DWM, RedrawWindow)
│   ├── Display/
│   │   ├── DisplayInfo.cs            # Monitor geometry, DPI, and friendly name models
│   │   └── DisplayService.cs         # Display enumeration via EnumDisplayMonitors & WMI
│   ├── Engine/
│   │   └── WindowRelocationEngine.cs # WinEvent hook engine, window position cache & redirection
│   ├── Config/
│   │   ├── AppSettings.cs            # Settings schema
│   │   └── ConfigService.cs          # JSON settings in %LocalAppData%\NOTM\settings.json
│   ├── Startup/
│   │   └── StartupService.cs         # Windows Run registry key manager
│   └── Tray/
│       └── TrayIconService.cs        # System tray icon & context menu management
├── UI/
│   ├── MainWindow.xaml / .cs         # Main dashboard replicating Windows Display Settings
│   ├── MainWindowViewModel.cs        # MVVM ViewModel coordinating engine, state, and UI
│   ├── MonitorVisualMap.xaml / .cs   # Scaled physical monitor layout canvas
│   └── IdentifyOverlayWindow.xaml    # Click-through screen identification overlays
├── Assets/
│   ├── app.ico                       # Multi-resolution application icon
│   └── generate_icon.ps1             # Icon generator script
├── NOTM.sln                          # Visual Studio Solution
├── NOTM.csproj                       # Project configuration (.NET 8 Windows)
├── install.cmd / install.ps1         # User-level installer
├── uninstall.cmd / uninstall.ps1     # Complete uninstaller
├── run.cmd                           # Debug runner
├── build.cmd                         # Release builder
└── publish.cmd / publish.ps1         # Single-file publisher
```

## Acknowledgements
- [SoundKeeper by VEG (vrubleg)](https://github.com/vrubleg/soundkeeper) — for the WASAPI audio keep-alive concept and the `Fluctuate` stream design that keeps digital audio devices awake without audible sound.

## License
MIT License.
