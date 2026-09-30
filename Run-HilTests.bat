@echo off
where pwsh.exe >nul 2>&1
if %ERRORLEVEL% == 0 (
    pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-HilTests.ps1"
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-HilTests.ps1"
)
exit /b %ERRORLEVEL%
