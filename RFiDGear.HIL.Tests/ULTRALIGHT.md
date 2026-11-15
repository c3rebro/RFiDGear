---
title: Ultralight and NTAG hardware cases
---

These cases check the task for MIFARE Ultralight and NTAG on a real card and
a real reader. Four of them run by themselves (`UltralightWorkflowTests`). The
rest need a card you may not have, and are a checklist below. Nothing here is
claimed to have run on a card until you have run it: the steps of the cases are
checked against readers that hold a chip in memory
(`HilUltralightScenarioSelfCheckTests`), the card is the part that is left.

**Which chips:**

MIFARE Ultralight, Ultralight C, Ultralight EV1 (MF0UL11, MF0UL21) and
NTAG213, NTAG215, NTAG216. A card of another family makes every case skip, and
the skip says which card the reader saw. NTAG210, NTAG211, NTAG212 and NTAG214
are not supported: their memory layout is not confirmed, so both readers
refuse them and nothing is sent to the card.

**What each reader gives:**

- **Elatec TWN4**: every chip above. It reports an Ultralight C like a plain
  Ultralight (Elatec.NET cannot tell them apart yet), so on this reader the
  cases treat a C as a plain Ultralight.
- **PC/SC (LibLogicalAccess)**: the plain Ultralight and the Ultralight C only.
  It has no name for an EV1 or an NTAG (an NTAG215 is "unsupported unknown" to
  it). The task ends with a protocol constraint and sends nothing to the card.
  The cases check exactly that refusal, so "this reader does not support this
  chip" is checked as hard as "it works". A card that this reader has no name
  for is reported with the type "unspecified": the cases then check the
  refusal for it without saying which chip it is (it may be an EV1, an NTAG or
  another card).

**Before you start:**

- Use a card you can spare. A card that is read-only, or whose user pages are
  locked, makes the write case fail (the message says which page).
- In `hil.settings.json` set `"readerScan": true`. For the two cases that write,
  also set `"cardWrites": true`. It is off in the sample, and a file with a
  mistake in it switches nothing on.
- With both an Elatec reader and a PC/SC reader attached, set `"readerKind"`
  to `"elatec"` or `"pcsc"`: without it the Elatec reader always wins. Run the
  cases once with each reader you use. They are two code paths, and they
  differ.
- Close every program that uses the reader. With a PC/SC reader the card has to
  be on the reader before you start; an Elatec reader finds it by itself.
- No case sends a key or a password to a card. In particular nothing tries the
  factory password `FFFFFFFF` of an NTAG: a wrong password counts against the
  number of tries that the chip allows (AUTHLIM), and a chip that has used them
  up refuses the password for good.

**Running the automated cases:**

`Run-HilTests.bat` runs them with everything else, once the settings are on. It
shows the settings it found. To run only these:

```powershell
dotnet test RFiDGear.HIL.Tests `
  --filter "FullyQualifiedName~UltralightWorkflowTests"
```

**The automated cases:**

- **AReadOfTheUserPages_IsAnswered_AsTheReaderAndTheChipAllow**: reads 4 pages
  from page 4, once with the chip left open (any chip) and once with it named.
  Where the reader gives the chip: no error, 4 pages, 16 bytes, and the chip
  that was found. Where it does not (a PC/SC reader and an EV1 or an NTAG): a
  protocol constraint and no page. Needs `readerScan`.
- **AnExpectationThatIsNotTheChip_IsRefused_AndNothingIsRead**: finds out which
  chip it is, then names another chip with a layout. The task ends with a
  protocol constraint and reads nothing. Skips where the reader gives no pages
  of the chip. Needs `readerScan`.
- **AWriteToAUserPage_IsReadBack_AndPutBack**: reads page 10, writes the same
  page with every byte changed, reads it back, and puts the old bytes back
  (also after a write that failed half way, and it says so when it cannot).
  Where the reader refuses the chip it checks that the write is refused and
  nothing is written. Needs `readerScan` and `cardWrites`.
- **AWriteToAProtectedPage_IsRefused_BeforeTheCard**: tries to write page 3
  twice, with the chip named and no opt-in, and with the opt-in and no chip
  named. Both end as a protocol constraint, before a key is asked for or the
  card is touched, and page 3 holds what it held. The bytes are zeros: the
  one-time-programmable bytes are only ever set, never cleared, so even a broken
  refusal would not change the card. Needs `readerScan` and `cardWrites`.

Page 10 is a page of the user memory of every chip of the family (the smallest,
a plain Ultralight, has user pages 4 to 15) and it is not page 4, where an
NTAG keeps the start of its NDEF message. No case writes pages 0 to 2, the lock
pages, the configuration pages, the password, the PACK or a key.

**If a case says a page could not be put back:**

The message gives the four bytes that page 10 held before the case. Write
exactly those bytes to the page, whatever it holds now: if the put-back worked
and only its check failed, it holds them already, and if it did not, it holds
what the case wrote. Do not work the bytes out from what the page holds now.
The bytes are in the message and nowhere else, so use a card you can spare.

Every task selects the card anew, so the case asks the reader who is on it at
the start, before it writes, and before it puts the page back; it compares the
answers and never shows them. If the card was replaced, it stops: before the
write it writes nothing, and before the put-back it writes nothing to the
second card. The message then says that the first card holds changed bytes in
page 10 and which bytes it held: put the first card back and write them.

**The checklist for cards and steps that are not automated:**

Each item says what to look for. Mark it when you have done it.

- **An Ultralight C, authentication with the key.** Not automated: the byte
  order in which the 16-byte key goes to the card is not verified on a card,
  for either reader (Elatec.NET and LibLogicalAccess may differ), and no key is
  offered as a default. With a C card whose key you know (a spare one, with a
  key you set yourself), set up a read task that names the chip (Ultralight C)
  and authenticates with the key. Expect: success with the right key, an
  authentication failure with a wrong one, and no page read in the second case.
  Try both readers, and if the right key fails on one of them, the byte order
  is the cause: report it with the reader.
- **An Ultralight EV1 or an NTAG, authentication with the password and the
  PACK.** Not automated, for the reason above: a wrong password uses up a try.
  Elatec only (a PC/SC reader refuses it as a protocol constraint, which the
  cases already check). Protect a spare card with a password you know and
  AUTHLIM 0 (no limit), outside RFiDGear, then set up a read task that names the
  chip and authenticates with the password and the PACK. Expect: the pages
  behind the protected start page are read with the right pair, and refused with
  a wrong password. Never try the factory password on a card whose AUTHLIM is
  above 0.
- **A plain Ultralight.** Run the four cases with each reader. The write case
  on a PC/SC reader is the one that is not covered by an NTAG.
- **An Ultralight C and an EV1, the four cases.** The cases check the page
  layout of each chip (the C has 48 pages and its key pages cannot be read; an
  EV1 has 20 or 41): run them once with each of these cards and with each
  reader.
- **A card of another family on the reader.** The four cases skip. In the
  application, set up a read task for an Ultralight and run it with a DESFire
  card on the reader: it must end with a protocol constraint, not hang and not
  read.
- **The dialog with a card.** Open the dialog of an Ultralight task, put a card
  on the reader, press Read on the Pages tab and see the pages in the grid; type
  a page, press Write, and press Read again. A box that holds no number, and a
  row that is not 8 hexadecimal digits, turn red and stop the buttons.
- **The chip tree.** Read the card in the main window: the tree has one node per
  page of the chip's layout (an NTAG215 has 135), and the quick check of the
  node shows the pages that a read gives.
