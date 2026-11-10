---
title: Card Check Assistant
---

The Card Check Assistant is an application extension for RFiDGear. It runs a
project once on the card that is on the reader, with a job number, a chip
number and an optional report file, and lists what every task did. It appears
in the Extensions menu of the main window.

**Install:**

- Copy `RFiDGear.Extensions.CardCheckAssistant.dll` and its support library
  `RFiDGear.ExtensionLibraries.CardCheck.dll`, and nothing else, to
  `%ProgramData%\RFiDGear\Extensions`. Both come out of the build folder of
  the extension, for the same platform (x64 or x86) as RFiDGear. The host
  already has the contract, the SDK, Serilog and GemBox.Pdf; a second copy of
  any of them next to the extension would load its types twice.
- Restart RFiDGear. Extensions are discovered once at start-up, so there is no
  hot loading.
- While developing, a build can do the copying: see `DeployToRFiDGear.targets`
  and `DEVELOPER_NOTES.md`. It is off unless the command line
  (`-p:DeployExtensionsToRFiDGear=true`), a git-ignored
  `RFiDGear.Extensions/deploy.local.props` or the environment variable
  `RFIDGEAR_DEPLOY_EXTENSIONS=true` turns it on.
- If the library file is missing, or is of another platform or version, the
  window opens with a short text that names the two files and the folder,
  instead of the page. The check runs each time a window opens, but a library
  that was loaded earlier in the same RFiDGear session stays loaded, so
  removing the file only shows up after RFiDGear is restarted.

**What the page does:**

- Takes the full path of a project (`.rfPrj`), chosen with the picker or
  typed. The host loads it, so it replaces the project open in RFiDGear.
- Sends the job number as `$JOBNUMBER`, the chip number as `$CHIPNUMBER` and
  the report file as `REPORTTARGETPATH`. A blank field leaves its variable
  out.
- Runs the project once, shows the outcome, the reason if the host gives one,
  and one line per task, with tasks the run never reached marked as such.
- Is disabled while RFiDGear runs tasks, whoever started them, and cannot be
  closed in the middle of its own run.
- Runs a project that ends with the program task `%EXIT` (the old assistant's
  way of closing RFiDGear) without closing RFiDGear: the host ends the run at
  that task as a normal finish, the tasks after it are shown as not reached
  and are not held against the outcome, and the page shows the result. The
  main window still closes RFiDGear on that task.
- Shows a **card line** under the reader line: the type and UID of the card on
  the reader (`Card: DESFireEV1, UID 04AABBCCDD`, followed by `(+1 more)` when
  an LF or LEGIC credential was read together with it), or why there is none:
  no card found, no reader, the reader is busy, the reader could not be read. A
  busy or failed look is never shown as "no card". The line is what RFiDGear
  last saw: it is read from the host when the window opens and again whenever
  the reader event arrives (a card arrived, was removed or was replaced, by a
  scan or by the Read button of the main window, or the reader changed). The
  **Read card** button next to it scans the reader once, now. It is off while
  tasks run (whoever started them) and while the page loads or runs, and the
  run button is off while the scan goes, because a load started meanwhile
  would be refused as busy by that scan. The page never scans on its own: no
  timer, nothing when the window opens. With a PC/SC reader a card that cannot
  be read, and an unplugged reader, also show as "no card found" (the host
  cannot tell them apart); a card that is not a MIFARE card is not listed.
- Is in English or German, following the language of RFiDGear, and switches
  when that changes.
- Remembers, in `%LocalAppData%\RFiDGear\Extensions\CardCheckAssistant`, the
  default project (prefilled when the window opens, never over text already
  typed) and the folder of the last project that was loaded (where the picker
  opens when no project is entered). The settings are read and written off the
  UI thread; if they cannot be, the page works without them.
- Has a **Settings...** button that opens a modal window with everything the
  assistant keeps: the folder reports are written to (one folder per job, remove
  working reports, default report language), the default project and the
  project that reads the memory of the card, the order database (server as
  `host` or `host\instance`, database, table, and a **Test connection** button
  that says in words what it found; the Windows account is used to log in), and
  the text blocks (add, rename, edit, remove). Save writes only the values that
  were changed, so a second window's changes to other values survive. A path
  that is relative or unusable, a text block without a name or with a name
  another one has, and a database with only part of its entries stop the save;
  a folder or project that does not exist is only a warning, because it may be
  a network share that is not reachable just now. Closing the window with
  changes asks first. A settings file of a newer version, or one that cannot be
  read, is shown as such and is never saved over. After a save the page reads
  its starting values again (a project that is already in the field stays).
- Has a **test mode** for trying a check with only a reader and a card, when
  there is no order database or no order in it. It is switched on with the
  check box in the Settings window ("Test mode: use stub jobs", saved, off by
  default). While it is on, the page shows a notice and a drop-down of five
  made-up jobs (job numbers `STUB-1001` to `STUB-1005`). Picking one fills in
  the job and the chip number and, where the report field is empty (or still
  holds the file the page proposed), proposes the report file in a `Test`
  folder: below the report folder of the settings if it is set and exists,
  otherwise in `%TEMP%\RFiDGear\CardCheckAssistant\Test`, with a folder for
  each job if the settings ask for one. The folder is made when a job is
  picked, and a proposal that arrives after a run was started is dropped. What
  the operator typed is never replaced. Switching the mode off takes back the
  numbers and the report file a picked job filled in, unless they were changed
  since. The jobs are kept in memory for as long as the window lives, and in
  this mode the order database is never read or written; **Test connection**
  in the Settings window still tests the real one. The drop-down is disabled
  while a run is going, like the other fields. The whole page is disabled for
  the moment the settings are read again after the Settings window was closed,
  so a run cannot start while a job's numbers are being taken back.
- Does **not** offer, for now, to take over the settings of the old WinUI Card
  Check Assistant. The code is kept (a question with "Import", "Not now" and
  "Never ask again", then a summary to confirm; the old files are only read,
  and the user name, password and port in them never are), but the settings
  are built from scratch and no backward compatibility is wanted. While it is
  off nothing is read from the old assistant's folder and no marker is
  written. `AssistantServicesFactory` has the one switch; Slice 6 decides
  whether to turn it on for the cutover or to remove the code.

**Rules that keep it correct:**

- It references `RFiDGear.Contracts` and `RFiDGear.ExtensionSdk`, both with
  `Private=false`, and its own library `RFiDGear.ExtensionLibraries.CardCheck`,
  which is copied beside it. A build error stops a private copy of a host
  assembly (the contract, the SDK, GemBox.Pdf, Serilog, SkiaSharp, HarfBuzz and
  Newtonsoft.Json included). Tests check the references of the built assembly
  by exact name, and that the page itself names no database driver.
- The exported class mentions no type of the library. MEF cannot see a missing
  sibling file, so `ComponentCheck` makes the first use of the library inside
  a try block when a window opens, and a library that is missing or does not
  fit gives the page that says so. A test lists the members of the exported
  class and fails if one is a library type.
- What the page remembers and offers sits behind two small interfaces of the
  extension's own (`IAssistantSettings`, `IImportOffer`), so the view model
  names no library type and is tested with fakes. The environment variable
  `RFIDGEAR_CARDCHECK_APPDATA` moves the settings folder and the search for
  the old files to another folder; the test run sets it to a temporary one, so
  no test reads or writes the settings of whoever runs it.
- The settings window sits behind a third seam, `IAssistantSettingsEditor`
  (read, save, test the order database). `SettingsViewModel` works on a draft
  and saves with the data as loaded and as edited, so only the values the
  operator changed are written and another window's changes to other values
  survive. A structural error stops the save; a folder or project that does
  not exist is only a warning.
- It does not set `Uri` in `[UiExtension]`: a `Uri` makes the host open that
  page and never ask the class for content.
- The exported class holds no state. Everything that belongs to one window
  lives in the page and its view model, which the host disposes when the
  window closes. The view model unsubscribes from the four host events there.
- The report file check mirrors the host's rule. A test runs both over a table
  of paths and fails if they disagree.
- `Workflow/CardWaiter` waits for things to happen on the reader: a card to
  arrive, to be taken off, or taken off and put back. RFiDGear does not poll
  the reader for an extension, so the waiter looks itself with `ScanAsync`,
  every half second plus the time a read takes (four times as long after an
  answer that says nothing). Only an answer in the state `Ready` says what is
  on the reader: `Busy`, `ReadFailed` and `NoReader` are never taken for "the
  card was taken off", and a reader that is empty at the first look counts as
  taken off. A PC/SC reader cannot tell an empty reader from a card it cannot
  read or one that another program holds, so one such answer counts as a
  removal. To stop a wait, cancel it and await it: it ends only after a look
  that has started has ended (up to a few seconds), so no scan is running when
  it returns, and a run (which the host refuses while a scan is going) can
  follow. A card that is taken off and put back between two looks is not
  seen. A look activates the card again and ends an authenticated session, and
  RFiDGear does not notice other users of the reader: wait only while nothing
  else works with the card. The page does not use the waiter yet; the workflow
  of the next steps does.
- `Workflow/CheckRunner` runs one check project for one order and reads the
  report it wrote. The first run of the check (default project, writes the
  first report) and the run that reads the memory out after programming
  (custom project, continues from the second report, writes the third) are the
  same job with other arguments. It checks the project, works out the order's
  paths and makes its folder (`IReportFiles`), has RFiDGear load the project,
  deletes a report an earlier attempt left, runs the project with `$JOBNUMBER`,
  `$CHIPNUMBER`, `REPORTTARGETPATH` and, if the run continues from a report,
  `REPORTTEMPLATEFILE`, and reads the report (`IReportService`). If a report
  language is given, the project of that language (`name.deutsch.rfPrj` or
  `name.englisch.rfPrj`) beside the one given is run instead. Every failure is
  a result with a stable code (`CheckFailureCodes`, or the library's own code
  for a number or a report) and nothing is retried; a run that ends well but
  leaves no readable report is a failure, never "the chip is not supported".
  A run that ends with intentional failures is finished: the check project
  fails some checks on purpose. A run that fails is a failure even when it left
  a report; the report is then offered as findings, not as the verdict, because
  a failed run can leave flags that were never set. Once a run has ended its
  report is read even if a cancel came meanwhile. A request that names a
  report that is not a step report is refused before anything is made or
  deleted, and the order's folder is made last. Loading replaces the project in
  RFiDGear's main window; if the project is changed again before the run starts
  (another project opened, this one reloaded or its tasks replaced), the run is
  not started. The disk work runs on a worker thread. The page does not
  use the runner yet; its own run method goes when the guided page replaces
  it.
- `Workflow/CardCheckWorkflow` is the guided check without a window: the list
  of orders, starting an order, waiting for a card, the check run, the verdict
  and the finish (the final report is made, uploaded with `IOrderStore`, the
  working reports are cleaned up). It covers the verdicts that do not lead to
  programming; programming, the read-out and the review come later, and for a
  chip that can be programmed it stops at the verdict with only Cancel
  offered. The page binds to `Current` (an immutable `WorkflowSnapshot`: state,
  what the operator may do, whether something runs, the orders, the card, the
  task outcomes, the verdict, the failure as a stable code) and `Changed`, and
  calls the commands. A command is a synchronous request that returns whether
  the state offers it and starts at most one operation; while one runs only
  Cancel is accepted, and a cancel wins over whatever the step brings back
  (the host cancels a run only between two tasks). The report is made and
  uploaded without a cancel, because the upload may already have been
  committed. The rules are tables (`WorkflowTables`: which state can follow
  which, what is offered in each, what closing the window means), and a test
  demands an entry for every state. Starting an order reads the list again, so
  a stale list cannot start a finished order (in the test mode only the third
  stub order is in progress). Two windows cannot check the same order: the
  reports of an order are files with fixed names, so a window reserves the
  order from the start of its check until it goes back to the list, is
  cancelled before the check, or is closed (`OrderReservations`, one instance
  for all windows). The workflow is for one context, the window's;
  events are delivered in the order of the versions, also when a handler calls
  a command. The order store and settings come from one
  `IWorkflowContextSource`, so an order is listed, checked and finished with
  the same pair (the real one, with the test folder for the made-up orders,
  comes with the page). The page does not use the workflow yet.
