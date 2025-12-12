![](docs/assets/img/logoRG.png) 

# RFiDGear - .net 8 Mifare encoding tool.

Support for batch processing.

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/ac98d255ca38466bb5803f9e2e4a11ae)](https://www.codacy.com/app/c3rebro/rfidgear)

![](https://messgeraetetechnik-hansen.de/rfidgear/mainWnd.jpg) 

### [Info](https://c3rebro.github.io/RFiDGear/) | [Download](https://github.com/c3rebro/RFiDGear/releases) | [Report Bugs](https://github.com/c3rebro/RFiDGear/issues)

Requirements:

* minimum Microsoft Windows 7 32/64bit (or later)
* Elatec Reader TWN4
* LibLogicalAccess 3.6.0 (or later - included)

In case of PCSC Provider: a PCSC compatibile Reader:
(Examples - others are reported to work)
* Omnikey 5321
* Sciel SCL3711
* ACR 122U

## Codebase overview

RFiDGear is a WPF desktop app structured around the MVVM pattern. Application resources in `App.xaml` register view-model-to-view mappings for dialogs and chip setup screens so XAML can instantiate the correct views dynamically, while Serilog logging is configured in `App.xaml.cs` to capture unhandled exceptions to rolling log files.

Managed Extensibility Framework (MEF) composition is used to discover view models. The `ViewModelLocator` exposes exports tagged as `"ViewModel"` as dynamic properties for XAML bindings, caching the composed instances to avoid repeated resolution.

Startup flows are coordinated by `AppStartupInitializer`, which establishes a single-instance mutex, prepares the Windows event log source, and captures command-line arguments in an `AppStartupContext` that seeds initialization in the main window view model.

`MainWindowViewModel` orchestrates settings bootstrapping, update notifications, context menu construction, reader monitoring, and project initialization. During `InitializeAsync`, it loads persisted defaults, configures timers for chip reads and task timeouts, and wires task execution callbacks that keep UI bindings in sync with reader state and task progress.

Task execution flows live in `Services/TaskExecution/TaskExecutionService.cs`. Adapters around `DispatcherTimer` manage device discovery, chip hydration, selection synchronization, and the task loop, while structured Serilog logging (via `NullTaskExecutionLogger`) captures JSON details for each stage.

### Configuring runtime defaults

RFiDGear writes a `runtime-defaults.json` file to `%LocalAppData%\RFiDGear` the first time it starts. You can edit this file with any text editor to control the initial values used for reader selection, language, auto-update behavior, and the default MIFARE keys that seed new `settings.xml` files—no code changes or recompilation required.

See `runtime-defaults.sample.json` for a complete set of defaults that mirrors the built-in configuration, including reader options, auto-update flags, COM port settings, default MIFARE keys, and the pre-populated quick-check key list. Copy this file to `%LocalAppData%\RFiDGear` and rename it to `runtime-defaults.json` to start from the sample.
