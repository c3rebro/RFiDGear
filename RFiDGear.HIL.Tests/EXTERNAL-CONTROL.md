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
  are `status`, `run`, `result`, `results`, `auto` and `keys`; its README lists
  them. The log of RFiDGear is `%LocalAppData%\RFiDGear\log\`.
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

**Cleaning up:**

Remove the test application from the card if you made one (`SECURE-KEYS.md`),
delete the copy of the project, and switch external control off again if you do
not want it on.
