# NOTM Windows Installer Script
# Installs NOTM cleanly to %LocalAppData%\Programs\NOTM (No Administrator Rights Required)
# Can be run locally or streamed directly from GitHub:
#   irm https://raw.githubusercontent.com/goldcd/NOTM/main/install.ps1 | iex

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "       Installing NOTM to Windows         " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

$installDir = Join-Path $env:LOCALAPPDATA "Programs\NOTM"
$repoOwner = "goldcd"
$repoName = "NOTM"
$releaseUrl = "https://github.com/$repoOwner/$repoName/releases/latest/download/NOTM.exe"
$rawBaseUrl = "https://raw.githubusercontent.com/$repoOwner/$repoName/main"

# Create destination directory
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
}

$targetExe = Join-Path $installDir "NOTM.exe"
$hasBinary = $false

# 1. Check if running from a local repository with a pre-built binary
if ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot "dist\NOTM.exe"))) {
    Write-Host "Using local pre-built binary..." -ForegroundColor Gray
    Copy-Item -Path (Join-Path $PSScriptRoot "dist\NOTM.exe") -Destination $targetExe -Force
    $hasBinary = $true
}
# 2. Check if running locally and can build from source
elseif ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot "publish.ps1"))) {
    Write-Host "Building release binary locally..." -ForegroundColor Yellow
    & "$PSScriptRoot\publish.ps1"
    if (Test-Path (Join-Path $PSScriptRoot "dist\NOTM.exe")) {
        Copy-Item -Path (Join-Path $PSScriptRoot "dist\NOTM.exe") -Destination $targetExe -Force
        $hasBinary = $true
    }
}

# 3. Running remotely from GitHub (e.g. irm ... | iex)
if (-not $hasBinary) {
    Write-Host "Downloading latest NOTM.exe from GitHub Releases..." -ForegroundColor Cyan
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
        Invoke-WebRequest -Uri $releaseUrl -OutFile $targetExe -UseBasicParsing
        $hasBinary = $true
    } catch {
        Write-Host "Prebuilt release binary not yet published on GitHub Releases." -ForegroundColor Yellow
        Write-Host "Attempting fallback build from GitHub repository archive..." -ForegroundColor Gray

        # Fallback: Check if dotnet SDK is installed
        $dotnetCmd = Get-Command "dotnet" -ErrorAction SilentlyContinue
        if ($dotnetCmd) {
            $tempZip = Join-Path $env:TEMP "NOTM_source.zip"
            $tempExtract = Join-Path $env:TEMP "NOTM_extract"

            try {
                Write-Host "Downloading source archive from GitHub..." -ForegroundColor Cyan
                Invoke-WebRequest -Uri "https://github.com/$repoOwner/$repoName/archive/refs/heads/main.zip" -OutFile $tempZip -UseBasicParsing
                
                if (Test-Path $tempExtract) { Remove-Item -Path $tempExtract -Recurse -Force }
                Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force
                
                $sourceDir = Join-Path $tempExtract "NOTM-main"
                Write-Host "Publishing NOTM standalone executable..." -ForegroundColor Yellow
                & dotnet publish (Join-Path $sourceDir "NOTM.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o (Join-Path $sourceDir "dist")
                
                $builtExe = Join-Path $sourceDir "dist\NOTM.exe"
                if (Test-Path $builtExe) {
                    Copy-Item -Path $builtExe -Destination $targetExe -Force
                    $hasBinary = $true
                }
            } finally {
                if (Test-Path $tempZip) { Remove-Item -Path $tempZip -Force -ErrorAction SilentlyContinue }
                if (Test-Path $tempExtract) { Remove-Item -Path $tempExtract -Recurse -Force -ErrorAction SilentlyContinue }
            }
        }
    }
}

if (-not $hasBinary -or -not (Test-Path $targetExe)) {
    Write-Error "Failed to install NOTM.exe. Please ensure internet access is available or download NOTM directly from https://github.com/$repoOwner/$repoName"
    return
}

# Stop any running NOTM instances before replacing shortcuts / final launch
Write-Host "Checking for running NOTM instances..." -ForegroundColor Gray
Get-Process -Name "NOTM" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# Install icon
$iconPath = Join-Path $installDir "app.ico"
if ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot "Assets\app.ico"))) {
    Copy-Item -Path (Join-Path $PSScriptRoot "Assets\app.ico") -Destination $iconPath -Force
} else {
    try {
        Invoke-WebRequest -Uri "$rawBaseUrl/Assets/app.ico" -OutFile $iconPath -UseBasicParsing -ErrorAction SilentlyContinue
    } catch {}
}

# Install uninstaller scripts
$uninstallPs1 = Join-Path $installDir "uninstall.ps1"
$uninstallCmd = Join-Path $installDir "uninstall.cmd"
if ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot "uninstall.ps1"))) {
    Copy-Item -Path (Join-Path $PSScriptRoot "uninstall.ps1") -Destination $uninstallPs1 -Force
    Copy-Item -Path (Join-Path $PSScriptRoot "uninstall.cmd") -Destination $uninstallCmd -Force
} else {
    try {
        Invoke-WebRequest -Uri "$rawBaseUrl/uninstall.ps1" -OutFile $uninstallPs1 -UseBasicParsing -ErrorAction SilentlyContinue
        Invoke-WebRequest -Uri "$rawBaseUrl/uninstall.cmd" -OutFile $uninstallCmd -UseBasicParsing -ErrorAction SilentlyContinue
    } catch {}
}

# Create Start Menu Shortcut
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "NOTM.lnk"

Write-Host "Creating Start Menu shortcut..." -ForegroundColor Gray
$wscript = New-Object -ComObject WScript.Shell
$shortcut = $wscript.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetExe
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "NOTM - Not on That Monitor"
if (Test-Path $iconPath) {
    $shortcut.IconLocation = $iconPath
} else {
    $shortcut.IconLocation = "$targetExe,0"
}
$shortcut.Save()

# Register in Windows "Installed Apps" (Settings > Apps > Installed Apps)
Write-Host "Registering in Windows Installed Apps..." -ForegroundColor Gray
$uninstallRegKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NOTM"
if (-not (Test-Path $uninstallRegKey)) {
    New-Item -Path $uninstallRegKey -Force | Out-Null
}

Set-ItemProperty -Path $uninstallRegKey -Name "DisplayName" -Value "NOTM - Not on That Monitor"
Set-ItemProperty -Path $uninstallRegKey -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $uninstallRegKey -Name "Publisher" -Value "goldcd"
Set-ItemProperty -Path $uninstallRegKey -Name "DisplayIcon" -Value $targetExe
Set-ItemProperty -Path $uninstallRegKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $uninstallRegKey -Name "UninstallString" -Value "`"$uninstallCmd`""
Set-ItemProperty -Path $uninstallRegKey -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallRegKey -Name "NoRepair" -Value 1 -Type DWord

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "    Installation Completed Successfully!  " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "NOTM is now installed to:" -ForegroundColor Cyan
Write-Host "  $targetExe" -ForegroundColor Yellow
Write-Host ""
Write-Host "You can find NOTM in your Windows Start Menu or search." -ForegroundColor Green
Write-Host "Launching NOTM now..." -ForegroundColor Cyan

Start-Process -FilePath $targetExe -WorkingDirectory $installDir
