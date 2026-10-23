# Build and publish NOTM as a standalone, zero-dependency Windows executable
$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Publishing NOTM (Single-File Release)   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$distDir = Join-Path $PSScriptRoot "dist"
if (Test-Path $distDir) {
    Remove-Item -Recurse -Force $distDir
}

dotnet publish NOTM.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $distDir

Write-Host ""
Write-Host "Publish complete! Executable located at:" -ForegroundColor Green
Write-Host (Join-Path $distDir "NOTM.exe") -ForegroundColor Yellow
Write-Host ""
Write-Host "You can move NOTM.exe anywhere or create a shortcut in your Startup folder." -ForegroundColor Green
