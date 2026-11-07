---
title: RFiDGear.ExtensionLibraries.CardCheck
---

RFiDGear.ExtensionLibraries.CardCheck is the support library of the Card Check
Assistant extension. It holds everything the assistant does that is not user
interface: reading and finalizing orders in the order database, preparing and
analysing the PDF reports, choosing the report template, keeping the settings
and importing the settings of the old assistant. The WPF extension
(`RFiDGear.Extensions.CardCheckAssistant`) uses it; nothing else does.

So far it holds the result types, the log abstraction, the status and
language values, the template rules, the report file handling, the settings
store, the workflow session and the guard tests. The other services follow one
at a time, each with its own tests.

**Settings** (`ISettingsStore`, created by
`CardCheckServices.CreateSettingsStore`):

- One JSON file, `settings.json`, in
  `%LocalAppData%\RFiDGear\Extensions\CardCheckAssistant`: the assistant's own
  folder, apart from the host's files. It holds the server, database and
  table of the order database, the report options, the project paths, the
  folders last used, the text blocks for report notes and a marker that the
  old assistant's settings were imported. There is no user name, password or
  port: the connection uses the Windows account of the operator.
- The file carries a `schemaVersion`. A file from a newer version is never
  overwritten: loading gives the defaults and saving is refused. A damaged
  file (not JSON, wrong shape, bigger than a megabyte) is renamed to
  `settings.json.corrupt-<time>` next to the original and the defaults are
  used, so nothing is lost. A file another program holds is "unreadable" and
  is not overwritten either.
- `Load` and `Save` never throw because of the file. A save writes a temporary
  file in the same folder and swaps it in, so a full disk or a crash leaves
  the old file whole. Values are tidied (missing becomes empty, blanks around
  names and paths go) and an unknown language falls back to German instead
  of discarding the file. The log gets status words only, never a value.

**Workflow session** (`IWorkflowSession`, created by
`CardCheckServices.CreateWorkflowSession`):

- The state of one check in one window: the order, the language, what the
  first report said (`ReportAnalysis`), what the template rules make of it
  (`TemplateSelection`) and the chosen template. It replaces the old
  assistant's static `CurrentCardCheckProcess`; nothing is static or shared,
  and each window creates its own.
- A snapshot never changes. Every successful change swaps in a new one and
  raises `Changed` after the lock is released, so a handler may read or change
  the session again. A handler that throws does not undo the change or stop
  the other handlers; the fault is logged by type, never by message.
- The rules keep the parts consistent: findings and selection come together,
  and the chosen template is always one of the available ones. A call that
  does not fit the state (findings before an order, a template that does not
  fit the chip) returns a failure and changes nothing.

**Report files** (`IReportFiles`, created by
`CardCheckServices.CreateReportFiles`):

- `Resolve` is the only place a report path is built. It gives the paths
  `<root>\[<job>\]<job>-<chip>` plus `.step1.pdf`, `.step2.pdf`, `.step3.pdf`,
  `_final.pdf`, and `.download.pdf` and `.edit.pdf` for a report fetched for
  editing. For ordinary numbers the names are exactly those of the old
  assistant. The old assistant kept every downloaded report under one fixed
  name, so one order could open the report of another; here they are per order.
- A job or chip number becomes one file name, and two different numbers never
  share one. A character that is not allowed becomes `_`, and `~` plus eight
  hex digits of the SHA-256 of the original are added: `A/B` and `A?B` stay
  different and `..\..` cannot leave the folder. `~` itself is not allowed, so
  a changed name cannot equal an unchanged one (a number typed as `A_B~1a2b3c4d`
  is changed too). The chip number may not contain the hyphen that joins it to
  the job number, so `A-B` with chip `C` and `A` with chip `B-C` have different
  files. A name that is empty, ends in a dot (so also `.` and `..`) or starts
  with a Windows device name (`NUL`, `COM1`, `COM¹`, `CONIN$` and the rest,
  with or without an extension) is refused. Two numbers that differ only in
  letter case name the same files on Windows; that is not handled.
- The root must be a full path with none of `? * < > " |` anywhere in it (a
  `?` would be replaced by the host as a numbering placeholder, also in a
  `\\?\` prefix) and no `:` or control character in a folder name. No path may
  be longer than 259 characters. Tests check every path against the
  extension's own report-target check.
- `EnsureOrderFolder` creates the order's folder and never the output folder.
  `PrepareStep` deletes a step report an earlier attempt left behind, before
  the run that writes it, so an old file is never read as the new result. Only
  "file not found" means there is nothing to delete: a folder in the way or a
  file that cannot be inspected is a failure, never a silent success.
- `Cleanup` removes working reports and never throws because of a file. Not
  supported: steps 1 and 2 if the setting is on. Supported only: nothing.
  Programmed: steps 1, 2 and 3 if the setting is on (the old assistant forgot
  step 1). Re-edit: the downloaded report and its copy, always. The final
  report is never removed. A failure is logged by kind, not by path.

**Template rules** (`TemplateRules`, `ProjectVariants`, `LsmTemplate`):

- The catalog of 13 report templates was generated from the old assistant's
  table by a script, not retyped. The placeholder row `N/A` is not a template
  and is left out. `MD10000L_AV` needs 3232 bytes, as its own text says; the
  old table said 1760 there, a copy of the row above. A test checks every size
  against the number in the template's own display text.
- `ParseFreeMemory` reads the free-memory field of the first report. A first
  word that is a whole number means a DESFire chip and the number is its free
  bytes; otherwise the comma-separated entries that contain a digit are the
  free sectors of a Classic chip. The text is trimmed. Empty, `N/A`, text
  without a digit and numbers too large for an `int` are unknown memory, which
  counts as not enough: the old assistant took such a field for a Classic chip
  with one sector and offered the smallest template. No culture is used.
- A DESFire chip needs at least 225 free bytes (the old gate; the smallest
  template needs 224), a Classic chip at least one free sector.
- `Classify` gives every combination a verdict. Not programmable is not
  supported, whatever the supported flag says. Programmable but not supported
  is indeterminate: the report contradicts itself and nothing is assumed.
  Supported and programmable is programmable with memory for a template and
  supported-only without.
- `AvailableTemplates` keeps the templates of the chip's family that need no
  more than the chip has. `DefaultTemplate` prefers `MD4000L_AV`, then
  `MC1000L_AV`, then the first.
- `ProjectVariants.Resolve` picks `name.deutsch.rfPrj` or `name.englisch.rfPrj`
  next to the default project if that file exists, else the default project.

**Rules that keep it loadable and safe:**

- The name is `RFiDGear.ExtensionLibraries.<Name>` and never starts with
  `RFiDGear.Extensions`. The host hands every `RFiDGear.Extensions*.dll` in
  the extensions folder to MEF, so a name like `RFiDGear.ExtensionsNative` or
  `RFiDGear.ExtensionSupport` would be loaded as an extension at every
  start-up.
- Plain `net8.0`: no WPF, no WinUI, no reader SDK, no reference to any
  RFiDGear project, no Windows event log, no console. It never starts a
  process.
- Serilog and GemBox.Pdf belong to the host. The library compiles against the
  same versions (`ExcludeAssets="runtime;native"`, `PrivateAssets="all"`), so
  the loaded copy is the host's and no private copy is built next to it.
- Only `Logging/SerilogCardCheckLog.cs` names Serilog. Everything else logs
  through `ICardCheckLog`, which takes no exception: a boundary turns an
  exception into a `Failure` and logs only the type, the HResult and the stack
  trace (`UnexpectedFault`), never the message or the data. Messages and
  failures never carry a server, user, connection string or report content.
- Operations return `Result` or `Result<T>` with a `Failure`; nothing is
  swallowed and nothing is thrown across the boundary on purpose.
- No mutable static state. Every window creates its own services.
- The public surface is interfaces, records and `CardCheckServices`. The
  implementations are internal and reach the tests through
  `InternalsVisibleTo`.
- `CardCheckCoreBoundaryTests` check all of this on the project file, the
  built assembly and the sources.

**Where it goes at run time:**

- Beside the extension, flat, in `%ProgramData%\RFiDGear\Extensions`. The
  host's own resolve hook only serves names that start with
  `RFiDGear.Extensions`, and it does not need to serve this one: .NET finds
  a managed library and its native files next to an assembly that was loaded
  with `Assembly.LoadFrom`, which is how MEF loads an extension.
- The exported class of the extension must not use library types in its
  members or its constructor. MEF's checks cannot see a missing library; the
  failure would appear when the class is created or used. When the extension
  uses the library (a later change), it is to check for it when its window
  opens and show a "component missing" page.

**What was measured before this library was built** (scratch harness with the
host's package versions and MEF 10.0.1, x64 and x86, every negative control
failed as it should):

- A library and `System.Data.SqlClient.dll` beside an extension that MEF
  loaded are found without any host change; the host's resolve hook is asked,
  returns nothing, and .NET's own `LoadFrom` resolver finds them. A subfolder
  is not searched.
- `sni.dll` beside `System.Data.SqlClient.dll` is found. Without it the first
  connection fails with a `DllNotFoundException`. The `netcoreapp2.1` build of
  `System.Data.SqlClient` is a stub that throws `PlatformNotSupported`; the
  real one comes from the runtime-specific publish assets. `sni.dll` has one
  file name for x86 and x64 and a different file for each.
- Exactly one copy of Serilog and one of GemBox.Pdf are loaded, both from the
  host folder. A line logged by the library reached the host's logger. A
  second `ComponentInfo.SetLicense` call is harmless.
- GemBox.Pdf 17.0.1300 (the old assistant) and 2025.12.101 (the host) read the
  same form fields the same way: a ticked check box reads as the text `Yes`,
  a missing field is a null field, writing `Off` reads back as nothing, and the
  read-only flag survives a save. Only the exact text `Yes` means ticked.
- A restore reports no warnings and no known vulnerable package. The payload
  of the library plus the SQL driver is three files per platform: the
  library, `System.Data.SqlClient.dll` and `sni.dll`.
- With the library missing, MEF still finds the part and the export; only
  creating or using the class fails.
