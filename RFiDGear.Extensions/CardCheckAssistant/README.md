---
title: Card Check Assistant
---

The Card Check Assistant is an application extension for RFiDGear. It runs a
project once on the card that is on the reader, with a job number, a chip
number and an optional report file, and lists what every task did. It appears
in the Extensions menu of the main window.

**Install:**

- Copy `RFiDGear.Extensions.CardCheckAssistant.dll`, and only that file, to
  `%ProgramData%\RFiDGear\Extensions`. The host already has the contract and
  the SDK; a second copy next to the extension would load the contract types
  twice.
- Restart RFiDGear. Extensions are discovered once at start-up, so there is no
  hot loading.

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
- Is in English or German, following the language of RFiDGear, and switches
  when that changes.

**Rules that keep it correct:**

- It references only `RFiDGear.Contracts` and `RFiDGear.ExtensionSdk`, both
  with `Private=false`. A build error stops a private copy of a host
  assembly. Tests check the references of the built assembly.
- It does not set `Uri` in `[UiExtension]`: a `Uri` makes the host open that
  page and never ask the class for content.
- The exported class holds no state. Everything that belongs to one window
  lives in the page and its view model, which the host disposes when the
  window closes. The view model unsubscribes from the four host events there.
- The report file check mirrors the host's rule. A test runs both over a table
  of paths and fails if they disagree.
