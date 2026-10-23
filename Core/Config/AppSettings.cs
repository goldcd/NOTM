using System.Collections.Generic;

namespace NOTM.Core.Config;

public class AppSettings
{
    public bool IsGuardEnabled { get; set; } = true;
    public string? TargetPrimaryHardwareId { get; set; }
    public string? TargetPrimaryDeviceName { get; set; }
    public List<string> ExcludedIdentifiers { get; set; } = new();

    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool KeepAudioAwake { get; set; } = true;
    public bool ShowNotificationOnRelocate { get; set; } = true;
    public int SweepIntervalMs { get; set; } = 1000;
}
