@echo off
rem Signs the bundle engine and the bundle. The certificate is not part of the repository:
rem   RFIDGEAR_SIGN_PFX       path of the .pfx file
rem   RFIDGEAR_SIGN_PASSWORD  its password
if "%RFIDGEAR_SIGN_PFX%"=="" (
    echo Set RFIDGEAR_SIGN_PFX and RFIDGEAR_SIGN_PASSWORD first.
    exit /b 1
)

insignia -ib ../bin/Release/RFiDGearBundleSetup.exe -o ../bin/Release/engine.exe
signtool sign /f "%RFIDGEAR_SIGN_PFX%" /p "%RFIDGEAR_SIGN_PASSWORD%" /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 ../bin/Release/engine.exe
insignia -ab ../bin/Release/engine.exe ../bin/Release/RFiDGearBundleSetup.exe -o ../bin/Release/RFiDGearBundleSetup_signed.exe
signtool sign /f "%RFIDGEAR_SIGN_PFX%" /p "%RFIDGEAR_SIGN_PASSWORD%" /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 "../bin/Release/RFiDGearBundleSetup_signed.exe"
