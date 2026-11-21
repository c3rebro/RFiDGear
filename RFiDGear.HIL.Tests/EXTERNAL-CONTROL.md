---
title: External control hands-on checklist
---

This is the checklist for trying the external control bridge (issue 232) with a
real RFiDGear, a real reader and a card. Everything in it needs a person: a
window, a process that is killed, a card that is put on and taken off. Nothing
here is claimed to have run on a reader until you have run it. What the code
does behind each item is checked by tests against a fake host and a real pipe
(`ExternalControl*` in `RFiDGear.Tests`, `BridgeSampleClientTests` for the
client); what is left is the machine around it, and that is these items.

The contract (`RFiDGear.Bridge.Contracts`) is not frozen until the first part
(the gate) has passed. If an item shows a behaviour you do not want, that is the
time to change the protocol.

**Before you start:**

- Use a card you can spare, and **a copy of a project, never the real one**.
  The runs write the card as the project says.
- Build or install RFiDGear, switch external control on (settings dialog, Misc
  tab, "Enable JSON-RPC pipe server" / "JSON-RPC Pipe Server aktivieren", then
  restart RFiDGear), or start it with `EXTERNALCONTROL=1`. The label fits the
  smallest settings window, and the tooltip breaks its lines: look at both.
- The client is the sample: `dotnet run --project RFiDGear.Bridge.SampleClient
  -- <command>` (or the built `RFiDGear.Bridge.SampleClient.exe`). The commands
  are `status`, `run`, `result`, `results`, `auto`, `keys` and `approved-run`;
  its README lists them. The log of RFiDGear is `%LocalAppData%\RFiDGear\log\`.
- Write down what you see. A line "as expected" is enough; anything else is a
  finding.

**The gate: switching it on and the first run (slice 1):**

- 1, the setting: the check box is there, its tool tip explains the pipe, and
  after a change a note says that a restart is needed. With it off and no
  argument, `status` fails to connect (no pipe).
- 2, the argument: with the setting off, start RFiDGear with `EXTERNALCONTROL=1`:
  `status` works. Close it, start it without: `status` fails again, and the
  setting in the dialog is still off.
- 3, the status bar: while the sample is connected (`auto` stays connected) the
  status bar of RFiDGear says "External control: connected", and the text goes
  away when it leaves.
- 4, `status`: it prints the version, the protocol (1.0), the capabilities
  (`session`, `status`, `project.load`, `execution.run`, ...), the instance id
  and the reader state. Put a card on the reader and ask again: the card shows
  (`card on it`).
- 5, load a copy: `run job-1 C:\path\copy.rfPrj` loads the copy (the window
  shows its tasks; any unsaved change in the window is lost: confirm that this
  is what you want) and runs it. The result lists one line per task with the
  outcome you see in the window.
- 6, the same job again: `run job-1` (the same id) must **not** write the card a
  second time: it answers with the result of the first run (the request id is
  known). With another id it runs again.
- 7, a refusal: unload the tasks (a project without tasks) or take the reader
  away and `run job-2`: the answer is a refusal that says nothing was started
  (`NotStarted`, `noProjectLoaded` or `noReader`), and the card is untouched.
- 8, a run in the window: start a run with the button in the window and, while
  it goes on, `run job-3`: refused as busy, nothing started.
- 9, the client dies: start a run that takes a while (a project with a task
  that waits, or many tasks), kill the sample with Ctrl+Break or the Task
  Manager while it runs, wait for the run to end, start the sample again:
  `result job-4` gives the result. The run was not stopped by the client
  leaving.
- 10, RFiDGear dies: start a run, kill RFiDGear with the Task Manager in the
  middle, start it again (same setting) and ask `result job-5`: it says
  `Unknown` with "RFiDGear was restarted ...". Then `run job-5` must **not**
  start anything (it is a known id). Look at the card: what state is it in?
- 11, closing during a run: close the window while a bridge run is going on
  (also with Exit in the menu): a question appears that says the run would be
  cut off. "No" keeps RFiDGear and the run. "Yes" closes it, and the sample
  reports `Unknown`.
- 12, closing while connected: close RFiDGear while the sample is connected (use
  `auto`): the sample says that the connection ended and exits; it does not
  hang.
- 13, a window that is busy: open a dialog that blocks the main window (the
  settings dialog) and run `status`: it answers within a few seconds and says
  the window is not responsive (`stale`). Then `run job-6`: it is refused after
  about five seconds (`uiUnresponsive`). **Close the dialog afterwards and
  check that the run does not start by itself.**
- 14, a second RFiDGear: start another RFiDGear while the first runs: it exits
  without a message, and the sample still talks to the first.
- 15, a window that is never activated: start RFiDGear in the background (a
  scheduled task, `start /min`, or from a service session) with the setting on,
  and run `status` right away. The start-up finishes anyway after about two
  seconds (the log says that the fallback started it), and `status` shows
  `start-up complete` instead of `notReady` for ever. Without the setting this
  behaves as it always did.
- 16, elevation: start RFiDGear as administrator and the sample as a normal
  user, then the other way round. Write down whether `status` connects. The
  documentation says the two must have the same rights; this item says
  whether that is a rule or a limit.
- 17, the installer: install the MSI or the bundle on a clean machine and check
  that `RFiDGear.Bridge.Contracts.dll` and `StreamJsonRpc.dll` are in the
  install folder, and no folder of satellite languages is added (only `de`).

**Results and catching up:**

- 18, `results`: after a few runs, `results 0` lists them in order with
  increasing numbers; `results <n>` lists only those after `n`; the latest
  number agrees with `status`.
- 19, after a restart of RFiDGear the numbers start again, and the instance id
  is another one.

**Automatic mode:**

- 20, `auto`: put a card on the reader: the project runs once (the sample prints
  "card processed"). Leave the card: nothing more. Take it away and put it back:
  it runs again. Put a second card: it runs. The menu item "automatic mode"
  in the window is checked while the sample runs.
- 21, a failure: put a card that makes a task fail: the mode goes on, and the
  result says failure. A fault, or the reader being unplugged: the mode stops
  and `automatic mode off (readerLost)` or another reason is printed.
- 22, the user's mode: switch automatic mode on in the menu, then start `auto`:
  it is refused (`autoModeActive`), and `run job-7` is refused too, as long as
  the mode is on. (One client is served at a time, so a second sample cannot
  `run` while `auto` holds the connection: that is why this is tried the other
  way round.)
- 23, the end of the connection: start `auto`, kill the sample: the mode is
  off in the menu soon after. Stop the mode in the menu while a card is being
  written: the run ends normally, and closing RFiDGear at that moment still
  asks.

**Approving a card (Check UID with a variable):**

Use a copy of a project whose first task is Check UID with the text
`$EXPECTEDUID` (a UID typed as `$EXPECTEDUID`), and whose second task depends on
it (execute condition: task 0, error "No error"). You need two cards that have
different UIDs, A and B. `status` shows the UID of the card that is on the
reader.

- 34, the approval: put A on the reader and `run job-34 C:\path\copy.rfPrj
  $EXPECTEDUID=<UID of A>`: both tasks ran, the status is `success`.
- 35, another card: put B on the reader and run with the UID of A: the Check UID
  says "Card not expected", the second task did not run (nothing was written),
  and the status is `failure`, not `completedWithIntentionalFailures`.
- 36, nothing approved: run without the variable: the same as case 35.
- 37, swapped after the scan: `status` with A on the reader, swap to B, run
  with the UID of A: the same as case 35. A typed UID of another card (replace
  the text of the first task) ends "no" instead, and the status is
  `completedWithIntentionalFailures`: that is the difference between a question
  and an approval.

**Card keys from the client's vault:**

These use a DESFire application that you can spare (see `SECURE-KEYS.md` for
how to prepare one and clean up), and a copy of a project whose task slot is
bound to the provider "Connected external application" with a reference such as
`orders/4711`. In the dialog of the task, press the button of the key box, pick
that provider and type the reference. The key typed into the box is a decoy
(a key the card refuses), so a task that used it could not pass.

- 24, the key from the client: start `keys orders/4711`, type the key digits at
  the prompt (they are not shown), run the project from the window: the task
  authenticates. The client says one key was handed out when you end it.
- 25, no client: end the sample and run again: the task ends with
  `ERROR.KeyUnavailable` and the card is untouched. The key typed into the box
  is not used.
- 26, another reference: start `keys orders/9999` and run the project that names
  `orders/4711`: `KeyUnavailable`, and the client handed out nothing.
- 27, a client that is too slow: start `keys orders/4711`, suspend the sample
  process (Task Manager, "Suspend") and run: after about ten seconds the task
  ends with `KeyUnavailable`. Resume it; nothing happens on the card.
- 28, the log: search the log of RFiDGear for the reference, the key digits and
  the card UID (`findstr /s /i`): none of them is in it. The log says the slot,
  the provider and the cause.
- 29, the client leaves while a key is asked for: the task ends with
  `KeyUnavailable` at once, not after the whole time limit.

**The revision of the project (`project.revision`):**

Use a copy of a project with at least two tasks, and no card on the reader: the
refusals happen before the card is touched.

- 30, the revision is there: `status` prints a revision after the project line.
  `run job-30 C:\path\copy.rfPrj` loads the copy and runs it with the revision
  of the load (the sample passes it), and the result is a success.
- 31, a task changed after the client looked: load the copy in RFiDGear and
  note the revision that `status` prints. In RFiDGear, open a task, change a
  text (the description of a task will do) and close the dialog with OK,
  without saving the project. `status` prints another revision. Then
  `run job-31 --expect-revision=<the noted one>` is refused with
  `projectMismatch` and the field `expectProjectRevision`, and nothing happens
  on the reader. `run job-31b --expect-revision=<the new one>` is run.
- 32, a run is not a change: run the project from the window, then look at
  `status`: the revision is the one it was before the run (the results that the
  tasks show are not part of it).
- 33, a restart: close RFiDGear and start it again with the same project:
  `status` prints another revision for the same file. That is meant: a revision
  is valid for one process.

**Where a request for a key comes from (`keys.context`):**

Use the copy of a project from above, with a task that names the provider
`bridge-client` and the reference `orders/4711` for its key (a test card with
that key on the reader). The sample command `keys` asks to be told only about
its own runs with `--own-runs-only`, and starts a run on the same connection
with `--run=<requestId>`; the bridge serves one client at a time, so a program
that supplies keys and starts runs needs one connection for both.

- 38, a run of the client: `keys orders/4711 --run=job-38`, type the key. The
  task authenticates, the result is a success, and the sample says one key was
  handed out.
- 39, the same with the restriction: `keys orders/4711 --own-runs-only
  --run=job-39`. The same, and the line "and so is every run that this program
  did not start" is printed at registration.
- 40, a run of the window with the restriction: start `keys orders/4711
  --own-runs-only` (no `--run`), type the key, then run the project from the
  window. The task ends with `KeyUnavailable` and the card is untouched; the
  sample says that no key was handed out. The log of RFiDGear has a line that
  says the key request of the run (source `gui`) was refused without asking
  the client, which asked for its own runs only.
- 41, a run of automatic mode with the restriction: the same, then switch
  automatic mode on and present a card: `KeyUnavailable`, the log says source
  `autoMode`, nothing was handed out.
- 41b, a run that was left behind: with `--own-runs-only` and `--run=<id>` on a
  project that takes a while, end the sample (Ctrl+C) while the run is still
  going, start `keys orders/4711 --own-runs-only` again and let the run ask for
  its key: it ends with `KeyUnavailable`, the new sample hands out nothing, and
  the log names the source `bridge`. (The run belongs to the connection that
  started it, not to whoever registers next.)
- 42, the restriction is not needed to work: `keys orders/4711` without it, run
  the project from the window: the task authenticates (the vault of the sample
  serves the reference whoever asked, as before).

**Why a check failed (`execution.verificationDetails`):**

Use a copy of a project with one task "Check: File Settings" for a file of a
test card (application, file and keys that you know), no other task, and the
card on the reader. The sample prints the lines of the details under the task.

- 43, a difference: set the expected size to a value the file does not have
  (the other settings as the file has them) and `run job-43 C:\path\copy.rfPrj`.
  The task is `FAILED`, the lines say `DOES NOT MATCH`, `fileSize: mismatch,
  expected <the one you set>, read <the real size>`, and the other settings
  `match`, with the numbers you know.
- 44, a match: set the expected size right: the task is `ok`, the line says
  `matches`, and every compared setting says `match`.
- 45, nothing compared: give the task a wrong application key: the task is
  `FAILED` with `AuthFailure` and has no lines of details at all.
- 46, a part of the settings: select only the size and the change right in the
  task: the first line says `2 compared, 5 not` and only those two are listed.

**The reference flow (`approved-run`):**

Use a copy of a project whose first task is "Check UID" with `$EXPECTEDUID` and
whose write task depends on it (condition: that task, `NoError`), and a test
card on the reader.

- 47, approve: `approved-run job-47 C:\path\copy.rfPrj`, check the UID that is
  shown, type `yes`. The run is a success, the sample says `card of the run: the
  approved one`, and the exit code is 0.
- 48, decline: answer with anything but `yes`: `Declined`, nothing is started,
  the card is untouched, and `results` shows no run for the id.
- 49, a swapped card: at the prompt, put another card on the reader, then type
  `yes`. The run is a `failure`, the write task is skipped (the check ended
  `CardNotExpected`), the sample says `NOT THE APPROVED ONE`, and the exit code
  is 1.
- 50, two cards: put two cards on the reader: `AmbiguousCard`, no prompt, nothing
  started.
- 51, a changed project: at the prompt, change a text of a task in RFiDGear and
  close its dialog, then type `yes`: the run is refused (`projectMismatch`),
  `NotStarted`, and the card is untouched.

**Automatic mode that waits for your approval (`auto --approve-with`):**

Use a copy of a project whose first task is "Check UID" with `$EXPECTEDUID` and
whose write task depends on it (condition: that task, `NoError`), and two test
cards, A and B, with different UIDs. Switch the automatic mode of the user off
(menu) and have the log of RFiDGear at hand. The command decides about every
card by one rule: `auto --approve-with=$EXPECTEDUID` approves every card (the
variable is set to its UID, and the project is held to the state the card was
shown with), `--wait=<seconds>` thinks first, `--set=NAME=value` adds a variable
to every approval, `--no-pin` leaves out the state of the project, `--decline`
declines every card and `--no-answer` never answers. Ctrl+C ends the command
and stops the mode. A card that is shown prints `card <UID> shown`, and every
card ends with a line that says what the sample answered and what RFiDGear says
about the end.

- 52, a card is run once: `auto --approve-with=$EXPECTEDUID`, put A on the
  reader. `card <A> shown`, the run (the write task runs), `card processed:
  ... Success`, and `answered approved, RFiDGear says approved, run <id>`.
  Leave A on the reader for a minute: nothing more happens.
- 53, taken away and put back: take A away and put it back. A new `card <A>
  shown` with another presence id, a new run and a new approval: RFiDGear did
  not use the old one.
- 54, decline: `auto --decline`, put A on: `answered declined, RFiDGear says
  declined`, and the card is untouched. Leave it: nothing more. Take it away
  and put it back: it is shown and declined again.
- 55, no answer: `auto --no-answer`, put A on. After the limit (15 seconds) the
  end says `expired`, and the card is untouched.
- 56, a replaced card: `auto --no-answer`, put A on and, within the 15 seconds,
  take it away and put B on. The end of A says `cardChanged` and not `expired`,
  and B is shown as a new presence.
- 57, a changed project: `auto --approve-with=$EXPECTEDUID --wait=15`, put A on
  and, while the sample thinks, change a text of a task in RFiDGear and close
  its dialog. The end says `projectChanged`, nothing is written, and the card
  is shown again (and approved, now in the new state).
- 58, the same without pinning: add `--no-pin` and make the same change: the
  run starts, because the approval did not name the state. This is why the
  sample pins by default.
- 59, a swap while it thinks: `--wait=15`, put A on and replace it by B before
  the 15 seconds are over: `cardChanged` for A, nothing is written, and B is
  shown as a new presence.
- 60, a lost connection: `--wait=15`, put A on and kill the sample (Task
  Manager) while it thinks. The mode is off in RFiDGear (the menu item is not
  ticked), nothing was written, and the log says that the connection ended.
  Start the sample again: nothing runs until you do, and then A is shown as a
  new presence.
- 61, the mode of the user: switch the automatic mode of RFiDGear on in the
  menu and start `auto --approve-with=$EXPECTEDUID`: it is refused
  (`autoModeActive`), and the mode of the user is untouched.
- 62, a refused variable: add `--set=REPORTTARGETPATH=relative.pdf`. The
  approval is refused (`invalidRequest`), the sample declines the card
  (`answered declined`), and nothing is written.
- 63, a report for each card: `--set=REPORTTARGETPATH=C:\folder\card-??.pdf`
  (the folder exists, and the project has a report task). Two cards give two
  files, `card-01.pdf` and `card-02.pdf`, and the report of the second does not
  hold the fields of the first. The report path of the window is what it was
  before.
- 64, the safe switch: stop the sample (Ctrl+C: the mode goes off), load the
  copy of the project that only reads in RFiDGear, and start the sample again
  with `--approve-with`: the project that reads runs for the cards, and the
  one that writes does not.

Not in this list: the keys of an approved run with `ownRunsOnly`. The `keys`
command and `auto` each hold the one connection of the bridge, so the sample
cannot do both; `BridgeSampleClientTests` and `BridgeKeyContextTests` cover it
without a reader. Try it with your own client.

**Cleaning up:**

Remove the test application from the card if you made one (`SECURE-KEYS.md`),
delete the copy of the project, and switch external control off again if you do
not want it on.
