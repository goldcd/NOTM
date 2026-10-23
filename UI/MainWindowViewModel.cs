using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using NOTM.Core.Config;
using NOTM.Core.Display;
using NOTM.Core.Engine;
using NOTM.Core.Startup;
using NOTM.Core.Tray;
using NOTM.Models;

namespace NOTM.UI;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly WindowRelocationEngine _engine;
    private readonly TrayIconService _trayService;
    private AppSettings _settings;

    private bool _isGuardActive = true;
    private int _relocatedCount = 0;
    private string _statusText = "GUARD ACTIVE";
    private Brush _statusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));

    public ObservableCollection<DisplayInfo> Displays { get; } = new();
    public ObservableCollection<RelocationLogItem> RelocationLog { get; } = new();

    public bool IsGuardActive
    {
        get => _isGuardActive;
        set
        {
            if (_isGuardActive != value)
            {
                _isGuardActive = value;
                _settings.IsGuardEnabled = value;
                UpdateGuardStatusVisuals();
                _trayService.SetGuardState(value);
                SyncConfiguration();
                SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public int RelocatedCount
    {
        get => _relocatedCount;
        set
        {
            _relocatedCount = value;
            OnPropertyChanged();
        }
    }

    private DisplayInfo? _selectedDisplay;

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (_selectedDisplay != value)
            {
                _selectedDisplay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSelectedPrimary));
                OnPropertyChanged(nameof(IsSelectedExcluded));
            }
        }
    }

    public bool IsSelectedPrimary => SelectedDisplay?.IsTargetPrimary ?? false;
    public bool IsSelectedExcluded => SelectedDisplay?.IsExcluded ?? false;

    public int BlockedCount => Displays.Count(d => d.IsExcluded);
    public int AllowedCount => Displays.Count(d => !d.IsExcluded);

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set
        {
            _statusBrush = value;
            OnPropertyChanged();
        }
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows != value)
            {
                _settings.StartWithWindows = value;
                StartupService.SetStartup(value);
                SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public bool StartMinimized
    {
        get => _settings.StartMinimized;
        set
        {
            if (_settings.StartMinimized != value)
            {
                _settings.StartMinimized = value;
                SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public bool ShowNotifications
    {
        get => _settings.ShowNotificationOnRelocate;
        set
        {
            if (_settings.ShowNotificationOnRelocate != value)
            {
                _settings.ShowNotificationOnRelocate = value;
                SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public MainWindowViewModel(WindowRelocationEngine engine, TrayIconService trayService)
    {
        _engine = engine;
        _trayService = trayService;
        _settings = ConfigService.Load();

        _isGuardActive = _settings.IsGuardEnabled;
        UpdateGuardStatusVisuals();

        _engine.OnWindowRelocated += HandleWindowRelocated;
        _trayService.OnGuardToggled += (enabled) => IsGuardActive = enabled;
        _trayService.OnIdentifyRequested += () => IdentifyAllDisplays();

        LoadDisplays();
    }

    public void LoadDisplays()
    {
        Displays.Clear();
        var monitors = DisplayService.GetConnectedMonitors();

        foreach (var m in monitors)
        {
            // Restore saved settings
            if (_settings.ExcludedIdentifiers.Contains(m.HardwareId) ||
                _settings.ExcludedIdentifiers.Contains(m.DeviceName) ||
                _settings.ExcludedIdentifiers.Contains(m.FriendlyName))
            {
                m.IsExcluded = true;
            }

            if (!string.IsNullOrEmpty(_settings.TargetPrimaryHardwareId) &&
                (m.HardwareId == _settings.TargetPrimaryHardwareId || m.DeviceName == _settings.TargetPrimaryDeviceName))
            {
                m.IsTargetPrimary = true;
            }

            m.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(DisplayInfo.IsExcluded) or nameof(DisplayInfo.IsTargetPrimary))
                {
                    OnDisplaySettingChanged(m, e.PropertyName);
                }
            };

            Displays.Add(m);
        }

        // If no target primary is set or target primary is excluded, pick OS primary or first non-excluded
        EnsureValidTargetPrimary();

        // Default selection to target primary or first display
        SelectedDisplay = Displays.FirstOrDefault(d => d.IsTargetPrimary) ?? Displays.FirstOrDefault();

        SyncConfiguration();
        OnPropertyChanged(nameof(BlockedCount));
        OnPropertyChanged(nameof(AllowedCount));
    }

    public void SetSelectedAsPrimary()
    {
        if (SelectedDisplay != null)
        {
            SetTargetPrimary(SelectedDisplay);
            OnPropertyChanged(nameof(IsSelectedPrimary));
            OnPropertyChanged(nameof(IsSelectedExcluded));
        }
    }

    public void ToggleSelectedExcluded()
    {
        if (SelectedDisplay != null)
        {
            ToggleExcluded(SelectedDisplay);
            OnPropertyChanged(nameof(IsSelectedPrimary));
            OnPropertyChanged(nameof(IsSelectedExcluded));
        }
    }

    public void IdentifySelected()
    {
        if (SelectedDisplay != null)
        {
            IdentifySingleDisplay(SelectedDisplay);
        }
    }

    private void EnsureValidTargetPrimary()
    {
        var target = Displays.FirstOrDefault(d => d.IsTargetPrimary && !d.IsExcluded);
        if (target == null)
        {
            // Pick OS primary if not excluded
            target = Displays.FirstOrDefault(d => d.IsOsPrimary && !d.IsExcluded)
                     ?? Displays.FirstOrDefault(d => !d.IsExcluded);

            if (target != null)
            {
                foreach (var d in Displays) d.IsTargetPrimary = (d == target);
            }
        }
    }

    public void SetTargetPrimary(DisplayInfo selectedDisplay)
    {
        if (selectedDisplay.IsExcluded)
        {
            selectedDisplay.IsExcluded = false; // Cannot be primary and excluded
        }

        foreach (var d in Displays)
        {
            d.IsTargetPrimary = (d == selectedDisplay);
        }

        _settings.TargetPrimaryHardwareId = selectedDisplay.HardwareId;
        _settings.TargetPrimaryDeviceName = selectedDisplay.DeviceName;

        SyncConfiguration();
        SaveSettings();
        OnPropertyChanged(nameof(BlockedCount));
        OnPropertyChanged(nameof(AllowedCount));
    }

    public void ToggleExcluded(DisplayInfo display)
    {
        display.IsExcluded = !display.IsExcluded;
        if (display.IsExcluded && display.IsTargetPrimary)
        {
            display.IsTargetPrimary = false;
            EnsureValidTargetPrimary();
        }

        UpdateExcludedSettingsList();
        SyncConfiguration();
        SaveSettings();

        OnPropertyChanged(nameof(BlockedCount));
        OnPropertyChanged(nameof(AllowedCount));

        if (display.IsExcluded)
        {
            // Immediately relocate any windows already sitting on this newly excluded monitor!
            SweepNow();
        }
    }

    private void OnDisplaySettingChanged(DisplayInfo display, string propertyName)
    {
        if (propertyName == nameof(DisplayInfo.IsExcluded) && display.IsExcluded && display.IsTargetPrimary)
        {
            display.IsTargetPrimary = false;
            EnsureValidTargetPrimary();
        }

        UpdateExcludedSettingsList();
        SyncConfiguration();
        SaveSettings();
        OnPropertyChanged(nameof(BlockedCount));
        OnPropertyChanged(nameof(AllowedCount));
    }

    private void UpdateExcludedSettingsList()
    {
        _settings.ExcludedIdentifiers.Clear();
        foreach (var d in Displays.Where(d => d.IsExcluded))
        {
            if (!string.IsNullOrEmpty(d.HardwareId))
                _settings.ExcludedIdentifiers.Add(d.HardwareId);
            else if (!string.IsNullOrEmpty(d.FriendlyName))
                _settings.ExcludedIdentifiers.Add(d.FriendlyName);
            else
                _settings.ExcludedIdentifiers.Add(d.DeviceName);
        }
    }

    public void SweepNow()
    {
        _engine.SweepAllWindows();
    }

    public void IdentifyAllDisplays()
    {
        foreach (var display in Displays)
        {
            var overlay = new IdentifyOverlayWindow(display);
            overlay.Show();
        }
    }

    public void IdentifySingleDisplay(DisplayInfo display)
    {
        var overlay = new IdentifyOverlayWindow(display);
        overlay.Show();
    }

    private void HandleWindowRelocated(RelocationLogItem item)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            RelocationLog.Insert(0, item);
            if (RelocationLog.Count > 100)
            {
                RelocationLog.RemoveAt(RelocationLog.Count - 1);
            }
            RelocatedCount++;

            if (_settings.ShowNotificationOnRelocate)
            {
                _trayService.ShowRelocationNotification(item);
            }
        });
    }

    public void ClearLog()
    {
        RelocationLog.Clear();
        RelocatedCount = 0;
    }

    private void SyncConfiguration()
    {
        _engine.UpdateConfiguration(Displays.ToList(), IsGuardActive, _settings.SweepIntervalMs);
    }

    private void SaveSettings()
    {
        ConfigService.Save(_settings);
    }

    private void UpdateGuardStatusVisuals()
    {
        if (_isGuardActive)
        {
            StatusText = "GUARD ACTIVE";
            StatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
        }
        else
        {
            StatusText = "GUARD PAUSED";
            StatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
