---
title: Card Check Assistant
---

The Card Check Assistant is an application extension for RFiDGear. It guides
the operator through the check of the chip of an order: pick the order from
the list, put the chip on the reader, run the check, read the verdict, and
finish the order, which makes the final report and stores it in the order
system. It appears in the Extensions menu of the main window.

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

- Shows the orders of the order system (server, database and table from the
  Settings window) with their customer and status. Only an order that is in
  progress can be checked. The list is read when the window opens and again
  every ten seconds while nothing else is going on (no order selected, no
  window of the page open); **Refresh list** reads it at once. The report
  language (German or English, from the settings until the operator chooses
  one) picks the project of that language.
- **Check this order** starts the order. The list is read again, so a stale
  list cannot start a finished order; the settings and the report paths are
  checked; if reports of an earlier check exist the operator is asked whether
  to replace them (the finished report is only replaced when the order is
  finished). Then the page waits for a chip and says what it sees: no reader,
  RFiDGear busy with tasks, or the chip found. **Start check** loads the check
  project from the settings into RFiDGear (it replaces the project open in the
  main window) and runs it once with `$JOBNUMBER`, `$CHIPNUMBER` and
  `REPORTTARGETPATH`. The results of the tasks are listed. A failed run can be
  repeated, which goes back to the chip step; nothing is retried on its own.
- Shows the verdict from the first report: not supported, supported but too
  little free memory, can be programmed, or unclear (shown as a warning), with
  the chip type, the free memory, the notes of the report and its path.
  **Finish order** makes the final report and stores it in the order system;
  if that fails it can be repeated or cancelled, and the page says what to
  check. For a chip that can be programmed the verdict offers **Begin
  programming** instead; if the project that reads out the memory cannot be
  used, the page says why and the only way out is **Leave order open**.
- Says what went wrong in words, chosen by the code of the failure (never by
  its kind, which stands for different causes), with the code in small print
  for support. A running step is stopped with Cancel; closing the window in
  the middle of a step that cannot be left (preparing, running, storing) is
  refused with a hint, and closing it while a check is only waiting asks
  first. Nothing of an order changes until it is finished.
- Runs a project that ends with the program task `%EXIT` (the old assistant's
  way of closing RFiDGear) without closing RFiDGear: the host ends the run at
  that task as a normal finish, the tasks after it are shown as not reached
  and are not held against the outcome. The main window still closes RFiDGear
  on that task.
- Is in English or German, following the language of RFiDGear, and switches
  when that changes.
- Has a **Settings...** button, offered while the list is shown, that opens a
  modal window with everything the assistant keeps: the folder reports are
  written to (one folder per job, remove working reports, default report
  language), the default project and the project that reads the memory of the
  card, the order database (server as `host` or `host\instance`, database,
  table, and a **Test connection** button that says in words what it found;
  the Windows account is used to log in), and the text blocks (add, rename,
  edit, remove). Save writes only the values that were changed, so a second
  window's changes to other values survive. A path that is relative or
  unusable, a text block without a name or with a name another one has, and a
  database with only part of its entries stop the save; a folder or project
  that does not exist is only a warning, because it may be a network share
  that is not reachable just now. Closing the window with changes asks first.
  A settings file of a newer version, or one that cannot be read, is shown as
  such and is never saved over. After a save the list is read again.
- Has a **test mode** for trying a check with only a reader and a card, when
  there is no order database or no order in it. It is switched on with the
  check box in the Settings window ("Test mode: use stub jobs", saved, off by
  default). While it is on, the page shows a notice, the orders are five
  made-up ones (job numbers `STUB-1001` to `STUB-1005`, of which only the
  third is in progress), and the reports go to a `Test` folder: below the
  report folder of the settings if it is set and exists, otherwise in
  `%TEMP%\RFiDGear\CardCheckAssistant\Test`. The made-up orders are kept in
  memory for as long as the window lives, so an order that was finished stays
  finished in it; the order database is never read or written in this mode
  (**Test connection** in the Settings window still tests the real one).
- Does **not** offer, for now, to take over the settings of the old WinUI Card
  Check Assistant. The code is kept (a question with "Import", "Not now" and
  "Never ask again", then a summary to confirm; the old files are only read,
  and the user name, password and port in them never are), but the settings
  are built from scratch and no backward compatibility is wanted. While it is
  off nothing is read from the old assistant's folder and no marker is
  written. `AssistantServicesFactory` has the one switch; Slice 6 decides
  whether to turn it on for the cutover or to remove the code.
- Left out on purpose, compared with the run-once page it replaces: the fields
  for job number, chip number and report file (they come from the order), the
  project picker (the project comes from the settings), the **Read card**
  button and the card line (the page looks at the reader itself while it waits
  for the chip), and the line for the project open in RFiDGear.

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
  class and fails if one is a library type. The page, its view model and the
  factories do use library types, but they are only created after that check;
  a test also fails if one of them holds a library enum or struct in a field,
  because the runtime would load the library for it when the type is loaded.
- What the page offers sits behind small interfaces of the extension's own
  (`IImportOffer`, `IGuidedWorkflow`), so the view model is tested with
  fakes. The environment variable
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
  window closes. The view model unsubscribes from the three host events (reader,
  running tasks, language) there.
- The paths of the reports are made by the library (`IReportFiles`), whatever
  the job and chip numbers look like; a test hands every path it makes to the
  real host and fails if the host refuses one.
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
  else works with the card. The workflow uses it while the page waits for a
  chip.
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
  not started. The disk work runs on a worker thread.
- `Workflow/CardCheckWorkflow` is the guided check without a window (the page
  below is its window): the list
  of orders, starting an order, waiting for a card, the check run, the verdict
  and the finish (the final report is made, uploaded with `IOrderStore`, the
  working reports are cleaned up). It covers the verdicts that do not lead to
  programming, and the programming itself: for a chip that can be
  programmed (and when the project that reads out its memory can be used),
  `BeginProgramming` waits for the chip to be taken off the reader and put
  back while the operator programs it elsewhere. A wait that runs out cancels
  nothing; the operator can wait again. Then `ContinueProgramming` checks the
  operator's answers (nothing is written if they are not good), writes them
  into the second report, sets the change date of the order and reads the
  memory out, as one operation. If the report, the date or the read-out fails,
  the operator can repeat that step; after a failed read-out the order can
  also be finished with the second report, by an explicit choice. A cancel
  is never turned into the review. `Finish` at the review makes the final
  report from the report of the read-out. The page leads there with the
  Begin programming button at the verdict of a chip that can be programmed;
  without a usable read-out project it explains why it is not
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
  the same pair. `Services/WorkflowContextSource` is that source for the page:
  in test mode it hands out the made-up orders (one set for the window, also
  when the mode is switched off and on) and a `Test` folder for the reports
  (`TestReportFolder`); otherwise a new store of the order system for the
  database settings as they are now and the report folder of the settings,
  which must be set and exist. Unreadable settings, settings of a newer
  version, a report folder that is not set or not there and a test folder that
  cannot be made are failures with a fixed code and text, not exceptions; an
  exception nobody expects is left for the workflow's catch-all, which logs it.
  `ViewModels/GuidedCheckViewModel` is the page. It binds to the workflow
  through `IGuidedWorkflow` (so a test can hand it any snapshot in any order)
  and shows every snapshot in full: each change of the workflow or of the
  host is posted to the window's dispatcher and
  applied there in order, a snapshot that is not newer than the one shown is
  ignored, and every property is computed from the snapshot, never from the
  difference to the one before. The steps (`GuidedStep`) follow the states;
  the commands follow the controls the snapshot offers. A refused start ends
  in the list with its reason in the snapshot, which the next read would
  erase, so the page keeps it as a notice until the operator acts. The list
  is read again every ten seconds only while nothing else is going on: it is
  shown, no order is selected, the last read worked and no window of the page
  is open. A failure is shown as a text chosen by its code
  (`Localization/FailureTexts`: a text of its own, else the family of its
  ending, else a generic text with the English safe message), never by its
  kind, because the same kind stands for different causes; the code is shown
  with it. Closing the window asks the workflow: nothing in progress closes,
  a step that cannot be left refuses, and a check that can be abandoned asks
  first, through `IGuidedPageServices`. `Views/GuidedCheckView` is its window:
  one section for each step, shown at a minimum of 620 by 490, with the way
  out (Cancel, or Leave order open for a programmable chip; from the
  programming on it asks first) in the footer and the problems with their codes
  below the step. It opens the page when it is loaded and asks the page
  before the window closes. `Services/GuidedWorkflowFactory` builds the real
  workflow of a window from the settings, the order system (or the made-up
  orders), the reports, `CheckRunner` and `CardWaiter`, and a workflow that
  only says it has nothing to work from for the case that the assistant's
  application data cannot be reached (or when building the real one fails).
  It uses library types to build the workflow, so `AssistantServicesFactory`
  calls it only after the library is known to be there, and the exported
  class sees only `IGuidedWorkflow`.
- The programming step of the page keeps the operator's answers in one form
  (`ViewModels/ProgrammingFormViewModel`) that belongs to the order: the
  template used in the LSM, three yes-or-no questions (unanswered is not "no"),
  the restriction text (it starts as the notes of the first report, which it
  replaces; a text block is appended, never put in place of what was typed) and,
  for a Classic template, the sectors used. The form has a data context of its
  own, so what the page announces with every picture of the reader does not undo
  what is being typed. It can be edited while the chip is awaited, so the
  questions can be answered while the chip is programmed elsewhere. *Continue*
  needs the chip back and a complete form, and asks once if the programming or
  the function test was answered "no". Once the work has begun the engine has
  the answers, so the form is shown locked.
- The review step of the page shows the path of the report of the read-out (the
  one the finished report is made from, as it is on disk when the operator
  presses *Report checked*), warnings that never block (another chip put back, a
  different chip type in the two reports, a read-out that marks the chip as not
  supported) and buttons that open the report and its folder. After a failed
  read-out the page offers *Read out again* and, after a question, *Finish
  without read-out*. From the programming on, leaving the order open asks first,
  because nothing resumes it.
- `Services/ShellOpener` opens a report, or the folder it is in, in the
  program the operator has set up for it, so that a report can be looked at
  (and corrected) before the order is finished. It is the one place of the
  extension that starts a program, and only in the way the shell opens a
  document: an existing PDF file or an existing folder given by a full path;
  never a program by name, never arguments. Other file types, relative paths
  and device paths are refused with a code (`OpenerFailureCodes`) and a text.
  Two tests keep it the only one: the assembly may use only four members of
  `Process` and `ProcessStartInfo`, and no other file of the extension may
  name them. The review step of the page uses it, through
  `IGuidedPageServices`, off the window's thread.
