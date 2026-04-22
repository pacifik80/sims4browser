@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%scripts\run-live-capture.ps1"

set "EXIT_CODE=%ERRORLEVEL%"
echo.
if not "%EXIT_CODE%"=="0" (
    echo Capture run failed with exit code %EXIT_CODE%.
) else (
    echo Capture run finished.
)
echo Press any key to close this window...
pause >nul
exit /b %EXIT_CODE%
