---
title: Keys that stay in a MIFARE SAM AV3, the checklist for the task commands
---

These cases check the task commands that authenticate a DESFire card, and
change its keys, with keys that stay in a MIFARE SAM AV3 (issue 347). They are
for **you** to run in the application: they change the keys of a card. Nothing
here has run as application code yet. The frames were seen on one bench SAM and
one DESFire EV3 with a throwaway console (`SAM-PICC-FIRST-CONTACT.md` and
`SAM-AV3-FIRST-CONTACT.md` are the record), and `SamPiccRelayTests` (see
`SAM-AV3.md`) checks the way through the application without changing anything.

**What it is:**

- A key slot of a DESFire task can name a key entry of the SAM instead of a
  typed key or a stored key: in the key manager ("Key manager..." at the
  bottom of the keys) choose the key store "MIFARE SAM AV3" and write
  `entry:version`, for example `1:0`. The project saves that reference and never a key; the key never reaches
  RFiDGear.
- Four commands take such a slot: the task "Check: App Key", the task
  "Change PICC Master Key", the task "Change Application Key" and the button
  "Authenticate with PICC MasterKey". All the keys of such a
  command have to be keys of the SAM (the SAM needs the old key and the new
  key to build the cryptogram of a key change), and the reader has to be a
  PC/SC reader with a reader set for the SAM. Every other command, and every
  check task, ends with "key not available" before it touches the card.
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
- Prepare two key entries with the SAM setup window (Tools > "MIFARE SAM AV3
  setup..."): entry 1 an AES-128 PICC key, for example
  `0F1E2D3C4B5A69788796A5B4C3D2E1F0` (SET `0x0024`), and entry 2 a DES/2K3DES
  PICC key, sixteen zero bytes, which is the factory key of the card (SET
  `0x0000`). The window says in green when an entry reads back as usable.
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
   bound to `2:0`. **This direction (AES to DES) has not been tried on a
   card.** Expect success and the factory key on the card; if it fails, write
   down the status words of the line and check which key the card holds.
5. Create an application with a typed key (a task you know), then run the
   task "Change Application Key" on it: the key that authenticates and the
   key that is replaced bound to the SAM `1:0` (AES) and the new key to the
   SAM `3:0` (another AES entry). **The change of an application key through
   the SAM has not been tried on a card** (the P1 and the P2 of a key other
   than the one that authenticated). Expect success and a check by
   authentication with the new key.
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
11. Switch the provider to the TWN4 and run case 2: expect the line that a key
    of the SAM needs a PC/SC reader, and nothing sent to the card.

**What is not covered:** 3K3DES keys (a SAM entry cannot be one), key
diversification, the ISO and the EV2 modes of the authentication, and every
command that is not one of the four. Reading and writing a file with a key of
the SAM, creating and deleting applications and files, and key settings are
later work.
