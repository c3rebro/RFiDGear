@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-HilTests.ps1"
exit /b %ERRORLEVEL%
