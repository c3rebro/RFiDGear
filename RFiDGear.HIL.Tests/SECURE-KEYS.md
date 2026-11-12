---
title: Secure-key hardware cases
---

These cases check the secure key provider (issue 234) on a real card: a
project that names a key by a reference to the Windows Credential Manager
instead of holding it. Five of them run by themselves (`SecureKeyWorkflowTests`).
The others need a person, a second kind of card or the application window, and
are a checklist below. Nothing here is claimed to have run on a card until you
have run it: the code behind the automated cases is checked against a scripted
reader (`HilSecureKeyScenarioSelfCheckTests`), the card is the part that is
left.

**Before you start:**

- Use a card you can spare. The automated cases need a DESFire EV1, EV2 or EV3
  card in factory state, as the other DESFire cases do (PICC master key all
  zero). They create and delete the application `0x5F5F01` and change its key
  0. They never change the PICC master key.
- Close every program that uses the reader. With a PC/SC reader the card has
  to be on the reader before you start.
- Run the cases once with each reader you use. The Elatec TWN4 and the PC/SC
  reader (LibLogicalAccess) are two code paths, and they differ.
- The cases use the Windows Credential Manager of the user who runs them. They
  store test keys under `RFiDGear/key/hil/<run>/<name>` and remove them when
  the case ends, also when it fails. The keys are test keys on a test
  application, not secrets.
- Set `"secureKeys": true` in `hil.settings.json`. It is off in the sample, it
  does not depend on `readerScan`, and a file with a mistake in it switches
  nothing on.

**Running the automated cases:**

`Run-HilTests.bat` runs them with everything else, once the setting is on. It
says so in its header. To run only these:

```powershell
dotnet test RFiDGear.HIL.Tests `
  --filter "FullyQualifiedName~SecureKeyWorkflowTests"
```

Each case runs twice, for an AES application and for a DES (2K3DES)
application. Without the setting they are skipped, and the skip says which file
and which setting to change.

**The automated cases (numbers are those of the plan):**

- 2, a bound slot opens the application: the application key is changed with
  typed keys to a key that only the Credential Manager knows, and a task whose
  slot is bound to that credential authenticates. The key typed into the task
  is the old one, which the card refuses now.
- 3, a bound key change and back: the key that opens the application and the
  new key both come from the Credential Manager; then the card is asked with
  the new key typed in; then the change is made back the other way round. The
  typed keys of the bound tasks are swapped on purpose.
- 5, a reference that gives no key: for the new key, and for the key that
  opens the application. The task ends with `KeyUnavailable` and makes no card
  call. The key typed into the task is the right one and is not used. The card
  still has its old key afterwards.
- 6, the second task of a run names a credential that does not exist. The
  first task of the run opened the application with a key from the Credential
  Manager and the card would accept that key. The second task must end with
  `KeyUnavailable`: it does not use the key of the first.
- 8, a key of the wrong length (6 bytes, the length of a Classic key, and 24
  bytes, a 3K3DES key) is refused for the key that opens the application and
  for the new key, before the card is touched.

Case 1 (typed keys as before: authenticate, change a key, verify) is the
existing `FullCrudWorkflow` of `DesfireWorkflowTests`, which runs with every
run of this project.

**If a run is interrupted:**

- The test application stays on the card. The next run of any DESFire case
  deletes it first. By hand: delete application `0x5F5F01` with the PICC master
  key (all zero).
- A test credential stays in the vault. `cmdkey /list` shows the targets; the
  ones of these cases start with `RFiDGear/key/hil/`. Remove each with
  `cmdkey /delete:<target>`. The log of the run
  (`%LOCALAPPDATA%\RFiDGear\log\hil-log-<date>.txt`) names the ones that a case
  could not remove.

**The checklist for the cases that need a person:**

For these, work in RFiDGear itself. In a task dialog, a key box has a button
"Use reference...": choose the Credential Manager, type a reference, and press
"Store key..." to put the key under it (the digits are typed into a password
box and are not shown again). Use references that start with `hil-` so that you
can tell them from your own, and remove them afterwards.

- 4, the PICC master key, bound, and back. Needs a card you can lose. Bind the
  new master key to a credential that holds a key you know, change the PICC
  master key, then authenticate with the bound key. Change it back to zeros,
  and authenticate with zeros. Keep a note of the key you stored: if the change
  back fails, the credential is the only place where the new master key is
  written down. Expected: both authentications succeed, and the card is as it
  was.
- 7, the card is pulled during a key change. Bind both keys of an application
  key change, start the run, and take the card off the reader within a second.
  Expected: the task ends with an error, and the run does not try the key
  change a second time (the log shows one change attempt). Afterwards ask the
  card with the old key and with the new key, and report which of them works:
  what the card does when the command is cut off is part of what this case
  finds out.
- 9, MIFARE Classic. Needs a Classic card you can spare, and for the MAD steps a
  PC/SC reader (MAD is not implemented for the Elatec reader). Bind key A and
  key B of a sector to credentials with the keys of the card and read the
  sector. Then bind key B to a reference that does not exist: expected
  `KeyUnavailable`, and no read with key A alone. Then write the MAD with
  bound target keys, and write the original keys back. Expected: every
  step succeeds, or stops with `KeyUnavailable` before the reader is touched.
- 10, saving during a key change. Start a run with a bound key change and use
  File > Save and File > Save as while it runs. Open the saved project as a zip
  archive: `taskdatabase.xml` has `KeyBindings` entries and none of the key
  digits. Search the profile for the digits of a key you used, for example
  `findstr /s /i <digits> "%LOCALAPPDATA%\RFiDGear\*"`: there is no match,
  and there is no `taskdatabase.xml` in that folder.
- 11, automatic mode with bound keys. Use a project whose task is bound, switch
  automatic mode on, and put three cards on the reader one after the other.
  Expected: each card is processed once, no window asks for a key, and the log
  shows the keys being asked for once per card.
- 12, Format card on a project with typed keys. Destroys everything on the
  card. Use a card with a test application on it. Expected: the card is
  formatted, authenticated with the master key of the project. This case
  covers the fix that stopped the format from using the key of the wrong slot.
- The migration (the last step of issue 234) is a case for a copy of a real
  project: File > "Move keys of a project to the Credential Manager...", open
  the copy, save under a new name. Expected: the new project opens, its tasks
  run on a card as the old project did, the old project is unchanged, and the
  report names no key.

**What to send back:**

The report of the run (`TestResults\HIL\hil-<time>.trx`) for the automated
cases, and for each case of the checklist: which reader, whether it did what is
expected, and the lines of the log if it did not. Cases that fail on one reader
and pass on the other are the reason for running both.
