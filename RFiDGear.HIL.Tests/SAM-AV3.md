---
title: MIFARE SAM AV3 setup, hardware cases
---

These cases check the setup of a MIFARE SAM AV3 in a PC/SC reader (issue 347):
Tools > "MIFARE SAM AV3 setup...". One of them runs by itself and only reads
(`SamDetectionTests`), one writes a key entry (`SamKeyEntryTests`), and the
rest is a checklist for the window, for **you** to run: it activates a SAM,
which is treated as permanent, and it writes a key into the SAM.

The steps themselves (the `6985` of a new SAM, the activation, the host
authentication, the key write and the read-back of the entry) have been run
once, on one bench SAM, with a throwaway console that traced every APDU. The
record is `SAM-AV3-FIRST-CONTACT.md` in this folder. What has **not** run on a
SAM is the window: that, and a second SAM, are what this checklist is for.

**What it is, and what it is not:**

- The SAM sits in a **PC/SC reader** (the contact slot of an OMNIKEY 5422, for
  instance) and is reached through LibLogicalAccess. It is not the TWN4: the
  TWN4 does not detect the SAM (Elatec.NET issue 28), and this work does not
  change that.
- It sets a SAM up: find it, activate a new one, authenticate as host, write one
  key into a key entry. **Using** that key, in the TWN4 or anywhere else, is the
  next task and is not checked here.

**Before you start:**

- Use a SAM you can spare. The activation worked on the bench SAM (`90AF`,
  `90AF`, `9000`) and the SAM stayed activated after it was taken out and put
  back. No way back is known, so treat it as **permanent**. It may still be
  refused by a SAM of another batch or firmware: write down the status word.
- Close every program that uses the reader. The window holds the reader of the
  application while it is open (the reader monitor, the automatic mode and the
  tasks leave it alone), and it does not open while a run holds the reader.
- A new SAM has the factory key, sixteen zero bytes, as its master key. If you
  do not know the master key of your SAM, do not guess: a wrong key counts as a
  use of the key if a usage counter is linked to the key entry.
- The log (`%LocalAppData%\RFiDGear\log\`) has lines that start with
  `SAM setup:`. They hold the step, the result and the status word, and never a
  key, a name of a stored key, a reader name, the serial number of the SAM or
  the text of an error. Keep the lines of your run: they are what to send back.
- The window is modal and holds the reader: while it is open you cannot use
  the rest of RFiDGear, and closing it lets go of the SAM and its session. For
  step 8, store a key first, with File > "Key manager..." (a name such as
  `sam/test` and 32 hexadecimal digits that are not a secret), **before** you
  open the window.
- Leave the own trace of LibLogicalAccess off (`config.log.enabled` with
  `seecommunication`) while a SAM is in a reader. It writes every answer in
  hexadecimal, and the answer to SAM_GetVersion holds the serial number of the
  SAM. It is off unless somebody switched it on, and RFiDGear cannot redact it.

**The automated case (read-only):**

`SamDetectionTests` needs `"readerScan": true` in `hil.settings.json`. It does
not depend on `readerKind`. To run only this:

```powershell
dotnet test RFiDGear.HIL.Tests `
  --filter "FullyQualifiedName~SamDetectionTests"
```

- **ASamAv3InAPcscReader_IsFound_AndGivesItsVersion**: reads the ATR of the
  card in each PC/SC reader (it connects to none of them), finds the SAM by the
  historical bytes `MIFARE SAM AV3`, connects to it, asks for its version
  (hardware and software information, 14 hexadecimal digits each) and sends one
  instruction that does not exist, which must come back as a status word and
  not as an error. It authenticates nothing and writes nothing. It is skipped,
  with the reason, when no SAM is in a PC/SC reader.

**The checklist for the window (you run it):**

Stop at the first surprise, and write down the status word and the line of the
log. What the window looks like, as the tests render it with a scripted SAM
(so the reader name and the messages are the tests' own): `images/` has
`sam-setup-found-en.png` (before the check) and `sam-setup-authenticated-en.png`
(after the host authentication), and the same in German (`-de`).

1. Open Tools > "MIFARE SAM AV3 setup...". The reader with the SAM is in the
   list and is chosen, and the state says that a SAM was found. The box for the
   master key is not there while "Factory key" is ticked. With the window open,
   look at the reader indicator of the main window: it should show the reader as
   busy (nothing has checked that on a screen yet). When you close the window
   the reader works again (try "Read chip").
2. "Check SAM". The state says connected, and the hardware and software
   information are shown. On the bench SAM they were `04010105002301` and
   `04010105011F01`. **No serial number** is shown anywhere.
3. "Authenticate" with the factory key ticked. Three outcomes:
   - "answered 6985: it was never activated": a new SAM. No key was used. The
     state says that the SAM is not activated and "Activate SAM..." is on. Go
     to step 4.
   - "Host authentication succeeded": the SAM was activated already and still
     has the factory key. Go to step 6.
   - "did not accept the authentication (status XXXX)": its master key is not
     the factory key. Stop. Do not try keys by guessing.
4. "Activate SAM..." **only if you want that**. The question says that no way
   to undo it is known, and **No** is the button that is on. Answer No once:
   nothing changes, and the message says "Not activated". Then again, with Yes.
   Three outcomes, all of them worth reporting with their status word:
   - "The SAM was activated. Authenticate now." (This is what the bench SAM
     did: `90AF`, `90AF`, `9000`.)
   - "The SAM refused the command (status XXXX)". Nothing was changed, as far as
     the SAM says.
   - "The activation ended with an error, but the SAM may be activated all the
     same": do "Check SAM" and "Authenticate" to find out.
5. "Authenticate" again. It must succeed now (on the bench SAM it did, with the
   factory key). If you take the SAM out of the reader and put it back, it
   must still be activated: "Check SAM" and "Authenticate" succeed without a
   second activation.
6. Write a key: key entry 1, and a key of your own that is not a secret (for
   instance `0F1E2D3C4B5A69788796A5B4C3D2E1F0`), typed into the box (hidden
   until you tick "Show key"). "Write key". The message says that the key was
   written to key entry 1, and the box is empty afterwards. A second line says
   what the SAM reports when the entry is read back: enabled, AES-128, PICC key
   class (green), or that the entry is still disabled or of another kind (red).
   If the SAM refuses the write, the message gives the status word: that is the
   interesting result, write it down. Nothing is repeated.
7. Entries that must be refused: key entry 0 and key entry 128 turn the box pink
   and the button "Write key" off.
8. A stored key: choose the key that you stored before you opened the window
   under "Stored key" in the part "Key to write": the box for the key goes
   away. Write it to key entry 2. (It is in the list because the window reads
   the names when it opens and when you press "Search again".)
9. While a step runs, the window cannot be closed, and the buttons are off.
   After it, it closes.
10. Switch the language of the application to German and look at the window
    once.

**The case that writes (`SamKeyEntryTests`, needs `cardWrites`):**

- **AKeyEntryThatIsWritten_IsReadBack_EnabledAndOfThePiccKeyClass**: with the
  factory master key it writes the test key `0F1E2D3C4B5A69788796A5B4C3D2E1F0`
  as an AES-128 PICC key into key entry 120, version 0, reads the entry back
  with SAM_GetKeyEntry, and asserts that SET bit 9 ("disable key entry") is
  clear, the type is AES-128 and the key class is PICC. A blank entry is
  disabled, so this is what shows that the write enabled it. It never
  activates a SAM and never changes a master key: it is skipped for a SAM that
  is not activated, or whose master key is not the factory key, and for an
  entry 120 that is in use. Run it again and it writes the same key again. The
  entry stays written.

**What this does not check:**

- That the key you wrote works. It is an AES-128 PICC key in a key entry
  (`SET 0x0020`, `ExtSET 0x01`) with the master key as the key that may change
  it. A host authentication cannot prove it: it is not a host key. What the
  read-back proves is the entry: enabled, AES-128, PICC class. That the key in
  it is the key you typed is not proved by anything until a task uses it to
  authenticate a PICC.
- The master key and the key usage counters: the setup never changes either.
  (On the bench SAM the master entry has no key usage counter, so a wrong key
  counts against nothing there. A SAM of another batch may differ.)
- What the last byte of the answer to SAM_GetVersion means. On the bench SAM it
  went from `03` to `A3` when the SAM was activated. The window does not show
  it. If you can read it in NXP's documents, say what bits `0x80` and `0x20`
  are.
- Whether the activation can be undone. Nobody has tried, and nothing we could
  read says how.
- Whether closing the window resets the SAM, which would end the session of the
  host authentication. If you can tell (the next "Authenticate" after a close
  succeeding or failing tells nothing by itself), say so.
