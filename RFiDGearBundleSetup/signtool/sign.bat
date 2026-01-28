insignia -ib ../bin/Release/RFiDGearBundleSetup.exe -o ../bin/Release/engine.exe
signtool sign /f "MessgeraetetechnikHansen.pfx" /p MessgeraetetechnikHansen /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 ../bin/Release/engine.exe
insignia -ab ../bin/Release/engine.exe ../bin/Release/RFiDGearBundleSetup.exe -o ../bin/Release/RFiDGearBundleSetup_signed.exe
signtool sign /f "MessgeraetetechnikHansen.pfx" /p MessgeraetetechnikHansen /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 "../bin/Release/RFiDGearBundleSetup_signed.exe"

pause