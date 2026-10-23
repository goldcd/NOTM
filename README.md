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

### ⚡ Getting Started

#### Option A: Download Standalone Executable (Recommended)
1. Go to **[Releases](https://github.com/goldcd/NOTM/releases/latest)**.
2. Download **`NOTM.exe`**.
3. Run it from anywhere — no installation or external runtimes required.

#### Option B: One-Click Installer (Local)
1. Download or clone this repository.
2. Double-click **`install.cmd`** (no administrator rights needed).
   - Installs cleanly to `%LocalAppData%\Programs\NOTM`
   - Creates a Start Menu shortcut
   - Adds NOTM to Windows Settings > Installed Apps

#### Option C: Build from Source
```cmd
git clone https://github.com/goldcd/NOTM.git
cd NOTM
publish.cmd
```
*`publish.cmd` builds the standalone, self-contained single-file executable to `dist\NOTM.exe` (note: `dist/` is `.gitignore`d to prevent large binary bloat in the git repository).*

---

### How to Use NOTM
1. **Configure Displays**:
   - Open NOTM (it opens on start, or left-click its system tray icon by the clock).
   - The UI shows your displays arranged according to their Windows Display Settings positions.
   - Click **Identify Displays** to flash large labels onto all screens.
   - Select your soundbar / secondary display and click **Block Display (Not in Use)**.
   - Ensure your main monitor is marked as **Primary Target**.
2. **Audio Keep-Alive**:
   - For any display with an associated HDMI/DP audio endpoint (such as the SoundSlayer), the **Keep Audio Alive** card is automatically available and enabled.
3. **Tray Controls (by the Clock)**:
   - **Left-Click**: Instantly opens / restores the NOTM GUI.
   - **Right-Click**: Opens the quick menu (*Open NOTM*, *Guard: Active/Paused toggle*, *Identify Displays*, *Reset Target*, and *Exit*).

To uninstall completely, run `uninstall.cmd` or uninstall from **Windows Settings > Apps > Installed Apps**.


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
- **Per-Device Targeting**: Rather than keeping all audio devices awake or waking headphones when you don't want them, NOTM correlates connected HDMI/DisplayPort audio endpoints with their specific physical displays. If a monitor has an associated soundbar or audio output, a dedicated **Keep Audio Alive** option appears directly on that display card (default: ON). Displays without audio outputs keep this option cleanly hidden.
- **Power-State Aware**: When you turn off your soundbar, NOTM stops streaming to it and waits for it without ever keeping alive whatever other audio output you might be using (such as headphones or desktop speakers). When the soundbar is powered back on, NOTM resumes keeping it awake automatically.
- **Implementation**: Uses native Windows Audio Session API (`IAudioClient`) in shared mode targeted specifically to the endpoint to stream inaudible keep-alive samples (`Fluctuate` mode) with ~0% CPU. Alternates polarity (`0x34000001` / `0xB4000001` for float, `+1` / `-1` for 16-bit PCM) once per second to maintain zero DC bias while keeping the HDMI transmitter awake.
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
