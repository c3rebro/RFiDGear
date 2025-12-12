![](docs/assets/img/logoRG.png) 

# RFiDGear - Mifare encoding tool.

Support for batch processing.

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/ac98d255ca38466bb5803f9e2e4a11ae)](https://www.codacy.com/app/c3rebro/rfidgear)

![](https://messgeraetetechnik-hansen.de/rfidgear/mainWnd.jpg) 

### [Info](https://c3rebro.github.io/RFiDGear/) | [Download](https://github.com/c3rebro/RFiDGear/releases) | [Report Bugs](https://github.com/c3rebro/RFiDGear/issues)

Requirements:

* Microsoft Windows 7 32/64bit (or later)
* Elatec Reader TWN4
* LibLogicalAccess 3.6.0 (or later - included)

a (tested) PCSC compatibile Reader:
Omnikey 5321
Sciel SCL3711
ACR 122U

## System requirements

The original German help includes a concise hardware and OS checklist:

* A PC that meets at least the Windows 7 (32/64-bit) minimum specifications
* Microsoft Windows 7 or newer
* At least one PC/SC-compatible encoder—tested examples include HID/Omnikey 5x21 devices, Sciel SCL3711, and ACR 122U
* Optional: the LibLogicalAccess provider stack supplied with the installer

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

## Codebase overview

RFiDGear is a WPF desktop app structured around the MVVM pattern. Application resources in `App.xaml` register view-model-to-view mappings for dialogs and chip setup screens so XAML can instantiate the correct views dynamically, while Serilog logging is configured in `App.xaml.cs` to capture unhandled exceptions to rolling log files.

Managed Extensibility Framework (MEF) composition is used to discover view models. The `ViewModelLocator` exposes exports tagged as `"ViewModel"` as dynamic properties for XAML bindings, caching the composed instances to avoid repeated resolution.

Startup flows are coordinated by `AppStartupInitializer`, which establishes a single-instance mutex, prepares the Windows event log source, and captures command-line arguments in an `AppStartupContext` that seeds initialization in the main window view model.

`MainWindowViewModel` orchestrates settings bootstrapping, update notifications, context menu construction, reader monitoring, and project initialization. During `InitializeAsync`, it loads persisted defaults, configures timers for chip reads and task timeouts, and wires task execution callbacks that keep UI bindings in sync with reader state and task progress.

Task execution flows live in `Services/TaskExecution/TaskExecutionService.cs`. Adapters around `DispatcherTimer` manage device discovery, chip hydration, selection synchronization, and the task loop, while structured Serilog logging (via `NullTaskExecutionLogger`) captures JSON details for each stage.

### Configuring runtime defaults

RFiDGear writes a `runtime-defaults.json` file to `%LocalAppData%\RFiDGear` the first time it starts. You can edit this file with any text editor to control the initial values used for reader selection, language, auto-update behavior, and the default MIFARE keys that seed new `settings.xml` files—no code changes or recompilation required.

See `runtime-defaults.sample.json` for a complete set of defaults that mirrors the built-in configuration, including reader options, auto-update flags, COM port settings, default MIFARE keys, and the pre-populated quick-check key list. Copy this file to `%LocalAppData%\RFiDGear` and rename it to `runtime-defaults.json` to start from the sample.
