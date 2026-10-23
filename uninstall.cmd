@echo off
echo Uninstalling NOTM...
powershell -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
echo Done.
timeout /t 2 >nul
