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
store, the workflow session, the import of the old assistant's settings, the
PDF report service, the access to the order table and the guard tests. The
rest follows one change at a time, each with its own tests.

**Order table** (`IOrderStore`, created by
`CardCheckServices.CreateOrderStore`):

- It reads the order table of the external order system and writes exactly
  three things: the report of a check (as base64 text), the status
  `CheckFinished` and the date of the change. The table and column names
  (`[CC-ID]`, `[CC-ReportBase64]` and so on) are the system's own. SQL Server
  only, with the Windows account of the person running the check: there is no
  user name, password or port. SQLite is used by the tests alone, to run the
  same statements on a real engine.
- `FinalizeAsync` is one transaction: the order has to exist (asked with a
  query of its own, because the row count of an `UPDATE` is not reliable on a
  table with triggers), then the report, the status and the date are written.
  If any step fails nothing is kept, so an order is never marked finished
  without its report. The transaction is serializable, so the existence check
  still holds when the updates run: at the default level the order system
  could delete the row in between and the updates would succeed on nothing.
  On SQL Server the check also takes an update lock (`UPDLOCK, HOLDLOCK`):
  with only a shared lock that is later upgraded, a writer of the order system
  and this transaction can deadlock, and one of them is chosen as the victim.
  An empty report is refused (it would read back as "no report" on a finished
  order). The old assistant wrote the three in separate calls and went on
  whatever happened. One thing no client can rule out: if the connection is
  lost while the final commit is on its way, the server may have committed
  anyway. Every write sets the same values again, so writing again is safe.
- Values are parameters, never part of the SQL text. The table name from the
  settings is read as up to three parts (bare words or `[bracketed]`) and
  written out with every part in brackets; anything else, such as `;` or a
  space outside brackets, is refused. `SqlIdentifier` and `OrderSql` are the
  only places that compose SQL text.
- The connection is the old assistant's: integrated security, the server's
  certificate trusted, two retries ten seconds apart, a thirty second connect
  timeout, and the instance name as the application name, but only when the
  server name has an instance (the old code failed without one). Encryption is
  left at the driver's default, as before; the packet size is no longer set.
- Every call opens its own connection and returns a `Result`: a timeout, a
  refused login, an unreachable server, a missing table or column, an unknown
  order and a missing database driver (`System.Data.SqlClient.dll` or its
  native file is not beside the extension) are different failures with fixed
  text that never names the server, database, table or path. Nothing that
  handles a failure needs the driver itself, so a missing driver is reported
  instead of thrown. The old assistant wrote errors to the Windows event log
  and carried on.
- `DownloadReportAsync` gives the bytes of the stored report; an order with no
  report (missing, `NULL` or empty) is "not found", never an empty file, and
  text that is not base64 is "corrupt". Columns are read as text whatever type
  they have; a missing status reads as `NA`.
- The report is sent as `nvarchar(max)` text where the old code sent `ntext`;
  both convert to the column. The date of a change is written as text in the
  current culture, as before, until the probe of the real table says otherwise.
  The suite runs against SQLite, not against a SQL Server.

**Stub orders for the test mode** (`IOrderStore`, created by
`CardCheckServices.CreateStubOrderStore`):

- A stand-in for the order table that keeps five made-up orders in memory, so
  the assistant can be tried with only a reader and a card when there is no
  database or no order. It reads and writes no database and no file. The
  orders and every change made to them last as long as the store, and each
  window creates its own.
- It answers as the store on the table does: the same order of checks (a
  missing report throws, an empty one is refused, then a blank id, a cancelled
  call and an unknown order), the same failure codes and texts (both take them
  from `OrderFailures`), the list newest first, `FinalizeAsync` setting the
  report, the status `CheckFinished` and the date of the change together.
- The orders say what they are: ids and job numbers start with `STUB-`, the
  customer, editor, dealer and sales names are made up, and the numbers are
  plain enough to become folder and file names. They stand for five stages an
  order goes through; one of them is finished and holds a placeholder text
  instead of a real PDF, so analysing or re-editing its report fails. An order
  finished during the session with a real PDF works like any other.

**Import of the old settings** (`ILegacySettingsImporter`, created by
`CardCheckServices.CreateLegacyImporter`). It is built and tested but **not used
by the extension for now**: since Slice 3.5 the settings are built from scratch
and no backward compatibility is wanted (the extension has one switch for it):

- `OfferAsync` is the whole conversation, once: it looks for the old
  `settings.xml` (the old assistant's own folder in the application data, and
  the private folder of a packaged install), asks the operator through
  `IImportPrompt` which file to use, shows what would be imported and asks for
  confirmation, then imports. The library shows nothing itself; the extension
  implements the two questions with its own dialogs. "Not now" asks again next
  time, "never" is remembered, and an import or a refusal is never asked
  again. `Discover`, `Preview` and `Apply` are the same steps for a menu entry.
- The old files are only read: opened for reading and shared with the old
  assistant if it is still running, never changed, moved or deleted. The
  preview changes neither that file nor the settings (a damaged settings file
  is moved aside, as with every load). `Apply` checks the SHA-256 of the file
  against the preview, so a file that changed in between is refused.
- A user name, a password and the port are not imported: the reader skips
  those elements while it reads, the preview only says that they were not
  read, and the library's copy of the file is cleared once it is parsed (the
  XML reader's own buffers are not under its control). The file is not
  trusted: a document type declaration is
  refused, nothing external is resolved, a file over a megabyte is refused, and
  it does not use the old assistant's own reader.
- Settings that no longer exist (theme, updates, the reader volume, the path of
  the old program, auto-run, the language of the user interface) are listed as
  not used. An SQLite configuration is not imported (this version has no SQLite)
  and the operator is told. The placeholder text block `N/A` is left out.
- Nothing the operator entered in this version is overwritten: a setting is
  filled in only if it is still at its default, and text blocks are added by
  name. The settings are merged into what is saved at the moment of applying,
  and a marker with the file's hash is saved with them. Reading, merging and
  writing are one step (`ISettingsStore.Update`), so a change made meanwhile by
  another window of the extension is merged with, not written over.

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
- `Update(change)` is the way to change settings that depend on the current
  ones: it reads them, calls `change` and writes the result as one step, while
  it holds a named system mutex made from the file's path. Every store for the
  file (another window, another program) waits for it, so no change is lost to
  a copy that went stale in between. Nothing is written if the file is from a
  newer version or cannot be read just now, or if `change` fails or returns
  its argument unchanged. A holder that does not let go for ten seconds makes
  the call fail with `settings.busy`; a system that will not make a mutex
  leaves only the lock inside one store, as before. `Load` and `Save` take the
  same mutex.
- `Load` and `Save` never throw because of the file. A save writes a temporary
  file in the same folder, flushes it to the disk and swaps it in, so a full
  disk, a crash or a power loss leaves the old file whole. A file that someone
  edited with a tool that adds a UTF-8 byte order mark is read like any other
  (it is not set aside as damaged). Values are tidied (missing becomes
  empty, blanks around names and paths go) and an unknown language falls
  back to German instead of discarding the file. The log gets status words
  only, never a value.

**Workflow session** (`IWorkflowSession`, created by
`CardCheckServices.CreateWorkflowSession`):

- The state of one check in one window: the order, the language, what the
  first report said (`ReportAnalysis`), what the template rules make of it
  (`TemplateSelection`) and the chosen template. It replaces the old
  assistant's static `CurrentCardCheckProcess`; nothing is static or shared,
  and each window creates its own.
- A snapshot never changes. Every successful change swaps in a new one and
  raises `Changed` after the lock is released, so a handler may read or change
  the session again. Events come one at a time in the order of the versions:
  a change made by a handler, or by another thread while events are being
  raised, is queued and raised afterwards by the caller that is already
  raising, so that call may return before its event has been delivered. A
  handler that throws does not undo the change or stop the other handlers; the
  fault is logged by type, never by message.
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
  file that cannot be inspected is a failure, never a silent success. Both
  `PrepareStep` and `Cleanup` delete only a file directly in the order's
  folder: the paths record can be copied or built by hand, so anything else
  is refused (`Cleanup` lists it as failed) and the file is kept.
- `Cleanup` removes working reports and never throws because of a file. Not
  supported: steps 1 and 2 if the setting is on. Supported only: nothing.
  Programmed: steps 1, 2 and 3 if the setting is on (the old assistant forgot
  step 1). Re-edit: the downloaded report and its copy, always. The final
  report is never removed. A failure is logged by kind, not by path.

**PDF reports** (`IReportService`, created by
`CardCheckServices.CreateReportService`):

- `Analyze` reads the five fields the first report carries: supported,
  programmable, free memory, chip type and notes, in one load. A check box
  counts as ticked only if its value is exactly `Yes`, as in the old
  assistant. A field the report lacks reads as empty; a file with no form at
  all is refused as not being a report.
- `WriteFields` and `SetFieldsReadOnly` never change their source. They load
  the report once, change it, save it under a short temporary name of its own
  beside the target (not the target's name plus a suffix, which could be too
  long) and swap it in, so a crash leaves the old target or the new one, never
  half of a file. A target name with a character the file system never allows
  is refused before anything is read. `WriteFields` is all or nothing: if a
  named field is missing, nothing is written and the failure names the missing
  fields (the report's own fixed names, never user data). A field that is
  written is left editable.
- A missing file (or a folder in its place), an empty or damaged file, a file
  another program holds, a report in a folder this account may not read and a
  folder that is not there each give a failure of their own kind, never an
  exception: a file that cannot be inspected is an access failure, not a
  missing file. The old assistant took a damaged report for an empty one and
  went on to finish the order. Field values are never logged and never part of
  a failure. The component runs in its free mode, with the same key the host
  uses; a report over its limits is an `InvalidConfiguration` failure.
- The component's save picks the format from the file extension, so the
  temporary file is saved with the format named explicitly. Its exceptions
  have no usable namespace and are recognised by assembly.

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
  failure would appear when the class is created or used. The extension makes
  the first use of the library inside a try block when a window opens
  (`ComponentCheck`) and shows a "component missing" page if that fails.
- By hand, for a trial: build the extension (Release, x64 or x86 to match
  RFiDGear) and copy `RFiDGear.Extensions.CardCheckAssistant.dll` and
  `RFiDGear.ExtensionLibraries.CardCheck.dll` from its build folder. Nothing
  in the extension opens the order database yet, so the database driver is not
  needed for that. When it is: the real `System.Data.SqlClient.dll` is the one
  under `runtimes\win\lib` of the package (the one under `lib` is a stub), and
  `sni.dll` has the same name for x86 and x64, so a flat folder holds only the
  one that matches RFiDGear. The installer step derives this from the resolved
  assets instead of listing files by hand.

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
