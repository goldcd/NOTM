; Inno Setup Script for NOTM (Not On This Monitor)
; Builds a standard Windows Setup Wizard (e.g. NOTM-Setup.exe)

#define MyAppName "NOTM"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "goldcd"
#define MyAppURL "https://github.com/goldcd/NOTM"
#define MyAppExeName "NOTM.exe"

[Setup]
AppId={{7E8B1A2C-9F3D-4A5B-8C6E-2D4E6F8A0B1C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=NOTM-Setup-v{#MyAppVersion}
SetupIconFile=Assets\app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Launch NOTM automatically on Windows startup"; GroupDescription: "Windows Integration:"

[Files]
Source: "dist\NOTM.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Assets\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Tasks: startupicon; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
