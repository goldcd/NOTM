@echo off
echo Building NOTM...
dotnet build NOTM.sln -c Release
if %ERRORLEVEL% equ 0 (
    echo.
    echo Build succeeded with 0 errors!
) else (
    echo.
    echo Build failed!
)
pause
