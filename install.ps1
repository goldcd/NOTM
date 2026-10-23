# NOTM Windows Installer Script
# Installs NOTM cleanly to %LocalAppData%\Programs\NOTM (No Admin Rights Required)
$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "       Installing NOTM to Windows         " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$installDir = Join-Path $env:LOCALAPPDATA "Programs\NOTM"
$sourceExe = Join-Path $PSScriptRoot "dist\NOTM.exe"

# If not yet published, publish now
if (-not (Test-Path $sourceExe)) {
    Write-Host "Building release executable first..." -ForegroundColor Yellow
    & "$PSScriptRoot\publish.ps1"
}

if (-not (Test-Path $sourceExe)) {
    Write-Error "Could not find or build NOTM.exe"
}

# Stop any running NOTM instances
Write-Host "Checking for running NOTM instances..." -ForegroundColor Gray
Get-Process -Name "NOTM" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# Create destination directory
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
}

# Copy binary and assets
Write-Host "Copying files to $installDir..." -ForegroundColor Gray
Copy-Item -Path $sourceExe -Destination (Join-Path $installDir "NOTM.exe") -Force

$iconSrc = Join-Path $PSScriptRoot "Assets\app.ico"
if (Test-Path $iconSrc) {
    Copy-Item -Path $iconSrc -Destination (Join-Path $installDir "app.ico") -Force
}

# Copy uninstall script to install directory
Copy-Item -Path (Join-Path $PSScriptRoot "uninstall.ps1") -Destination (Join-Path $installDir "uninstall.ps1") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "uninstall.cmd") -Destination (Join-Path $installDir "uninstall.cmd") -Force

# Create Start Menu Shortcut
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "NOTM.lnk"

Write-Host "Creating Start Menu shortcut..." -ForegroundColor Gray
$wscript = New-Object -ComObject WScript.Shell
$shortcut = $wscript.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDir "NOTM.exe"
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "NOTM - Monitor Guard"
$shortcut.IconLocation = Join-Path $installDir "NOTM.exe,0"
$shortcut.Save()

# Register in Windows "Installed Apps" (Settings > Apps > Installed Apps)
Write-Host "Registering in Windows Installed Apps..." -ForegroundColor Gray
$uninstallRegKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NOTM"
if (-not (Test-Path $uninstallRegKey)) {
    New-Item -Path $uninstallRegKey -Force | Out-Null
}

Set-ItemProperty -Path $uninstallRegKey -Name "DisplayName" -Value "NOTM - Monitor Guard"
Set-ItemProperty -Path $uninstallRegKey -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $uninstallRegKey -Name "Publisher" -Value "goldcd"
Set-ItemProperty -Path $uninstallRegKey -Name "DisplayIcon" -Value (Join-Path $installDir "NOTM.exe")
Set-ItemProperty -Path $uninstallRegKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $uninstallRegKey -Name "UninstallString" -Value "`"$installDir\uninstall.cmd`""
Set-ItemProperty -Path $uninstallRegKey -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallRegKey -Name "NoRepair" -Value 1 -Type DWord

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "    Installation Completed Successfully!  " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "NOTM is now installed to:" -ForegroundColor Cyan
Write-Host "  $installDir\NOTM.exe" -ForegroundColor Yellow
Write-Host ""
Write-Host "You can find NOTM in your Windows Start Menu or search." -ForegroundColor Green
Write-Host "Launching NOTM now..." -ForegroundColor Cyan

Start-Process (Join-Path $installDir "NOTM.exe")
