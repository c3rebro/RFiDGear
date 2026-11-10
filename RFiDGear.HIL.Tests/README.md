---
title: RFiDGear hardware tests
---

These tests run against a real reader and real cards. They are not part of
the normal test run (`RFiDGear.Tests`) and not part of CI. The solution
compiles them. The reader-scan tests open a reader only if you ask for it; the
older DESFire workflow tests do not wait to be asked (see the end).

**Running the reader-scan tests:**

Close every program that uses the reader first, including a second RFiDGear
window: a COM port can be open once. Put a card on the reader and leave it
there. Then, in PowerShell, from the folder of the solution:

```powershell
$env:RFIDGEAR_HIL = "1"
$env:RFIDGEAR_HIL_INTERACTIVE = "1"   # also the cases that ask you to move cards
dotnet test RFiDGear.HIL.Tests `
  --filter "FullyQualifiedName~ReaderScanTests" --blame-hang-timeout 5m
```

- Without `RFIDGEAR_HIL=1` every test of the class is reported as skipped,
  with the reason, and the reader is not touched.
- Without `RFIDGEAR_HIL_INTERACTIVE=1` the three cases that ask you to move
  cards are skipped; the other four run.
- The order is fixed: the four cases that do not move cards run first, with
  the card that was on the reader when you started. The three cases that ask
  you to move cards run last, and each of them asks for the card it needs.
- The cases that ask you to move cards show a message box that stays on top.
  OK means "done, go on", Cancel ends the test.
- A scan that has started cannot be limited from outside. If a run hangs,
  `--blame-hang-timeout` ends it.
- The log of the run is `%LOCALAPPDATA%\RFiDGear\log\hil-log-<date>.txt`.

**The reader:**

An Elatec TWN4 is found by the reader itself, with or without a card on it.
A PC/SC reader is found only with a card on it, because RFiDGear cannot tell
a PC/SC reader without a card from no reader: with one, put the card on the
reader before you start. With a PC/SC reader a card that cannot be read, and a
reader that was unplugged, are reported like a missing card (the contract of
`IReaderHost.ScanAsync` says so).

**The cases:**

Each case is a few lines in `ReaderScenarios.cs`. `RFiDGear.Tests` compiles
that file too and runs the same steps against a scripted reader
(`HilScenarioSelfCheckTests`), so the assertions are checked before they reach
the hardware.

- **AScan_AlwaysAnswers**: a scan answers Ready or NoReader, becomes the
  current picture, and lists the primary credential first. With an Elatec
  reader, with or without a card.
- **ACardOnTheReader_IsReportedWithItsUidAndType**: a card is on the reader
  from the start. The UID is upper-case hex, the type has a code and a name,
  and the first picture of the host raises `ReaderStateChanged` once.
- **ASecondScanOfTheSameCard_RaisesNoEvent**: a card is on the reader from
  the start and is not moved. The second scan gives the same picture and
  raises nothing.
- **AScanDuringARun_IsAnsweredBusy**: a scan while a run is going answers
  Busy, lists no card, changes nothing and raises nothing; a scan after the run
  works. With an Elatec reader, with or without a card.
- **ARemovedCard_IsReportedAsGone** (asks you to move cards): put any card on
  the reader, then take it off. The scan reports it gone, once.
- **AReplacedCard_IsReportedWithItsOwnUid** (asks you to move cards): put one
  card on the reader, then replace it with a different one. The second UID is
  reported, once.
- **ACardThatIsNotMifare_DoesNotLeaveTheOldCardBehind** (asks you to move
  cards): put a MIFARE card on the reader, then replace it with an HF card that
  is not a MIFARE card but that your reader can read (for example ISO 15693;
  iCLASS only if your reader reads it). The MIFARE card must not be reported
  any more. This guards the Elatec fix of the reader scan, which cannot be
  tested without a reader. A pass proves the fix only if the reader detected
  the second card (it usually beeps, or its LED reacts): a card the reader does
  not read gives the same result with the fix and without it.

**The DESFire workflow tests:**

`DesfireWorkflowTests` need a reader with a DESFire card and are not gated by
the variables: they open the reader on every run of this project. They try to
skip themselves when no reader is found, but xunit 2 reports the dynamic skip
of the assert library as a failure, not as a skip. Without the hardware they
fail with a message that starts with `$XunitDynamicSkip$` and goes on with the
reason ("No reader detected ..."). Run only the reader-scan tests with the
filter above, or leave the DESFire tests out with
`--filter "FullyQualifiedName!~DesfireWorkflowTests"`. The reader-scan tests do
not have this problem: they decide when the tests are discovered, from the
variables.
