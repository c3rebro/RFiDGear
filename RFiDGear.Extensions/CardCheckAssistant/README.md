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
- If the library file is missing, or is of another platform or version, the
  window opens with a short text that names the two files and the folder,
  instead of the page.

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
- It does not set `Uri` in `[UiExtension]`: a `Uri` makes the host open that
  page and never ask the class for content.
- The exported class holds no state. Everything that belongs to one window
  lives in the page and its view model, which the host disposes when the
  window closes. The view model unsubscribes from the four host events there.
- The report file check mirrors the host's rule. A test runs both over a table
  of paths and fails if they disagree.
