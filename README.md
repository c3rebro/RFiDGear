![](docs/assets/img/logoRG.png) 

# RFiDGear - .NET 10 Mifare encoding tool.

Support for batch processing.

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/ac98d255ca38466bb5803f9e2e4a11ae)](https://www.codacy.com/app/c3rebro/rfidgear)
![](https://messgeraetetechnik-hansen.de/rfidgear/mainWnd.jpg) 

### [Info](https://c3rebro.github.io/RFiDGear/) | [Download](https://github.com/c3rebro/RFiDGear/releases) | [Report Bugs](https://github.com/c3rebro/RFiDGear/issues)

Requirements:

* Microsoft Windows 11, or Windows 10 in a version [supported by .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md), 32/64bit, with the .NET 10 Desktop Runtime
* Elatec Reader TWN4 Multi or
* Elatec Reader TWN4 Legic 45 or
* PC/SC compatible Reader

In case of PCSC Provider: a PCSC compatibile Reader:
(Examples - others are reported to work)
* Omnikey 5321
* Omnikey 5422
* Sciel SCL3711
* ACR 122U

## System requirements

The original German help includes a concise hardware and OS checklist:

* A PC that meets the minimum specifications of Windows 10 (in a version supported by .NET 10) or Windows 11 (32/64-bit)
* Microsoft Windows 11, or Windows 10 in a version [supported by .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md), with the .NET 10 Desktop Runtime
* At least one PC/SC-compatible encoder—tested examples include HID/Omnikey 5x21 devices, Sciel SCL3711, and ACR 122U
* Optional: the LibLogicalAccess provider stack supplied with the installer

## Android Support

Join the GoogleGroup and become a tester:

* Load a prebuilt RFiDGear project into the Android App
* Perform a QuickCheck on Android
* [GoogleGroup](https://groups.google.com/g/rfidgear/members/)
  
## Task model and supported operations

RFiDGear organizes programming steps as sequential “tasks.” Each task has an index that drives execution order, a user-defined label, and an internal error code captured during execution. Tasks can also depend on the outcome of an earlier task (index + expected error code) before they run. They can be executed individually, as a batch, or automatically when a new chip is detected.

Task types include:

* Device-independent helpers
  * Populate a PDF form with static text or variable placeholders
  * Simple logic building blocks for if/then checks
* Chip-generic checks
  * Validate a tag’s UID
  * Verify the detected chip type
* MIFARE Classic
  * Read data blocks
  * Write data blocks
  * Inspect whether a sector is still unused
* MIFARE DESFire
  * Check for application existence
  * PICC-level operations: change the master key or format the card
  * Application-level operations: create apps, change keys, delete apps, authenticate
  * File-level operations: create, write, read, and delete files

## Main window quick start

The main window exposes keyboard-driven file and options commands described in the localized help:

* **Open project** (`Alt` + `D`, `P`): restore a previously saved project
* **Save** (`Alt` + `D`, `S`): persist the current task definitions as the default database
* **Save as…** (`Alt` + `D`, `U`): store multiple projects under distinct paths
* **Language** (`Alt` + `O`, `L`): change the UI language (requires restart)
* **Load last project on startup** (`Alt` + `O`, `P`): automatically reopen the default project saved via the file menu
* **Move keys of a project to the Credential Manager…** (file menu): see the next section

## Keeping card keys out of project files

A project file normally holds the card keys of its tasks as text. A task can instead name a key by a *reference* to the Windows Credential Manager: the project then holds the reference, and the key is read from the Credential Manager when the task runs. The key of a bound slot is not written to the project file, so a project whose key slots are all bound can be copied, backed up and mailed without its keys. A slot that is not bound still holds the key typed into it: before you share a project, check the boxes that are left (after a migration, those the report counts as not moved).

**Binding a key box.** In a task dialog, every section with card keys (DESFire card mastering, application mastering and data explorer, MIFARE Classic key tab) has a button **Key manager…** at the bottom. It opens the key manager for the keys of that section. For each key you choose whether it is typed into the task or named by a *reference*: a key store and a name. Switching a key that is typed in to a reference asks first, because the key is not kept anywhere else. A name is letters, digits and `. _ : / -`, up to 128 characters. A key box that has a reference is greyed out in the task dialog, with the name below it; point at the name to see the key store. For the Windows Credential Manager, **Store key** in the key manager puts the key under the name: the digits are typed into a password box (Enter stores them too), are not shown again, and are not kept by RFiDGear. The other key store, *Connected external application*, asks a program that is connected to RFiDGear through external control for the key when the task runs; the key manager says whether such a program is connected. Without the key manager, `cmdkey /generic:RFiDGear/key/<reference> /user:RFiDGear /pass` stores a key and asks for the digits at a prompt (a key written on the command line can be read by other programs and ends up in shell histories). The credential holds the hexadecimal digits of the key: 12 for a MIFARE Classic key, 32 for a DES or AES key, 48 for a 3K3DES key.

**Seeing and managing the stored keys.** File > **Key manager…** needs no project. It lists the keys that are stored in the Windows Credential Manager for RFiDGear, by name (a key is never shown), and it stores a key under a name: you choose the kind (MIFARE Classic, 12 digits; DES, 2K3DES or AES, 32 digits; 3K3DES, 48 digits) and type the digits into a password box. If a key is stored under that name already, RFiDGear asks before it replaces it. **Remove** deletes a key from the Credential Manager after asking; a project or task that names it can no longer get the key. The window also says whether a program that is connected through external control supplies keys.

**A key that cannot be obtained** (no such credential, the wrong length, the Credential Manager does not answer) stops the task before the card is touched, with the result `KeyUnavailable`. A bound slot never falls back to a key that is still typed into the task, and a task never reuses the key of an earlier task.

**Moving an existing project.** File > **Move keys of a project to the Credential Manager…** takes a project whose keys are typed in, stores each different key once in the Credential Manager under a random reference (`migrated/…`), and saves a *new* project that names them. The project you choose is only read, and an existing file is never overwritten. If a step fails, everything the attempt created is removed again; if something cannot be removed (a credential, or the new file while another program has it open), the message names it so that you can remove it by hand. A typed value that is not a key of the length its slot needs is not moved: the report counts it, and it stays in the new project as it was typed. Afterwards you are asked whether the old project may be deleted; before it is, the new project is read once more and every key it refers to is compared with the key the old project holds.

**What this does and does not protect.** It protects against a key that ends up in a project file, a backup or an e-mail. It does not protect against a program that runs as the same Windows user: the Credential Manager gives the secret to every process of that user. The keys stay with that user on that computer; a project with references does not run on another computer or for another user until the keys are stored there too. Versions of RFiDGear before 2.1 do not know references: they open such a project, but without the keys. Plug-ins can add other places to read keys from (see `RFiDGear.ExtensionSdk/README.md`).

## Codebase overview

RFiDGear is a WPF desktop app structured around the MVVM pattern. Application resources in `App.xaml` register view-model-to-view mappings for dialogs and chip setup screens so XAML can instantiate the correct views dynamically, while Serilog logging is configured in `App.xaml.cs` to capture unhandled exceptions to rolling log files.

Managed Extensibility Framework (MEF) composition is used to discover view models. The `ViewModelLocator` exposes exports tagged as `"ViewModel"` as dynamic properties for XAML bindings, caching the composed instances to avoid repeated resolution.

Startup flows are coordinated by `AppStartupInitializer`, which establishes a single-instance mutex, prepares the Windows event log source, and captures command-line arguments in an `AppStartupContext` that seeds initialization in the main window view model.

`MainWindowViewModel` orchestrates settings bootstrapping, update notifications, context menu construction, reader monitoring, and project initialization. During `InitializeAsync`, it loads persisted defaults, configures timers for chip reads and task timeouts, and wires task execution callbacks that keep UI bindings in sync with reader state and task progress.

Task execution flows live in `Services/TaskExecution/TaskExecutionService.cs`. Adapters around `DispatcherTimer` manage device discovery, chip hydration, selection synchronization, and the task loop, while structured Serilog logging (via `NullTaskExecutionLogger`) captures JSON details for each stage.

### Configuring runtime defaults

RFiDGear writes a `runtime-defaults.json` file to `%LocalAppData%\RFiDGear` the first time it starts. You can edit this file with any text editor to control the initial values used for reader selection, language, auto-update behavior, and the default MIFARE keys that seed new `settings.xml` files—no code changes or recompilation required.

See `runtime-defaults.sample.json` for a complete set of defaults that mirrors the built-in configuration, including reader options, auto-update flags, COM port settings, default MIFARE keys, and the pre-populated quick-check key list. Copy this file to `%LocalAppData%\RFiDGear` and rename it to `runtime-defaults.json` to start from the sample.

## Known Limitations

### PC/SC provider (LibLogicalAccess) is not available in Remote Desktop sessions

The Windows Smart Card service (`SCardSvr`) is disrupted when you connect to a machine via Remote Desktop (RDP). Windows RDP injects virtual smart card readers into the remote machine's PC/SC stack, which causes `SCardSvr` to stop and restart. Any call to `SCardEstablishContext` — which LibLogicalAccess performs on startup — will fail with `SCARD_E_NO_SERVICE (0x8010001D)` for the duration of the RDP session.

**Effect in RFiDGear:** If the PC/SC provider is selected and the app is running inside an RDP session, it will automatically fall back to *No Reader* and display a warning in the reader settings and status bar. The restriction is lifted only by closing the RDP session.

**Elatec TWN4 readers are not affected** — they communicate over USB CDC (serial) and do not rely on the Smart Card service.

**Workaround (administrator-controlled):** To prevent RDP from redirecting smart cards to the remote machine, set the following registry value on the remote machine:

```
HKLM\SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services
  fEnableSmartCard = 0  (DWORD)
```

This stops RDP from injecting virtual readers and allows `SCardSvr` to remain stable, restoring PC/SC functionality under RDP.

See [issue #203](https://github.com/c3rebro/RFiDGear/issues/203) for technical details and current status.
