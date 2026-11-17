---
title: Keys that stay in a MIFARE SAM AV3, the checklist for the task commands
---

These cases check the task commands that authenticate a DESFire card, and
change its keys, with keys that stay in a MIFARE SAM AV3 (issue 347). They are
for **you** to run in the application: they change the keys of a card. Nothing
here has run as application code yet. The frames were seen on one bench SAM and
one DESFire EV3 with a throwaway console (`SAM-PICC-FIRST-CONTACT.md` and
`SAM-AV3-FIRST-CONTACT.md` are the record), and `SamPiccRelayTests` (see
`SAM-AV3.md`) checks the way through the application without changing
anything; `Twn4SamRelayTests` does the same on a TWN4.

**What it is:**

- A key slot of a DESFire task can name a key entry of the SAM instead of a
  typed key or a stored key: in the key manager ("Key manager..." at the
  bottom of the keys) choose the key store "MIFARE SAM AV3" and write
  `entry:version`, for example `1:0`. The project saves that reference and
  never a key; the key never reaches RFiDGear.
- Four commands take such a slot: the task "Check: App Key", the task
  "Change PICC Master Key", the task "Change Application Key" and the button
  "Authenticate with PICC MasterKey". All the keys of such a command have to
  be keys of the SAM (the SAM needs the old key and the new key to build the
  cryptogram of a key change), and the reader has to be a PC/SC reader with a
  reader set for the SAM, or a TWN4 of hardware revision G with the SAM in one
  of its two sockets (no setting: the SAM is found by its ATR). Every other
  command, and every check task, ends with "key not available" before it
  touches the card.
- The version of a new key is the version of its key entry. The version box of
  the dialog is not used for a key of the SAM.

**Before you start:**

- Use a **spare card** and a **spare SAM** (the SAM is disposable). A key
  change can end with the card holding a key that nobody knows: keep the key
  of every step written down, and keep the keys of the SAM entries.
- The SAM in the contact slot and the card on the contactless slot of the
  **same** reader pair (an OMNIKEY 5422, for instance). Set the reader of the
  SAM in the reader settings (the contact slot) and the reader of the card
  (the contactless slot), PC/SC provider. Close the SAM setup window and every
  other program that uses the readers.
- With a **TWN4** (hardware revision G, TWN4 MultiTech 2): the SAM in one of
  its two SAM sockets (not in both), the card on its antenna, the provider
  Elatec in the reader settings, and no other program on its COM port. The
  setup window lists the SAM in a socket of the TWN4 too (the Elatec reader
  has to be selected and connected): prepare the key entries there, or with a
  PC/SC reader first.
- Prepare three key entries with the SAM setup window (Tools > "MIFARE SAM
  AV3 setup..."): entry 1 an AES-128 PICC key, for example
  `0F1E2D3C4B5A69788796A5B4C3D2E1F0` (SET `0x0024`), entry 2 a DES/2K3DES PICC
  key, sixteen zero bytes, which is the factory key of the card (SET
  `0x0000`), and entry 3 another AES-128 PICC key, for example
  `00112233445566778899AABBCCDDEEFF`, which the cases 5 and 6 use for the
  keys of an application. The window says in green when an entry reads back as
  usable.
- The log has lines that start with `SAM PICC:`. They hold the step, the
  result and the status words, never a key, a reference, a reader name or the
  text of an error. Keep the lines of your run: they are what to send back.

**The cases:**

1. Set a task "Change PICC Master Key": the current key DES, the new key
   AES; bind the current key to the SAM `2:0` and the new key to the SAM `1:0`.
   Run it on the spare card in the factory state. Expect success, the status
   line "Changed key 0 of AppID 0 with keys of the SAM", and the card now holds
   the AES key of entry 1. Check it from outside: authenticate the card with
   the typed key `0F1E2D3C4B5A69788796A5B4C3D2E1F0` (AES) in another task.
2. Press "Authenticate with PICC MasterKey" with the current
   key AES bound to the SAM `1:0`. Expect success and the line "Authenticated
   with a key of the SAM to AppID 0, key 0".
3. Run the task of case 1 again, now with the card in the AES state: it must
   fail with an authentication failure (the card refuses the DES key of entry
   2), say so in the status line with the reason and the status words, and
   leave the card as it was (check with the typed AES key).
4. Change the key back: the current key AES bound to `1:0`, the new key DES
   bound to `2:0`. **This direction (AES to DES) was seen on a TWN4 (the TWN4
   section of `SAM-PICC-FIRST-CONTACT.md`), not yet with a PC/SC reader.**
   Expect success and the factory key on the card; if it fails, write down
   the status words of the line and check which key the card holds.
5. Create an application with AES keys that you know: its keys 0 and 1 both
   hold the test key of the SAM entry `1:0`. **The change of an application
   key through the SAM was seen on a TWN4, not yet with a PC/SC reader**; the
   two cases below are what it needs.
   a. Key number 0, so that the key that authenticates is the key that is
      replaced: run "Change Application Key" with the application key bound
      to the SAM `1:0` and the new key bound to `3:0` (another AES entry).
      Expect success and a check by authentication with the new key.
   b. Key number 1 with the key policy "change with the master key", so that
      key 0 authenticates and key 1 is replaced (the P1 and the P2 of a key
      other than the one that authenticated): the dialog shows a box for the
      old key then. Bind the application key (key 0, the key that
      authenticates, which holds the key `3:0` since case 5a) to the SAM
      `3:0`, the old application key (what key 1 holds now) to the SAM `1:0`,
      and the new key to `3:0`. Expect success and a check by authentication
      of key 1 with the new key.
6. Run the task "Check: App Key" for that application with the
   current key bound to the SAM `3:0`. Expect success.
7. Leave the reader of the SAM empty in the reader settings and run any of the
   four: expect the task to end with "key not available", the line "No reader
   is set for the SAM", and nothing sent to the card.
8. Bind only one of the two keys of a change to the SAM and type the other:
   expect "key not available" and the line that keys of the SAM cannot be
   mixed with others.
9. Bind the read key of a file to the SAM: the key manager does not list the
   SAM for that slot. Bind a key of an offered slot to the SAM and run a task
   that does not know it (a format of the card, a file read): expect "key not
   available" and the line that says which commands can use a key of the SAM.
10. Take the card off the reader in the middle of case 1 (after the task
    started): expect an error, and the line "The card may hold the new key
    although the change was not confirmed" if the cryptogram had been sent.
    Check which key the card holds before you do anything else.
11. With the TWN4 and the SAM in one of its sockets, first make sure that the
    card holds its factory DES key (case 10 or a failed run may have left it
    with the AES key or another one: authenticate with each key you know and
    restore the factory key if need be). Then run the cases 1 to 4 again
    (only the reader changes; case 4 puts the card back into the factory
    state). Then run the cases 5 and 6 with a **fresh application**: the one
    of the first run is still on the card, so create another one (another
    AID) whose keys 0 and 1 hold the key `1:0`. **The cases 1 to 6 have been
    seen on a TWN4 and a card** through the provider of the application (the
    TWN4 section of `SAM-PICC-FIRST-CONTACT.md`); only the task window has
    not.
12. With the TWN4 and both sockets empty, or a TWN4 of an older revision (D or
    E): run case 2. Expect "key not available", the line "No MIFARE SAM AV3
    was found" with the hint on the hardware revision, and nothing sent to
    the card.
13. With a SAM in each socket: run case 2. Expect "key not available", the
    line that a SAM sits in each socket, and nothing sent to the card.
14. Take the SAM out of its socket, or the card off the antenna, in the middle
    of case 1: as case 10; check which key the card holds.

**What is not covered:** 3K3DES keys (a SAM entry cannot be one), key
diversification, the ISO and the EV2 modes of the authentication, and every
command that is not one of the four. Reading and writing a file with a key of
the SAM, creating and deleting applications and files, and key settings are
later work.
