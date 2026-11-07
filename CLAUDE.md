# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Is

RFIDGear is a .NET 8 WPF desktop application for MIFARE chip encoding and batch processing, targeting x64/x86 Windows. It supports Elatec TWN4 readers and PC/SC compatible readers (via LibLogicalAccess).

## Build & Test Commands

```powershell
# Build (Debug or Release)
dotnet build RFiDGear.sln -c Debug
dotnet build RFiDGear.sln -c Release

# Run all tests
dotnet test RFiDGear.Tests/RFiDGear.Tests.csproj

# Run a single test
dotnet test RFiDGear.Tests/RFiDGear.Tests.csproj --filter "FullyQualifiedName~MyTestClass.MyTestMethod"
```

After a build, the extensions can be copied to `%ProgramData%\RFiDGear\Extensions` (`dotnet build RFiDGear.Extensions/CardCheckAssistant -p:DeployExtensionsToRFiDGear=true`; `RFIDGEAR_DEPLOY_EXTENSIONS=true` or a git-ignored `RFiDGear.Extensions/deploy.local.props` turn it on for every build). It is off by default on purpose: tests, clean checkouts and mutation runs also build the extensions and must not write into a real RFiDGear. See `RFiDGear.Extensions/DeployToRFiDGear.targets`.

The solution targets `net8.0-windows`. Tests require the `EnableWindowsTargeting` MSBuild property (already set in the test project) for non-Windows dev environments. Tests use `StaTestRunner.cs` to run on an STA thread — required for any WPF-touching test.

Setup/installer projects (`RFiDGearBundleSetup`, `RFiDGearSetup_x64/x86`) use WiX and are built separately; they are not part of `dotnet build`.

## Architecture

### MVVM + MEF

The app uses MVVM via **CommunityToolkit.Mvvm** (ObservableObject, RelayCommand) combined with **MEF** (Managed Extensibility Framework) for ViewModel discovery.

- ViewModels are exported with the `[ExportViewModel]` attribute.
- `ViewModelLocator` (extends `DynamicObject`) resolves them at runtime — `locator.SomeViewModel` dynamically looks up the MEF export.
- `App.xaml` registers DataTemplate bindings that map each ViewModel type to its View, with `x:Shared="False"` so each binding creates a new VM instance.
- There is **no DI container**: services are wired up manually via constructor injection and factory patterns in `AppStartupInitializer`.

### Startup Flow

`AppStartupInitializer` is the composition root. It:
1. Enforces a single-instance mutex.
2. Initializes Serilog logging.
3. Loads `runtime-defaults.json` (created on first run at `%LocalAppData%\RFiDGear\`).
4. Wires all services and passes them to `MainWindowViewModel`.

`MainWindowViewModel` handles reader monitoring, project loading, and task orchestration.

### Reader Abstraction

`ReaderDevice` (abstract) defines the reader contract. Two implementations:
- `ElatecNetProvider` — Elatec TWN4 readers
- `LibLogicalAccessProvider` — PC/SC compatible readers

The active provider is selected at startup from settings and injected into the task system.

### Task System

Tasks are modeled as sequential, indexed items with optional dependencies (prior task index + expected error code). Lazy evaluation: a task only executes if the dependency condition is satisfied.

Task categories:
- **DeviceHelper** — reader-level operations (connect, beep, LED)
- **GenericChip** — chip detection / UID reads
- **MifareClassic** — sector read/write/authenticate
- **MifareDesfire** — application/file CRUD (DES, 3DES, AES)
- **MifareUltralight** — page read/write

`TaskExecutionService` drives sequential execution with error handling; individual task VMs implement `ITaskViewModel`.

### Key Design Rule

**One-attempt rule**: Card-state-changing operations (write, authenticate, format) must **not** auto-retry internally. The caller (task orchestrator) owns retry logic. This prevents partial-state corruption when a card is removed mid-operation.

Normalized error codes used across the stack: `AuthFailure`, `PermissionDenied`, `ProtocolConstraint`, `TransportError`, `Unknown`.

### Settings & Persistence

- **Settings**: XML-serialized `settings.xml` in `%LocalAppData%\RFiDGear\`.
- **Runtime defaults**: `runtime-defaults.json` — reader selection, language, COM ports, MIFARE default keys, auto-update behavior.
- **Project files**: Task definitions stored as versioned XML.

### Logging

Serilog with rolling file sink; logs written to `%LocalAppData%\RFiDGear\log\`, 30-day retention. Route all errors through Serilog — do **not** use Windows EventLog (removed in commit 71ffab2).

### Extension/Plugin System

MEF enables extensions. `RFiDGear.Extensions.DesfirePluginSample` is the reference plugin project. Extensions export ViewModels and task implementations using the same `[ExportViewModel]` / MEF attributes as the host.

The extension contract is split in two assemblies. An extension references
only these, never `RFiDGear.exe`:

- `RFiDGear.Contracts` is the host service API (`IRFiDGearExtensionHost` and
  the project, execution, reader and culture services). It has no MEF
  dependency.
- `RFiDGear.ExtensionSdk` is the MEF-facing ABI: `[UiExtension]`,
  `HostPlacement`, `IUIExtension`, `IUIExtensionDetails`. It is versioned on
  its own (see its README). `RFiDGear.exe` forwards the old locations of
  these types so already-built extensions keep working.

Both public surfaces are frozen by approved snapshots under
`RFiDGear.Tests/ApiSurface/`; a change fails the test and is accepted by
regenerating the snapshot (`RFIDGEAR_UPDATE_API_SNAPSHOT=1`). Extensions are
discovered once at start-up from `%ProgramData%\RFiDGear\Extensions`, so
adding one needs an app restart.

An application extension (`HostPlacement.Application`) implements
`IApplicationUIExtension` from the SDK. RFiDGear creates the exported class on
the first click on its Extensions menu entry, never at start-up, and calls
`CreateContent(IRFiDGearExtensionHost host)` every time it is opened; the
service arrives as that argument because it does not exist yet when MEF
composes. It is deliberately not a MEF export. `ApplicationExtensionLauncher`
disposes the content when the window closes. An extension that sets `Uri`
still opens that page in a frame as before, without creating its class.
`RFiDGear.Extensions.CardCheckAssistant` is the first one and the reference
for the pattern (see its README).

A broken extension never stops the others or the application. `MefHelper`
probes each DLL (parts, export metadata, imports) before adding it to the
container, logs a failure with Serilog and skips that file. If the extensions
that passed still break the export queries, it rebuilds the container from the
host alone. `MefHelper.LastLoadResults` records the outcome per file.

An extension that needs more than the contract (services, a database driver,
a PDF engine) keeps that code in a support library named
`RFiDGear.ExtensionLibraries.<Name>`; the first is
`RFiDGear.ExtensionLibraries.CardCheck`. **The name must never start with
`RFiDGear.Extensions`, in any letter case.** `MefHelper` scans the extensions
folder for `RFiDGear.Extensions*.dll` and hands every match to MEF, so a library
called `RFiDGear.ExtensionsNative.*` or `RFiDGear.ExtensionSupport.*` would be
loaded as an extension at every start-up. A support library is plain `net8.0`
(no WPF), references no other RFiDGear project, compiles against host-owned
packages (Serilog, GemBox.Pdf) without shipping a copy of them, and sits flat
next to the extension that uses it: .NET finds it there, the host needs no
change for that. `CardCheckCoreBoundaryTests` pin the rules, including the
name, against the host's own scan.

### UI Structure

Views are organized under `Views/` by task category: `CommonTask/`, `GenericChipTask/`, `MifareClassicTask/`, `MifareDesfireTask/`, `MifareUltralightTask/`. Dialog flows use the **MVVM Dialogs** library for message boxes and file pickers.

## Key Dependencies

| Package | Purpose |
|---|---|
| CommunityToolkit.Mvvm 8.4 | ObservableObject, RelayCommand |
| Elatec.NET 0.6.1 | Elatec TWN4 reader SDK |
| LibLogicalAccessNetCE 3.6.0 | PC/SC reader provider |
| GemBox.Pdf 2025.x | PDF form population for reports |
| Serilog 4.3 | Structured logging |
| Portable.BouncyCastle 1.9 | Cryptography (MIFARE key derivation) |
| Newtonsoft.Json 13.0.4 | JSON serialization |
| Microsoft.Extensions.* 10.x | Logging abstractions, options |
