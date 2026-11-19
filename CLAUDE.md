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

A release (the four MSIs, the bundle, `update.zip` and `update.xml`) is made with
`RFiDGearBundleSetup/Build-Release.ps1`, `Sign-Release.ps1` and `Pack-Release.ps1`
in that order, from a clean checkout of the merged commit; the version is
written by hand in `RFiDGear.csproj`, `Bundle.wxs` and `update.xml`, and
`ReleaseManifestTests` stops a release in which they differ.
`RFiDGearBundleSetup/Make-Release.bat` runs the three and asks for the
certificate and its password. See `DEVELOPER_NOTES.md` (Making a release).

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

With the PC/SC provider the MIFARE SAM AV3 sits in a reader of its own (the
contact slot of a pair such as the OMNIKEY 5422, the card in the other slot).
Its name is the setting `SamReaderName` (Reader setup, PC/SC only; empty for
none; no automatic search). With it set, the PC/SC provider never takes that
reader for the card and never falls back to the library's default unit, which
is the first reader and so the contact slot (`PcscReaderChoice`). With the
Elatec provider the SAM sits in a SAM socket of the same TWN4 that the card is
on (hardware revision G only), is found by its ATR in either socket, and needs
no setting.

The Elatec provider searches with `ElatecNetProvider.SearchedHfTagTypes`: every
high-frequency tag type but HID iCLASS. A TWN4 with iCLASS in the mask answers
SearchTag with "no tag" for a SmartMX that presents itself as DESFire (SAK
`20`, GetVersion type `81`), although the card is activated, so RFiDGear saw no
card at all; never set `HFTagTypes.AllHFTags` in that provider. The facts are in
`DEVELOPER_NOTES.md` (The TWN4 search mask).

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

### Ultralight / NTAG

`MifareUltralightSetupViewModel` is the task kind for the chips of the Type 2
family with 4-byte pages: MIFARE Ultralight, Ultralight C, Ultralight EV1
(MF0UL11, MF0UL21) and NTAG213, NTAG215, NTAG216. It reads and writes pages.
Lock pages, counters, signatures and changing a key or a password are not
tasks.

- `ReaderDevice` has three virtual operations (`ReadMifareUltralightPagesAsync`,
  `WriteMifareUltralightPagesAsync`, `AuthenticateMifareUltralightAsync`; the
  default answer is `ProtocolConstraint`) and `SupportsUltralightAccess`, which
  a task asks before it asks a key provider for anything. A call carries an
  `UltralightAccess` (the chip the task expects, how it authenticates, the
  opt-in for protected pages) and answers an `UltralightResult`.
- The layout of each chip is a table (`UltralightMemoryMap`, every row pinned
  by a test). A chip that is not in it is refused, never guessed at. Pages 0 to
  3 and the lock, configuration, password and key pages are written only when
  the task opts in (`AllowProtectedPages`) **and** names the chip. That is
  checked before a key is asked for, and again by the reader.
- The Elatec provider reads and writes every chip of the family. The PC/SC
  provider (LibLogicalAccess) knows only the plain Ultralight and the
  Ultralight C: an EV1 or an NTAG answers `ProtocolConstraint` there, and
  nothing is sent to the card.
- Keys use the secure key slots above: the key of an Ultralight C is one slot
  (`TripleDes2K`, 16 bytes), the password (4 bytes) and the PACK (2 bytes) of an
  EV1 or an NTAG are two. The byte order of the C key is not verified on a card
  and no key is offered as a default. The bridge has no wire name for a
  password or a PACK, so a slot bound to `bridge-client` ends `KeyUnavailable`.
  Never send the factory password `FFFFFFFF` of an NTAG to a card: a failed
  try counts against the limit of tries.
- One operation runs at a time per task (`IsRunning`), and while it runs the
  dialog cannot be closed. The grid of pages in the dialog is a view of the
  task (`PageRows`, no part of the project); while a row is not a whole page
  the data keeps the rows apart by a mark that no run accepts.
- Hardware cases and the checklist: `RFiDGear.HIL.Tests/ULTRALIGHT.md`. The
  reader layers were probed with an NTAG215 on an Elatec reader (reads, writes
  and a refused password) and a PC/SC reader (which does not know the chip);
  the task, the dialog and the hardware cases have not run on a card, and no
  plain Ultralight, Ultralight C or EV1 card has been tried at all.
  `DEVELOPER_NOTES.md` has the facts that bite.

### MIFARE Classic

The read, write and MAD calls of both readers run on one layer,
`Infrastructure/Classic`, and on four primitives per reader (`IClassicPort`:
log in, read a block, write a block, wake the card). `ReaderDevice` holds the
calls (`ReadMifareClassicSectorAsync`, `ReadMifareClassicSingleSector`,
`WriteMifareClassicSingleSector`, `WriteMifareClassicSingleBlock`,
`ReadMifareClassicWithMAD`, `WriteMifareClassicWithMAD`); a provider only
overrides `OpenClassicPortAsync` and `ClassicGate` (the PC/SC session gate, the
COM port lock of the TWN4). `ElatecClassicPort` and `LlaClassicPort` hold no
logic; the logic is in `ClassicOperations` and `ClassicMadOperations`, which
are tested against `SimulatedClassicChip` (tests), a chip in memory that
enforces the access bits and halts the card after a refused access.

- Pure types, each pinned by tests: `ClassicLayout` (sectors 0 to 31 have 4
  blocks, 32 to 39 have 16), `ClassicAccessBits` (the condition of a group is
  `C1 + 2 * C2 + 4 * C3`; the table of what a key may do; the three bytes with
  their inverses), `ClassicMad` (CRC8, AIDs little-endian, MAD1 and MAD2, the
  general purpose byte `ClassicMadGpb`), `ClassicSectorKeys` (an erased buffer;
  a key that is no key never reaches a card).
- **Key B is data while a key may read it** (the transport configuration `001`,
  `FF078069`): it cannot log in, whatever the reader says. PC/SC readers answer
  a login with yes and the chip judges at the next access, so a login is proved
  by reading the trailer; a refused access halts the card, so the next command
  fails once unless the card was woken (a throwaway login on PC/SC, a search for
  the tag on the TWN4). The wake belongs after a refusal, never between two good
  sessions and never as a retry of a write.
- The TWN4 numbers the sector of a login by the first block of the sector
  divided by four (`ElatecClassicPort.SectorNumberOf`).
- **One attempt** (the key rule): every write is one attempt, read back, never
  repeated. The first block that fails ends the call. A write that the chip
  refuses is looked at once (is the card still there?) and ends as
  `AuthFailure` or `TransportError`; a block that does not read back as written
  is `Unknown`.
- `ClassicWritePolicy` decides before a key is asked for: block 0 and a trailer
  are written only on an opt-in (`AllowManufacturerBlock`, `AllowTrailer`), the
  access bits of a trailer must be consistent, and access bits that can never
  be changed again need a second opt-in (`AllowFinalAccessBits`). The sector
  write is data blocks only (32 bytes for sector 0, 48 for a sector of 4 blocks,
  240 for 16). `WriteMifareClassicSingleBlock` may write a trailer (the dialog
  does) and never block 0.
- **The MAD write** puts the data first, then the directory, then the trailers,
  and never a directory entry before the data. A chip without a MAD gets one
  (MAD2 where sector 16 exists) because the call asks for it; the sectors that
  the data reaches get the new keys and access bits, and the others of the
  application stay as they are. Refusals (an AID of the administration, a sector
  of another application, no room, a missing key, final access bits) happen
  before the first block. Sector 0 and sector 16 hold no application.
- The legacy `Sector` property of the reader is shared by every call. A caller
  that reads it after an `await` may get the sector of another read: use
  `ReadMifareClassicSectorAsync`, which hands the sector back with the answer.
  The property is set under the gate, for the old call only.
- The dialogs take the access bits through `ClassicAccessBits`
  (`MifareClassicSectorModel`: the text of the trailer, `SAB`, all derived from
  the conditions). The status text says which answer failed
  (`ClassicFailureText`: only `AuthFailure` is a refused key) and the task ends
  with that answer.
- Hardware cases and the checklist: `RFiDGear.HIL.Tests/CLASSIC.md`. The PC/SC
  reader was probed with a 4K (reads, the reselect, a write experiment with the
  old code); the layer, the writes, the MAD and the dialogs have run against
  the chip in memory only, and the TWN4 has not been tried on a card.

### MIFARE SAM AV3 setup

Tools > "MIFARE SAM AV3 setup..." (`SamSetupView`, `SamSetupViewModel`) sets up
a MIFARE SAM AV3 that sits in a PC/SC reader or in a SAM socket of a connected
TWN4 (issue 347). It needs no project. It finds the SAM, activates a new one,
authenticates as host and writes one key into a key entry. Using the key in a
SAM is not part of it. The window lists the SAMs of both kinds of reader
(`ISamFinder`: `SamReaderFinder` for PC/SC, `Twn4SamFinder` for the sockets,
`CompositeSamFinder` for both, made by `SamFinders.Create`); the sockets only
while the Elatec reader is the one in use and connected. A TWN4 sees a SAM AV3
only from hardware revision G
(TWN4 MultiTech 2, two SAM sockets); the revisions D (TWN4 Legic 45) and E
(TWN4 MultiTech) never do, which was the cause of Elatec.NET issue 28 (Elatec
support, 2026-11-17). The task commands use the SAM on a revision G (see the
relay below); the record is `RFiDGear.HIL.Tests/SAM-AV3-FIRST-CONTACT.md`.

- The code is `Services/Sam`. `SamReaderFinder` looks at the ATR of the card in
  each PC/SC reader (historical bytes `MIFARE SAM AV3`) **without connecting to
  any card**; only a card that is a SAM is connected to (`Open`). The finder of
  a TWN4 takes **no** `SamPortGate`, unlike `Twn4SamConnector` of the relay: the
  window holds that gate already, and a second `TryEnter` would answer busy.
  `LlaSamReaderSource` is the one class that touches LibLogicalAccess, and it
  holds no logic. `LlaSamApduChannel` is the `ISamApduChannel` that Elatec.NET's
  `SamAv3HostSession` needs. It sits on the **data transport** of
  LibLogicalAccess, not on the reader card adapter: the adapter throws for every
  status word but `9000`, and the host protocol needs `90AF`, and `6985` tells a
  new SAM. A reader that fails is a `SamReaderException`; a SAM that says no is
  a plain `SamException` with a status word.
- `SamSetupService` has the steps: connect (SAM_GetVersion), authenticate (key
  entry 0, version 0), activate (SAM_LockUnlock), write a key entry (a PICC
  key into 1 to 127, version 0: AES-128 with Keep IV, SET `0x0024`, or a DESFire
  native DES/2K3DES key, SET `0x0000`; Elatec.NET writes AES-128 only, so the
  native entry is built by `SamKeyEntryFrame` and sent through `ExecuteSecureAsync`).
  Each is one attempt and never repeated.
  **The activation worked on the one real SAM AV3 it was tried on (2026-11-15:
  `90AF`, `90AF`, `9000`, and the SAM stayed activated after it was taken out
  and put back). No way back is known, so it is treated as irreversible.** It
  asks first (`ISamSetupDialogs.ConfirmActivate`, No is the default) and sends
  nothing without a yes. A step that changes a real SAM (the activation, a key
  write) runs only after the person has said go for that step, in a test, a
  script or a window.
- A key is the factory key (16 zero bytes), a stored key (the Credential
  Manager, slots `sam.master` and `sam.newkey`) or typed into a `KeyEntryBox`.
  It lives in a pinned buffer (`SamKeyBuffer`) for one step and is erased after
  it. No log line of RFiDGear holds a key, a reference, a reader name, the
  serial number of the SAM or the text of an exception. SAM_GetVersion gives the
  serial number as well: `SamDetection` keeps the first 14 bytes (hardware and
  software information) and drops the rest. The own trace of LibLogicalAccess
  (`config.log.enabled` with `seecommunication`) would write the whole answer;
  leave it off while a SAM is in a reader.
- The window holds `MainWindowViewModel.ReaderGate` and sets `IsReaderBusy`
  while it is open (`SamSetupGate`): a host session lives on the connection, and
  any other connect to that reader could reset the SAM. If somebody else holds
  the gate, the window does not open.
- After a key entry was written the setup reads it back (`SAM_GetKeyEntry`,
  `SamKeyEntryInfo`): a blank entry is disabled (SET bit 9), so the entry must
  read back enabled, AES-128 and of the PICC key class (ExtSET), or the key in
  it cannot be used. The key itself cannot be read back.
- `SamPiccSession` authenticates a DESFire card, and changes its keys, with
  keys that stay in the SAM (`SAM_AuthenticatePICC` INS `0A`,
  `SAM_ChangeKeyPICC` INS `C4`): the PC relays between the SAM in the contact
  slot and the card in the contactless slot, one attempt per step.
  `SAM_KillAuthentication` (`80 CA 01 00`, **no Le**) comes before the first
  step and after a part 1 that was not finished; a card that answers `91CA`
  to the select gets the select once more (it changes nothing).
  `SamKeyReference` (`entry:version`, provider id `sam`, entries 1 to 127)
  names such a key and holds no secret; `SamPiccConnector` opens the SAM for
  one operation under `SamPortGate`, which the setup window takes as well.
  Both providers relay through `SamRelay.RunAsync` (the gate of the reader's
  session is held, the SAM is opened first and the card second, both are let
  go of, an exception ends as `TransportError`) and three virtual operations
  of `ReaderDevice` (`SupportsDesfireSamKeys`,
  `AuthenticateMifareDesfireWithSamAsync`,
  `ChangeMifareDesfireKeyWithSamAsync`). The PC/SC reader does it in
  `LibLogicalAccessProvider.Sam.cs` (the SAM in the reader that the setting
  names). The TWN4 does it in `ElatecNetProvider.Sam.cs` with the classes
  `Twn4*` of `Services/Sam`: `Twn4SamConnector` looks at both SAM sockets,
  takes the card whose ATR says `MIFARE SAM AV3` (two of them are
  `SamAmbiguous`, none is `SamNotFound`), sets the socket up for T=1 from the
  ATR (`Twn4AtrReader`, no PPS) and powers it down when it is let go of; the
  card on the antenna is searched once, after the SAM is open
  (`Twn4CardSide`: the SAK and the first frame of GetVersion), and gets the
  frames of the PC/SC relay as they are; the whole relay holds the lock of the
  COM port. `ITwn4SamRelayPorts` hides the reader (`ElatecTwn4Ports` is the
  thin class over Elatec.NET). A reader that knows nothing of the SAM keeps
  the defaults of `ReaderDevice` (no support, `ProtocolConstraint`), and a task
  on it ends `KeyUnavailable` before anything is sent. Four commands of the
  DESFire task call them
  (`MifareDesfireSetupViewModel.Sam.cs`): the task "Check: App Key", "Change
  PICC Master Key" and "Change Application Key", and the button that
  authenticates the PICC master key; every other command and every check task
  refuses a key of the SAM. What the SAM and the card did:
  `RFiDGear.HIL.Tests/SAM-PICC-FIRST-CONTACT.md`; the checklist of the
  commands, not run on a card yet (on a TWN4 the provider ran the cases 1 to
  6, the PICC master key and the application keys, but nothing was run
  through the task window): `RFiDGear.HIL.Tests/SAM-PICC.md`.
- Hardware: `RFiDGear.HIL.Tests/SAM-AV3.md` (a read-only case, a case that
  writes a key entry, and the checklist for the window).
  `RFiDGear.HIL.Tests/SAM-AV3-FIRST-CONTACT.md` is the record of the first
  session with a real SAM: every APDU and answer (UID masked), what was found
  (the activation, the version byte that changed, the key entry that reads
  back) and what is still open. The window itself has not been clicked with a
  real SAM.

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
  capability a client has seen. A guarantee (`execution.journal`,
  `project.revision`) has no method and is listed in
  `BridgeCapabilities.Guarantees`.
- **Project revision** (`project.revision`): `loadProject` and `getStatus` give
  a `revision` of the loaded project, `startRun` takes `expectProjectRevision`
  and refuses (`projectMismatch`, never started) a project in another state,
  and the result carries `projectRevision`. It is a keyed hash
  (`ProjectRevision`, key made at process start, never written) of the text a
  save would write (`XmlSerializer` of `ChipTaskHandlerModel`), so unsaved
  changes count, and a typed key in a task is not recoverable from it; it is
  valid for one process only. What a run writes into the tasks is left out:
  results are `[XmlIgnore]`, and `ProjectRevision` removes the elements that
  a read fills (`ChildNodeViewModelFromChip` always, `ChildNodeViewModelTemp`
  unless the task is `WriteData` that does not refresh from a file) and the
  focus (`IsFocused`); tests run a DESFire read and write and a real loop to
  prove it, so a serialized member that a task type starts to write needs
  the same treatment. The project host gives it through the internal
  `IProjectRevisionSource` (a host without it does not offer the capability
  and refuses a run that expects a revision); `LoadWithRevisionAsync` takes it
  inside the commit of a load. It is read on the main window's thread.
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
discovered once at start-up from `%ProgramData%\RFiDGear\Extensions` and from
the user's own folder of installed packages (below), so adding one needs an app
restart.

An extension is released on its own and ships an update as a **package**
(`.rfext`, a zip with an `extension.json` of the id, the version, the oldest
RFiDGear and the SHA-256 of each file), which a person installs with File >
"Install extension update..." for their own user: no administrator and no
folder to browse to, because `%ProgramData%\RFiDGear\Extensions` is not
writable for a normal user and a loaded DLL is locked.
`Infrastructure/ExtensionPackages` has the reader (limits, flat plain names of
dlls and pdbs, none of the host's assemblies, exactly the listed files, every
hash), the installer (stage, check the file version, ask, then one rename to
`%LocalAppData%\RFiDGear\InstalledExtensions\<id>\<version>`, never over an
existing folder) and the selector: `MefHelper` loads each extension **once**,
the higher file version of the machine-wide and the user's copy (the
machine-wide one when equal), and the resolver asks `FindExtensionAssembly` so
the older copy is not loaded by name. A provider makes a package with
`-p:PackExtension=true` (`RFiDGear.Extensions/PackExtension.targets`); nothing in
a package is executed. The hash is integrity, not authenticity: the signer and
whether Windows finds the signature valid are shown (`WinVerifyTrust`), not
enforced. See `DEVELOPER_NOTES.md` ("Shipping an extension update as
a package"). An extension shows its own version in its UI (the Card Check
Assistant reads it from its file version, set once in `CardCheckVersion.props`).

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

A key of a MIFARE SAM AV3 is a third source for a slot: the binding names the
provider id `sam` and a reference `entry:version` (`SamKeyReference`, entries 1
to 127). It is no key and has no bytes, so `SamKeyProvider` (built in, which
reserves the id and gets it listed) answers `Unsupported` to everything, and
`KeyAcquisition` never asks the registry for it. A command that does not know
the SAM gets `SamKeyPolicy.Refuse`, the default, and ends with
`ERROR.KeyUnavailable` and no card call, whatever the kind of task, when it
uses that slot (a slot that a command does not use is not asked for, from a
provider or from the SAM). A command
that can use one passes `AllOrNothing` and reads `AcquiredKeys.SamKey(slot)`;
all its slots must be keys of the SAM, because the SAM builds the cryptogram of
a key change from the old key and the new one. `AcquiredKeys.Hex` throws for
such a slot. The key manager lists the SAM only for the slots in
`SamKeySlots.Offered`: the keys of the authentication and of the key changes
of the PICC and of an application, which four commands use.

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

What such a library needs beside it that the host does not have is the
extension's **payload**: for `CardCheck` the SQL driver, the real
`System.Data.SqlClient.dll` and the `sni.dll` of the platform. The library names
the files (`SqlDriverPayload.targets`, target `GetSqlDriverPayload`), the
extension build copies them into its output folder and lists them as
`ExtensionPayloadFile` items, and from there the deploy step and the installers
take them along; an installer build stops without them. A new payload file goes
through the same three places, and `SqlDriverPayloadTests` load it in a process
of its own, because the test project's own `deps.json` hides a missing file.

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
| Elatec.NET 0.7.0 | Elatec TWN4 reader SDK |
| LibLogicalAccessNetCE 3.7.0 | PC/SC reader provider |
| GemBox.Pdf 2025.x | PDF form population for reports |
| Serilog 4.3 | Structured logging |
| Portable.BouncyCastle 1.9 | Cryptography (MIFARE key derivation) |
| Newtonsoft.Json 13.0.4 | JSON serialization |
| Microsoft.Extensions.* 10.x | Logging abstractions, options |
