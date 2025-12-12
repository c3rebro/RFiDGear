![](https://messgeraetetechnik-hansen.de/rfidgear/logoRG.png) 

# RFiDGear
**Your essential gear to encode (read - modify - write) rfid tags.**

Support for batch processing.

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/ac98d255ca38466bb5803f9e2e4a11ae)](https://www.codacy.com/app/c3rebro/rfidgear)

![](https://messgeraetetechnik-hansen.de/rfidgear/mainWnd.jpg) 

### [Info](https://https://c3rebro.github.io/rfidgear) | [Download](https://github.com/c3rebro/RFiDGear/releases) | [Report Bugs](https://github.com/c3rebro/RFiDGear/issues)

Requirements:

* Microsoft Windows 7 32/64bit (or later)
* Elatec Reader TWN4
* LibLogicalAccess 3.6.0 (or later - included)

a (tested) PCSC compatibile Reader:
Omnikey 5321
Sciel SCL3711
ACR 122U

### Configuring runtime defaults

RFiDGear writes a `runtime-defaults.json` file to `%LocalAppData%\RFiDGear` the first time it starts. You can edit this file with any text editor to control the initial values used for reader selection, language, auto-update behavior, and the default MIFARE keys that seed new `settings.xml` files—no code changes or recompilation required.

See `runtime-defaults.sample.json` for a complete set of defaults that mirrors the built-in configuration, including reader options, auto-update flags, COM port settings, default MIFARE keys, and the pre-populated quick-check key list. Copy this file to `%LocalAppData%\RFiDGear` and rename it to `runtime-defaults.json` to start from the sample.
