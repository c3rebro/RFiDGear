@echo off
rem Builds, signs and packs a release. Asks for the certificate (.pfx) and its password, then runs Build-Release.ps1,
rem Sign-Release.ps1 and Pack-Release.ps1. See Make-Release.ps1 (-CheckOnly tries the certificate and stops before the build).
where pwsh.exe >nul 2>&1
if %ERRORLEVEL% == 0 (
    pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Make-Release.ps1" %*
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Make-Release.ps1" %*
)
set "RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %RESULT%
