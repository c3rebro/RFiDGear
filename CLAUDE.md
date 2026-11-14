# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with
code in this repository.

## What This Project Is

RFIDGear is a .NET 10 WPF desktop application for MIFARE chip encoding and batch
processing, targeting x64/x86 Windows. It supports Elatec TWN4 readers and PC/SC
compatible readers (via LibLogicalAccess).

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

After a build, the extensions can be copied to
`%ProgramData%\RFiDGear\Extensions`:

```powershell
dotnet build RFiDGear.Extensions/CardCheckAssistant `
  -p:DeployExtensionsToRFiDGear=true
```

The environment variable `RFIDGEAR_DEPLOY_EXTENSIONS=true` or a git-ignored
`RFiDGear.Extensions/deploy.local.props` turn it on for every build. It is off
by default on purpose: tests, clean checkouts and mutation runs also build the
extensions and must not write into a real RFiDGear. See
`RFiDGear.Extensions/DeployToRFiDGear.targets`.

Everything targets .NET 10: the host (`RFiDGear`), the test projects, the
sample plugin, the extension contract (`RFiDGear.Contracts`,
`RFiDGear.ExtensionSdk`) and `CardCheckAssistant` as `net10.0-windows` (written
`net10.0-windows7.0` in some projects, which is the same), and the sample CLI
and the `CardCheck` library as plain `net10.0`. The contract assemblies keep
their assembly version `1.0.0.0`, so an extension that was built earlier against
`net8.0` keeps binding to them: `RFiDGear.Tests/Fixtures/LegacyExtensions`
holds prebuilt `net8.0` extensions, and the tests load them in the host.
`global.json` pins the SDK to
10.0.x, because a newer preview SDK on the machine would otherwise be picked
up. Visual Studio 2022 cannot target `net10.0`; use Visual Studio 2026 or the
`dotnet` CLI. Tests require the
`EnableWindowsTargeting` MSBuild property (already set in the test project) for
non-Windows dev environments. Tests use `StaTestRunner.cs` to run on an STA
thread — required for any WPF-touching test. The test projects run on xunit.v3
(the package `xunit.v3.mtp-off`: the .NET 10 SDK refuses to run a project that
is built on Microsoft.Testing.Platform through the VSTest path of `dotnet test`,
and the commands, filters and loggers here use that path), so they are
executables; `dotnet test` and `--filter` work as above (see
`DEVELOPER_NOTES.md`).

Setup/installer projects (`RFiDGearBundleSetup`, `RFiDGearSetup_x64/x86`) use
WiX 3.14 and are built separately; they are not part of `dotnet build`. Build
the host and `DesfirePluginSample` in Release for the platform first, then the
MSI project, then the bundle, with the MSBuild of Visual Studio 2026 (the
bundle references the host project, which needs SDK 10 and so MSBuild 18):

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"
& $msbuild RFiDGearBundleSetup\RFiDGearBundleSetup.wixproj `
  -p:Configuration=Release -p:Platform=x86
```

The bundle carries the .NET 10 Desktop Runtime (x64 and x86). The installers are
not in the repository: `RFiDGearBundleSetup/Get-DotNetRuntime.ps1` downloads the
versions pinned in `dotnet-runtime.json` (SHA-512 and Microsoft signature are
checked) into the git-ignored `runtime/` folder, and the bundle project runs it
before every build. A new runtime version means new file names in
`dotnet-runtime.json` and in `Bundle.wxs`. Signing needs `RFIDGEAR_SIGN_PFX` and
`RFIDGEAR_SIGN_PASSWORD`; no certificate is kept in the repository.

## Architecture

### MVVM + MEF

The app uses MVVM via **CommunityToolkit.Mvvm** (ObservableObject, RelayCommand)
combined with **MEF** (Managed Extensibility Framework) for ViewModel discovery.

- ViewModels are exported with the `[ExportViewModel]` attribute.
- `ViewModelLocator` (extends `DynamicObject`) resolves them at runtime —
  `locator.SomeViewModel` dynamically looks up the MEF export.
- `App.xaml` registers DataTemplate bindings that map each ViewModel type to its
  View, with `x:Shared="False"` so each binding creates a new VM instance.
- There is **no DI container**: services are wired up manually via constructor
  injection and factory patterns in `AppStartupInitializer`.

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

The active provider is selected at startup from settings and injected into the
task system.

### Task System

Tasks are modeled as sequential, indexed items with optional dependencies (prior
task index + expected error code). Lazy evaluation: a task only executes if the
dependency condition is satisfied.

Task categories:

- **DeviceHelper** — reader-level operations (connect, beep, LED)
- **GenericChip** — chip detection / UID reads
- **MifareClassic** — sector read/write/authenticate
- **MifareDesfire** — application/file CRUD (DES, 3DES, AES)
- **MifareUltralight** — page read/write

`TaskExecutionService` drives sequential execution with error handling;
individual task VMs implement `ITaskViewModel`.

### Automatic Mode

Continuous card handling is one state machine, `AutoModeController`
(`Services/AutoMode`). The menu toggle, the `AUTOMODE=1` start-up argument and
the host API all drive it, and nothing else polls the reader for new cards.
Its `IsEnabled` flag is the state; the reader timer is only a clock, because
every run switches the timer off and puts it back.

A card is run once per *presence*: a poll that finds the reader empty ends the
presence, so a card that stays is not run again, and a card that is taken away
and put back is. The card is remembered when its run is admitted, so a run that
fails is never repeated by the machine (the one-attempt rule). A run made by
hand counts as processing the card on the reader, and the card it really read
(the execution service reads once more) is the one remembered. A fault, a
timeout, a run the watchdog gave up or a lost reader stops the mode instead of
guessing, whoever started the run; an ordinary task failure does not. A run of
the mode that waits for a reader scan of an extension is withdrawn by a stop.

### External Control Bridge

A program next to RFiDGear (SIE Encode is the reason it exists, issue 232) can
drive the official process over ONE named pipe, JSON-RPC 2.0 (StreamJsonRpc
2.26.150), built into the host: `Services/ExternalControl`. It listens only
when the setting `EnableExternalControl` is on OR the start-up argument
`EXTERNALCONTROL=1` is given. The contract is `RFiDGear.Bridge.Contracts`
(plain `net10.0`, no references, assembly version `1.0.0.0`, shipped with the
installer); `RFiDGear.Bridge.Contracts/README.md` is the protocol,
`CLIENT-GUIDE.md` the guide, `RFiDGear.Bridge.SampleClient` a working client
whose tests (`BridgeSampleClientTests`) prove the guide. The contract is NOT
frozen until the hands-on checklist `RFiDGear.HIL.Tests/EXTERNAL-CONTROL.md` has
passed with a real reader; until then a snapshot may be re-baselined.

- `ExternalControlService` is the composition: `BridgeServer` (the pipe, one
  session at a time, current user only) -> `BridgeSession` (one `JsonRpc`, one
  event consumer) -> `BridgeService` (the guard: hello first, capability gate,
  start-up gate, faults to wire errors). It never touches the main window; what
  it needs arrives as a delegate. The application creates it in
  `MainWindowViewModel.StartExternalControl` right after the extension host.
- Every method and event of the contract has exactly ONE `[Capability]`; the
  `hello` answer is generated from those attributes (`BridgeCatalog`), so a
  capability cannot be advertised without a method and a method cannot be
  called without its capability. A new request field ships only with a new
  capability a client has seen. A guarantee (`execution.journal`) has no method
  and is listed in `BridgeCapabilities.Guarantees`.
- **The bridge never retries a card operation, and never repeats a request.**
  `startRun` is idempotent by the client's request id (`RunRegistry`,
  `RunSupervisor`: admission by compare-and-swap, the window's callback must win
  before it touches the host, the 5 s wait must win before it answers
  `uiUnresponsive`), answers when the run is admitted and not when it ends, and
  a write-ahead journal (`RunJournal`) lets a restarted RFiDGear still know
  which ids it admitted. An error carries `neverStarted: true` only when that is
  certain.
- Every result gets a number (`resultSeq`) in one sequence for the runs of
  clients and those of automatic mode (`RunRegistry` keeps the last 64 for
  `listRunResults`). Events are hints and never replayed: the truth is
  `getStatus`, `getRunResult` and `listRunResults`.
- Automatic mode is driven through the internal `IAutoModeControl` (an enum
  answer instead of an exception text, and the number of a start). What a client
  owns is a claim on one START of the mode (`AutoModeController.CurrentSession`,
  `StopIfSession`), never on "the mode", so a mode the user switched on is never
  stopped for a client.
- Card keys: the built-in secure key provider `bridge-client`
  (`BridgeKeyProvider`) is bound to the connected client that called
  `registerKeyProvider`; RFiDGear then calls `resolveKey` on the client inside
  the registry's limits. No client means `ProviderUnavailable` and the task ends
  `ERROR.KeyUnavailable`. No key, reference, UID or text of a client reaches a
  log (only the type of an exception), and the text forms of the two messages
  are fixed strings.
- Logs hold method names, request and run ids, numbers and error kinds. Never a
  variable, a path, a UID, a key or the text of an exception (`SanitizedException`
  is for the existing logs; the bridge logs only the type). Errors on the wire
  carry an `errorId` that matches the log.
- The tests: `BridgeTestClient` (a typed client on a real pipe that overrides
  `GetErrorDetailsDataType`), `FakeExtensionHost` (scriptable execution, project,
  reader and automatic mode), golden JSON (`BridgeSamples`, `BridgeGolden`) and
  the approved API snapshot (`RFiDGear.Bridge.Contracts.api.approved`,
  regenerated with `RFIDGEAR_UPDATE_API_SNAPSHOT=1` and the filter
  `BridgeContractsApiSurface`). A test through the main window view model must
  pass a temporary journal folder.

### Key Design Rule

**One-attempt rule**: Card-state-changing operations (write, authenticate,
format) must **not** auto-retry internally. The caller (task orchestrator) owns
retry logic. This prevents partial-state corruption when a card is removed
mid-operation.

Normalized error codes used across the stack: `AuthFailure`, `PermissionDenied`,
`ProtocolConstraint`, `TransportError`, `Unknown`. A task command adds
`KeyUnavailable`: a key slot is bound to a secure key provider and the key
could not be obtained, so the command made no card call. The execution service
does not route it like `AuthFailure`: a vault outage must not start the
fallback branches that a project wrote for a card that refused a key.

### Settings & Persistence

- **Settings**: XML-serialized `settings.xml` in `%LocalAppData%\RFiDGear\`.
- **Runtime defaults**: `runtime-defaults.json` — reader selection, language,
  COM ports, MIFARE default keys, auto-update behavior.
- **Project files**: Task definitions stored as versioned XML. A project is
  written straight into its archive (`DatabaseReaderWriter.WriteDatabase`);
  no plain copy of it, with its typed keys, stays in `%LocalAppData%`.

### Logging

Serilog with rolling file sink; logs written to `%LocalAppData%\RFiDGear\log\`,
30-day retention. Route all errors through Serilog — do **not** use Windows
EventLog (removed in commit 71ffab2).

### Extension/Plugin System

MEF enables extensions. `RFiDGear.Extensions.DesfirePluginSample` is the
reference plugin project. Extensions export ViewModels and task implementations
using the same `[ExportViewModel]` / MEF attributes as the host.

The extension contract is split in two assemblies. An extension references
only these, never `RFiDGear.exe`:

- `RFiDGear.Contracts` is the host service API (`IRFiDGearExtensionHost` and
  the project, execution, reader and culture services). It has no MEF
  dependency.
- `RFiDGear.ExtensionSdk` is the MEF-facing ABI: `[UiExtension]`,
  `HostPlacement`, `IUIExtension`, `IUIExtensionDetails`,
  `[SecureKeyProvider]`. It is versioned on its own (see its README).
  `RFiDGear.exe` forwards the old locations of the UI types so already-built
  extensions keep working.

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

A secure key provider (`ISecureKeyProvider` in `RFiDGear.Contracts`, exported
with `[SecureKeyProvider]` from the SDK) hands out card keys that a project
names by reference instead of containing them. `SecureKeyRegistry`
(`Services/SecureKeys`) is the only way to call one: it creates the providers
once, runs each call on a worker thread under a time limit, turns anything a
provider does wrong into a failed answer, logs only the type of a provider's
exception (never its message), and disposes a key that arrives late. A
resolved key lives only in a `SecureKeyLease` on the stack of the card
operation: never put one in a property or a static, and never log it.
RFiDGear brings one provider of its own, `windows-credential-manager`
(`WindowsCredentialKeyProvider`), which reads a key from a generic credential
below the prefix `RFiDGear/key/`; the registry holds it as a built-in, so no
extension can take its id. See `DEVELOPER_NOTES.md` for the format.

A task stores which slots are bound as `KeyBindings` (one entry per slot:
slot id, provider id, opaque reference; `SecureKeySlots` has the ids). The
embedded key of a bound slot is not written to the project, and a bound slot
never uses its embedded key, even when its binding is broken. A DESFire command
asks for all its keys first (`AcquireKeysAsync`, which uses `KeyAcquisition`):
an embedded key is read from the property, the key of a bound slot comes from
its provider through the ambient `KeyResolutionScope`, which the execution
service opens for each task attempt (a button of a task dialog finds the
default of the application). Whatever fails ends the command with
`ERROR.KeyUnavailable` and no card call. In a new command, do not read the key
properties on the way to the card; acquire the keys first, and do not log,
store or return the `AcquiredKeys`.

An extension that needs more than the contract (services, a database driver,
a PDF engine) keeps that code in a support library named
`RFiDGear.ExtensionLibraries.<Name>`; the first is
`RFiDGear.ExtensionLibraries.CardCheck`. **The name must never start with
`RFiDGear.Extensions`, in any letter case.** `MefHelper` scans the extensions
folder for `RFiDGear.Extensions*.dll` and hands every match to MEF, so a library
called `RFiDGear.ExtensionsNative.*` or `RFiDGear.ExtensionSupport.*` would be
loaded as an extension at every start-up. A support library is plain `net10.0`
(no WPF), references no other RFiDGear project, compiles against host-owned
packages (Serilog, GemBox.Pdf) without shipping a copy of them, and sits flat
next to the extension that uses it: .NET finds it there, the host needs no
change for that. `CardCheckCoreBoundaryTests` pin the rules, including the
name, against the host's own scan.

### UI Structure

Views are organized under `Views/` by task category: `CommonTask/`,
`GenericChipTask/`, `MifareClassicTask/`, `MifareDesfireTask/`,
`MifareUltralightTask/`. Dialog flows use the **MVVM Dialogs** library for
message boxes and file pickers.

Every box in which a card key is typed is wrapped by `KeySlotEditor`
(`Views/KeySlotEditor.xaml`), which shows that a slot names its key by a
reference to a secure key provider instead of holding it: the box is greyed and
shows the reference in the place of the key (the editor swaps the binding of the
box's `Text` and puts it back; the box keeps filling its cell, so it keeps its
height). The choice is made in the key manager
(`KeyManagerView`, `KeyManagerViewModel`), which the `KeyManagerButton` at the
bottom of each section of keys opens. The decisions are in
`KeySlotEditorState`, which the editor and the manager both show; the task view
models implement `IKeySlotHost`. A new key box needs the wrapper, a slot id, a
name for the slot and a `KeyManagerButton` in its section (see
`DEVELOPER_NOTES.md`). The key manager lists the stored keys to choose from
and creates a new key in a group of its own (never under a name in use, and
never while the list cannot be read); the key is entered in a `KeyEntryBox`
(hidden unless "Show key" is ticked, limited to its digits, green or red) that
is cleared after a key was tried; no property of its view models holds a key.

File > "Key manager..." (`StoredKeysView`, `StoredKeysViewModel`) needs no
project: it lists the keys under `RFiDGear/key/` by name only
(`ICredentialStore.Enumerate`, `WindowsCredentialKeyProvider.List`; no secret is
read to show a list, and the ones the vault hands out with it are erased),
stores a key through `KeyStorage.Store` and removes one, asking before it
replaces or removes a key, and storing nothing while the list cannot be read.

File > "Move keys of a project..." (`KeyMigration`, `KeyMigrationFlow`) moves
the typed keys of an existing project into the Credential Manager and writes a
new project; the old one is only read. It runs on a `ProjectManager` of its own
because creating a `DatabaseReaderWriter` deletes files in the root of its
project manager. A task that has typed keys
implements `IKeyMigrationHost` as well (see `DEVELOPER_NOTES.md`).

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
