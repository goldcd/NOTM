# NOTM Windows Uninstaller Script
$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "         Uninstalling NOTM                " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Stop NOTM if running
Write-Host "Closing running NOTM processes..." -ForegroundColor Gray
Get-Process -Name "NOTM" | Stop-Process -Force
Start-Sleep -Milliseconds 800

# Remove Start Menu shortcut
$startMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\NOTM.lnk"
if (Test-Path $startMenuShortcut) {
    Write-Host "Removing Start Menu shortcut..." -ForegroundColor Gray
    Remove-Item -Path $startMenuShortcut -Force
}

# Remove Startup Run registry key
Write-Host "Removing Windows Startup entry..." -ForegroundColor Gray
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "NOTM" -ErrorAction SilentlyContinue

# Remove Installed Apps registry key
Write-Host "Removing Windows Installed Apps registration..." -ForegroundColor Gray
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NOTM" -Recurse -Force -ErrorAction SilentlyContinue

# Remove Installation Directory
$installDir = Join-Path $env:LOCALAPPDATA "Programs\NOTM"
if (Test-Path $installDir) {
    Write-Host "Removing application files from $installDir..." -ForegroundColor Gray
    # Schedule deletion via background cmd if running from inside the install dir
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c timeout /t 1 /nobreak >nul & rmdir /s /q `"$installDir`"" -WindowStyle Hidden
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "    NOTM was successfully uninstalled!    " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
