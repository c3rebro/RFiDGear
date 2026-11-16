---
title: MIFARE Classic hardware cases
---

These cases check the read, write and MAD calls of the reader for MIFARE
Classic on a real card and a real reader. Five of them run by themselves
(`ClassicWorkflowTests`). The rest are a checklist below. Nothing here is
claimed to have run on a card until you have run it: the steps of the cases are
checked against a chip in memory that behaves like the 4K that was probed
(`HilClassicScenarioSelfCheckTests`), the TWN4 and the writes are the part that
is left. What has run on a real card so far: the reads and the reselect of the
PC/SC reader, on a 4K in an OMNIKEY 5422CL.

**Which chips:**

MIFARE Classic Mini, 1K, 2K and 4K. A card of another family makes every case
skip, and the skip says so.

**Which cards:**

A card **as it left the factory**: every key `FFFFFFFFFFFF`, the access bits
`FF078069` (the transport configuration), every data block zero, no MAD. A card
that is not like that makes a case skip with the reason; none of them fails on
it. Use a card you can spare: the cases that write put everything back, but a
card that is taken away half way, or a reader that fails, can leave a change.

**What each reader gives:**

- **PC/SC (LibLogicalAccess)**: the reader says yes to every key and the chip
  judges it at the next access, and a refused access halts the card until the
  next command fails once. The code knows both (it proves a login by reading the
  trailer, and wakes the card after a refusal). Key B of a factory card is plain
  data (it is readable in the trailer) and cannot log in.
- **Elatec TWN4**: it judges a login at once. It numbers the sector of a login
  by the first block of the sector divided by four, which differs from the
  sector number from sector 32 on. This reader has not been tried on a card.

**Before you start:**

- In `hil.settings.json` set `"readerScan": true`. For the three cases that
  write, also set `"cardWrites": true`. It is off in the sample, and a file with
  a mistake in it switches nothing on.
- With both an Elatec reader and a PC/SC reader attached, set `"readerKind"` to
  `"elatec"` or `"pcsc"`. Run the cases once with each reader you use.
- Close every program that uses the reader. With a PC/SC reader the card has to
  be on the reader before you start.
- Do not put a card that holds anything you need on the reader.

**Running the automated cases:**

`Run-HilTests.bat` runs them with everything else, once the settings are on. To
run only these:

```powershell
dotnet test RFiDGear.HIL.Tests `
  --filter "FullyQualifiedName~ClassicWorkflowTests"
```

**The automated cases:**

- **AFactorySector_IsRead_WithTheFactoryKey**: reads sector 1 (4 blocks, the
  trailer last) and, on a 4K, sector 33 (16 blocks). On a smaller chip it reads
  the first sector that the chip does not have and expects a protocol
  constraint, and then a read that works. Needs `readerScan`.
- **AWrongKey_IsRefused_AndTheNextReadWorks**: reads sector 1 with a key that no
  card has (refused), with the factory key (works), with key B alone (refused:
  key B is data on a factory card), with key A alone (works), and with a key
  that is no key (refused before the card). It writes nothing. This is the case
  that proves that the card is woken after a refusal. Needs `readerScan`.
- **ABlock_IsWritten_ReadBack_AndPutBack**: reads sector 1, writes block 5 with
  every byte changed, reads it back, checks that blocks 4 and 6 did not change,
  and puts the old bytes back (also after a step that failed). Needs
  `readerScan` and `cardWrites`.
- **ASector_IsWritten_ReadBack_AndPutBack**: the same for the three data blocks
  of sector 2 in one call and, on a 4K, the 15 data blocks of sector 33. It
  checks that the trailer (the keys and the access bits) did not change. Needs
  `readerScan` and `cardWrites`.
- **AnApplication_IsWrittenThroughTheMad_ReadBack_AndPutBack**: makes a MAD on a
  card that has none (MAD2 on a 4K), writes 20 bytes of the application `1234`
  into sector 1 through it, reads them back through it, and puts the card back:
  the trailers of the MAD sectors (back to the factory keys and the transport
  access bits), the blocks of the directory and the data. The MAD sectors have
  the keys `A0A1A2A3A4A5` and `B0B1B2B3B4B5` for the time the case runs. A card
  that has a MAD already makes the case skip. Needs `readerScan` and
  `cardWrites`.

Every case that writes asks the reader who is on it before it writes and
before it puts back; it compares the answers and never shows them. If the card
was replaced, the case stops, and nothing is written to the second card.

No case writes block 0, a trailer of a sector other than the MAD sectors of the
last case, or a value block, and none writes access bits that can never be
changed again.

**If the MAD case stopped half way:**

The MAD sectors (sector 0, and sector 16 on a 4K) then have the keys
`A0A1A2A3A4A5` and `B0B1B2B3B4B5` and the access bits `787788`. Put the factory
trailer `FFFFFFFFFFFF FF078069 FFFFFFFFFFFF` back to block 3 (and block 67) with
key B `B0B1B2B3B4B5`, for example with the block write of a Classic task: key B
writes the trailer of a MAD sector. Then write zeros to blocks 1 and 2 (and 64
to 66), and to blocks 4 and 5, with the factory key.

**The checklist for cards and steps that are not automated:**

Each item says what to look for. Mark it when you have done it.

- **The TWN4, all five cases.** Not run on a card yet. Expect the same as the
  PC/SC reader. If a refused key leaves the next read failing, the wake (a
  search for the tag) does not select the card on this reader: report it with
  the reader and the card.
- **A card whose keys are not the factory keys.** Set up a read task with the
  keys of the card and read its sectors. Expect: success with the right keys, an
  authentication failure with a wrong key and, in the status text, "Unable to
  Authenticate". With no card on the reader the status text must say that no
  chip answered, and not that a key was refused.
- **A sector whose data blocks have different conditions** (for example the
  MAD sector 787788, where key B writes). Read it with key A only: the blocks
  that key A may read are shown, and a block that no key given may read is
  shown empty. Write a block with key A only: the answer is that the keys are
  not allowed (no card access happens).
- **The sector 0.** Write the data blocks of sector 0 with a task: 32 bytes,
  blocks 1 and 2. Block 0 (the manufacturer block) is never written by any
  call. On a card with a writable block 0 (a "magic" card) it must stay as it
  was.
- **The dialog with a card.** Open the dialog of a Classic task, read sector 33
  of a 4K and see the 16 blocks; read sector 4 and see four. Edit the access
  bits text: a valid text turns green, a text that is none turns red and is not
  taken. Choose the conditions in the boxes and see the text follow them.
- **The MAD in the dialog.** Read an application through the MAD of a card that
  has one (for example an NFC Forum tag, MAD key A `A0A1A2A3A4A5`, or key
  `D3F7D3F7D3F7` for the NDEF sectors). A card that has no MAD ends with the
  answer that the MAD is missing, not with a refused key.
- **A 7-byte UID card and a card with a writable block 0.** Run the five cases.
  Nothing in them should depend on the length of the UID.
- **The chip tree.** Read a Classic card in the main window: the tree has one
  node per sector, and the quick check of the node reads it with the default
  keys of the settings and shows the blocks and the key that worked.
