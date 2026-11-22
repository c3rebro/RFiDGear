@echo off
rem Builds the Store package and signs it with a test certificate of yours, so that it can be installed on this PC. Asks for the
rem certificate (.pfx) and its password, then runs Build-StorePackage.ps1 -Sign. See Make-StorePackage.ps1 (-CheckOnly tries the
rem certificate and stops before the build).
where pwsh.exe >nul 2>&1
if %ERRORLEVEL% == 0 (
    pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Make-StorePackage.ps1" %*
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Make-StorePackage.ps1" %*
)
set "RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %RESULT%
