---
title: MIFARE SAM AV3, what the PICC authentication needs (2026-11-16)
---

This is the record of one hardware session with a real MIFARE SAM AV3 and a
spare DESFire EV3: what a PC that relays between the SAM and the card has to
know besides the frames of the authentication and of the key change, which are
in `SAM-AV3-FIRST-CONTACT.md`. It is kept so that none of it is lost, and it
backs the code of `SamPiccSession`. The trace below is complete (only the
authentications that were made; nothing was written to the SAM or to the card).
The keys are the AES test key that `SAM-AV3-FIRST-CONTACT.md` names, held in
the SAM and never seen by the PC. Times are local.

**The setup:**

- The SAM in the contact slot and the card in the contactless slot of the same
  HID OMNIKEY 5422. The SAM key entry 1 holds the AES test key with SET
  `0x0024`; the PICC master key of the card is that key (key settings `0F81`).
- A throwaway console that sends the frames of `SamPiccFrames` itself (the SAM
  through the data transport of LibLogicalAccess, the card through the data
  transport of its own reader unit) and writes every APDU of both sides to a
  file. The maintainer said go ("continue with the next pr until done") for a
  session that only authenticates.
- The stages: `kill-test` (run twice: the second time with a select between the
  steps, which is the one that answers the questions below) and
  `kill-test-after`.

**What we found:**

1. **SAM_KillAuthentication is `80 CA <P1> 00`, with no Lc and no Le.** P1 `00`
   kills every authentication of the logical channel, P1 `01` only the PICC
   authentication; both answered `9000` on a SAM with nothing to kill. With an
   Le (`80 CA 00 00 00`) the SAM answers `6700`, and `6883` while a PICC
   authentication is half done. The application kills only the PICC
   authentication (P1 `01`): the SAM is not host-authenticated in that session.
2. **A SAM that waits for part 2 turns the next part 1 into a part 2.** After a
   part 1 that was not finished, a second part 1 (with a new challenge of the
   card) was answered `6700`, the status of a part 2 of the wrong length. The
   kill (without Le) answered `9000`, and the next part 1 was answered `90AF`,
   as a part 1 again. So the session kills before it starts, and again after a
   part 1 that was not finished.
3. **Part 2 may or may not have an Le.** `80 0A 00 00 10 <data>` and the same
   with a trailing `00` both answered `9000`. The application sends none.
4. **A card that was left with an unfinished authentication answers the next
   command with `91CA`** (command aborted), whatever the command is, and is
   clean afterwards. The first command of a session is a SelectApplication, and
   it may get this answer: the select is sent once more. It changes nothing on
   the card; this is the only command that the session ever sends twice.
5. **Closing the connection resets the SAM.** A part 1 was left dangling and
   both connections were closed; on a new connection the next part 1 was
   answered `90AF` and the authentication completed. A new connection starts
   clean, with or without a kill.
6. **Opening the SAM costs about 120 ms** (reading the ATR and connecting), so
   it is opened for one operation and not kept.
7. The authentication through the AES entry with Keep IV set worked every time
   (`90AF`, `9100`, `9000`), with and without an Le at part 2.

**What is still open:**

- The key change of an application key (the P1 and P2 of a key other than the
  one that authenticated), AES to AES with a new version, AES to DES, and an
  authentication after a change on the same SAM connection.
- 2K3DES keys with two different halves, 3K3DES, the ISO and EV2 modes of the
  authentication, key diversification.
- The window and the commands of the application with a real SAM.

**The trace:**

```text
02:11:36.226  ##### stage: kill-test  (trace file C:\tmp\sam-session\trace-2
    0261116.txt)
02:11:36.240  ##### kill-test: SAM_KillAuthentication, a dangling authentica
    tion, and a closed connection (authentications only)
02:11:36.679  COST: opening the SAM took 116 ms (ReadAtr + connect)
02:11:36.741  --- 1. SAM_KillAuthentication frames on a clean SAM
02:11:36.744  SAM  kill P1=00 no Le
02:11:36.749  -> 80CA0000
02:11:36.776  <- SW=9000
02:11:36.780  SAM  kill P1=00 Le 00
02:11:36.781  -> 80CA000000
02:11:36.786  <- SW=6700
02:11:36.788  SAM  kill P1=01 (PICC only) no Le
02:11:36.790  -> 80CA0100
02:11:36.795  <- SW=9000
02:11:36.797  SAM  kill P1=01 (PICC only) Le 00
02:11:36.798  -> 80CA010000
02:11:36.803  <- SW=6700
02:11:36.806  --- 2. a normal AES authentication through entry 1, part 2 wit
    hout Le
02:11:36.810  CARD -> 90AA0000010000   ref card part 1
02:11:36.821  CARD <- AA79B23EF82A8DB1FB3F519877AD312B SW=91AF
02:11:36.825  SAM  ref SAM part 1
02:11:36.826  -> 800A0000120100AA79B23EF82A8DB1FB3F519877AD312B00
02:11:36.840  <- CB40C7867F86FF1C0BC7FDA8267663CED1B1083E0F248D1AC40B8C0F59A
    0F37F SW=90AF
02:11:36.848  CARD -> 90AF000020CB40C7867F86FF1C0BC7FDA8267663CED1B1083E0F24
    8D1AC40B8C0F59A0F37F00   ref card part 2
02:11:36.856  CARD <- D8139B2B79A90037E14D5FA392FB3369 SW=9100
02:11:36.858  SAM  ref SAM part 2
02:11:36.860  -> 800A000010D8139B2B79A90037E14D5FA392FB3369
02:11:36.868  <- SW=9000
02:11:36.871  RESULT 2: authenticated
02:11:36.872  --- 3. part 2 with Le 00
02:11:36.874  CARD -> 90AA0000010000   le card part 1
02:11:36.881  CARD <- EBE35FE84E0DF1A5815D4AD51A350189 SW=91AF
02:11:36.883  SAM  le SAM part 1
02:11:36.884  -> 800A0000120100EBE35FE84E0DF1A5815D4AD51A35018900
02:11:36.898  <- 8975501F1CD3E2C50B83724A2944AD2012EB4DB9E4CC938EC6B590E71B1
    C1EB7 SW=90AF
02:11:36.900  CARD -> 90AF0000208975501F1CD3E2C50B83724A2944AD2012EB4DB9E4CC
    938EC6B590E71B1C1EB700   le card part 2
02:11:36.908  CARD <- D230B2E1DAA115D9F87DA08BE4377479 SW=9100
02:11:36.910  SAM  le SAM part 2 (with Le 00)
02:11:36.912  -> 800A000010D230B2E1DAA115D9F87DA08BE437747900
02:11:36.920  <- SW=9000
02:11:36.922  RESULT 3: authenticated with Le 00
02:11:36.923  --- 4. SAM part 1 twice without part 2
02:11:36.925  CARD -> 90AA0000010000   dangling-1 card part 1
02:11:36.932  CARD <- E21E5EC4173D6631973703E6AB5B9ADA SW=91AF
02:11:36.934  SAM  dangling-1 SAM part 1
02:11:36.935  -> 800A0000120100E21E5EC4173D6631973703E6AB5B9ADA00
02:11:36.949  <- 1A0DC2E67CC8A6B606CF02CD5F44DEE1B06C2593E81156C9C6AF35E333F
    8FF79 SW=90AF
02:11:36.951  RESULT 4a: first part 1 SW 90AF
02:11:36.952  CARD -> 90AA0000010000   dangling-2 card part 1
02:11:36.957  CARD <- SW=91CA
02:11:36.959  RESULT 4b: second part 1 (new card challenge) SW FFFFFFFF = th
    e SAM waits for part 2 or refuses
02:11:36.961  --- 5. kill, then a full authentication
02:11:36.963  SAM  kill P1=00 Le 00
02:11:36.964  -> 80CA000000
02:11:36.969  <- SW=6883
02:11:36.971  SAM  kill P1=00 no Le
02:11:36.972  -> 80CA0000
02:11:36.978  <- SW=9000
02:11:36.980  RESULT 5a: kill SW 9000
02:11:36.981  CARD -> 90AA0000010000   after-kill card part 1
02:11:36.988  CARD <- 1E0AEC20BD7F6AE6741B625A2ADF01B7 SW=91AF
02:11:36.991  SAM  after-kill SAM part 1
02:11:36.993  -> 800A00001201001E0AEC20BD7F6AE6741B625A2ADF01B700
02:11:37.007  <- 879E8C73BB90BD04F4FB5260D03E73D7556F6AF28F3336F3D057ADDD8BA
    FD63B SW=90AF
02:11:37.009  CARD -> 90AF000020879E8C73BB90BD04F4FB5260D03E73D7556F6AF28F33
    36F3D057ADDD8BAFD63B00   after-kill card part 2
02:11:37.017  CARD <- 8617F5FA1F21A5D1D3919980021978F3 SW=9100
02:11:37.018  SAM  after-kill SAM part 2
02:11:37.020  -> 800A0000108617F5FA1F21A5D1D3919980021978F3
02:11:37.028  <- SW=9000
02:11:37.030  RESULT 5b: authenticated after the kill
02:11:37.032  --- 6. leave a dangling part 1 and close both connections
02:11:37.034  CARD -> 90AA0000010000   closing card part 1
02:11:37.041  CARD <- C5861ECAE76021494FF857DDFCF70F0F SW=91AF
02:11:37.043  SAM  closing SAM part 1
02:11:37.044  -> 800A0000120100C5861ECAE76021494FF857DDFCF70F0F00
02:11:37.058  <- EE5AA0BABB4A74AF0CC69A724A94D1F50CCAB3273A2F7621367DA6486EE
    5A0B8 SW=90AF
02:11:37.060  RESULT 6: dangling part 1 SW 90AF; the connection is closed no
    w; run `kill-test-after` next
02:12:05.193  ##### stage: kill-test  (trace file C:\tmp\sam-session\trace-2
    0261116.txt)
02:12:05.206  ##### kill-test: SAM_KillAuthentication, a dangling authentica
    tion, and a closed connection (authentications only)
02:12:05.635  COST: opening the SAM took 117 ms (ReadAtr + connect)
02:12:05.698  --- 1. SAM_KillAuthentication frames on a clean SAM
02:12:05.701  SAM  kill P1=00 no Le
02:12:05.706  -> 80CA0000
02:12:05.733  <- SW=9000
02:12:05.736  SAM  kill P1=00 Le 00
02:12:05.738  -> 80CA000000
02:12:05.743  <- SW=6700
02:12:05.745  SAM  kill P1=01 (PICC only) no Le
02:12:05.747  -> 80CA0100
02:12:05.752  <- SW=9000
02:12:05.754  SAM  kill P1=01 (PICC only) Le 00
02:12:05.756  -> 80CA010000
02:12:05.761  <- SW=6700
02:12:05.763  --- 2. a normal AES authentication through entry 1, part 2 wit
    hout Le
02:12:05.766  CARD -> 90AA0000010000   ref card part 1
02:12:05.778  CARD <- 4A021BD9DFE6BF2EB40F44000ECEF69F SW=91AF
02:12:05.781  SAM  ref SAM part 1
02:12:05.783  -> 800A00001201004A021BD9DFE6BF2EB40F44000ECEF69F00
02:12:05.797  <- F64E1FFE039890DCF9F0C00A9DE10DAB3ACC9DAAFFD260A9207992EB59D
    99A76 SW=90AF
02:12:05.802  CARD -> 90AF000020F64E1FFE039890DCF9F0C00A9DE10DAB3ACC9DAAFFD2
    60A9207992EB59D99A7600   ref card part 2
02:12:05.811  CARD <- 5E9404FAB76BC6771582D471450F52E2 SW=9100
02:12:05.813  SAM  ref SAM part 2
02:12:05.814  -> 800A0000105E9404FAB76BC6771582D471450F52E2
02:12:05.823  <- SW=9000
02:12:05.825  RESULT 2: authenticated
02:12:05.827  --- 3. part 2 with Le 00
02:12:05.829  CARD -> 90AA0000010000   le card part 1
02:12:05.836  CARD <- 2BD001A2967B78BE3326772547F0C980 SW=91AF
02:12:05.838  SAM  le SAM part 1
02:12:05.840  -> 800A00001201002BD001A2967B78BE3326772547F0C98000
02:12:05.854  <- 7659530F8AC9DB0CA92807EB6B708B82F6D5B08483C7666FC1F994EE4CA
    39986 SW=90AF
02:12:05.856  CARD -> 90AF0000207659530F8AC9DB0CA92807EB6B708B82F6D5B08483C7
    666FC1F994EE4CA3998600   le card part 2
02:12:05.864  CARD <- E7B684C4301E72E792DD90292435F5C1 SW=9100
02:12:05.866  SAM  le SAM part 2 (with Le 00)
02:12:05.868  -> 800A000010E7B684C4301E72E792DD90292435F5C100
02:12:05.876  <- SW=9000
02:12:05.878  RESULT 3: authenticated with Le 00
02:12:05.880  --- 4. SAM part 1 twice without part 2
02:12:05.882  CARD -> 90AA0000010000   dangling-1 card part 1
02:12:05.889  CARD <- 19B41647E8B372FF798E491AD1E6A73A SW=91AF
02:12:05.891  SAM  dangling-1 SAM part 1
02:12:05.893  -> 800A000012010019B41647E8B372FF798E491AD1E6A73A00
02:12:05.907  <- 830AC8B1EE198D92C8046E88D07E48527481CF12FDC04B4E775E192D443
    130DD SW=90AF
02:12:05.909  RESULT 4a: first part 1 SW 90AF
02:12:05.911  CARD -> 905A00000300000000   select PICC level (resets the car
    d side)
02:12:05.916  CARD <- SW=91CA
02:12:05.920  CARD -> 90AA0000010000   dangling-2 card part 1
02:12:05.927  CARD <- 8A0321E3251899207F5F0563F96EE60F SW=91AF
02:12:05.929  SAM  dangling-2 SAM part 1
02:12:05.931  -> 800A00001201008A0321E3251899207F5F0563F96EE60F00
02:12:05.938  <- SW=6700
02:12:05.940  RESULT 4b: second part 1 (new card challenge) SW 6700 = the SA
    M waits for part 2 or refuses
02:12:05.942  --- 5. kill, then a full authentication
02:12:05.944  SAM  kill P1=00 Le 00
02:12:05.945  -> 80CA000000
02:12:05.951  <- SW=6700
02:12:05.953  SAM  kill P1=00 no Le
02:12:05.955  -> 80CA0000
02:12:05.960  <- SW=9000
02:12:05.962  RESULT 5a: kill SW 9000
02:12:05.964  CARD -> 90AA0000010000   after-kill card part 1
02:12:05.968  CARD <- SW=91CA
02:12:05.971  RESULT 5b: FAILED (SAM part 1 SW FFFFFFFF)
02:12:05.973  --- 6. leave a dangling part 1 and close both connections
02:12:05.975  CARD -> 905A00000300000000   select PICC level (resets the car
    d side)
02:12:05.979  CARD <- SW=9100
02:12:05.981  CARD -> 90AA0000010000   closing card part 1
02:12:05.988  CARD <- C6D340E66F5DAD06547E30F297E72898 SW=91AF
02:12:05.991  SAM  closing SAM part 1
02:12:05.992  -> 800A0000120100C6D340E66F5DAD06547E30F297E7289800
02:12:06.006  <- EB1EBF08E4864E6DA074F5D6C15914779DFE46A384DA240BA1EE6AF508D
    0FD50 SW=90AF
02:12:06.008  RESULT 6: dangling part 1 SW 90AF; the connection is closed no
    w; run `kill-test-after` next
02:12:06.261  ##### stage: kill-test-after  (trace file C:\tmp\sam-session\t
    race-20261116.txt)
02:12:06.274  ##### kill-test-after: a new connection after a dangling part 
    1 (authentications only)
02:12:06.705  CARD -> 905A00000300000000   select PICC level (resets the car
    d side)
02:12:06.712  CARD <- SW=9100
02:12:06.716  CARD -> 90AA0000010000   new-connection card part 1
02:12:06.724  CARD <- 7D6F5A81FE7C002C360A0A595F89B819 SW=91AF
02:12:06.728  SAM  new-connection SAM part 1
02:12:06.731  -> 800A00001201007D6F5A81FE7C002C360A0A595F89B81900
02:12:06.767  <- 1985E0890C4F2A1E21F502695DB994D8D69BA0B28E6523D7113D3E4FD46
    AFA53 SW=90AF
02:12:06.773  CARD -> 90AF0000201985E0890C4F2A1E21F502695DB994D8D69BA0B28E65
    23D7113D3E4FD46AFA5300   new-connection card part 2
02:12:06.781  CARD <- B626A64FC633A3476550A7D29B4C65E5 SW=9100
02:12:06.784  SAM  new-connection SAM part 2
02:12:06.786  -> 800A000010B626A64FC633A3476550A7D29B4C65E5
02:12:06.794  <- SW=9000
02:12:06.797  RESULT 7: SAM part 1 on a new connection SW 90AF and the authe
    ntication completed: closing the connection reset the SAM
```

**The TWN4 (2026-11-17): the SAM and the card on one reader:**

The same relay on **one TWN4** instead of a pair of PC/SC readers: the SAM
in socket 1 (SAM1) and the spare DESFire EV3 on the antenna of the same
reader. Hardware revision G (TWN4 MultiTech 2), firmware
`TWN4/B1.64/NCF5.07/PRS1.04/PIMBAV`, USB serial (PID 0420). The card held its
factory DES key at key 0 (key type DES, key settings free create and delete
without the master key). **Authentication only: no key was changed, nothing
was written to the SAM or to the card.** First with a throwaway console
(spike, the second run is traced below), then with the classes of the
application (`ElatecTwn4Ports`, `Twn4SamRelay.Parts`, `SamRelay.RunAsync`) on
the same reader and card.

What was found:

- **The frames of the PC/SC relay go to the card unchanged** through
  `ISO14443_4_Tdx` of Elatec.NET: `90 5A` (select) gives `9100`, `90 0A`
  (native authentication, part 1) gives `91AF` and the eight bytes of the
  challenge, `90 AF` (part 2) gives `9100`. The reader does the framing of
  ISO 14443-4. The first frame of GetVersion is nine bytes and ends with
  `91AF`.
- **`Twn4SamApduChannel` sends no Le for a command that has none:**
  `80 CA 01 00` (SAM_KillAuthentication) gives `9000`, and so does the second
  part of the authentication (`80 0A 00 00 08 ...`).
- **The SAM stays `PresentAndActive` across a search for a card** on the same
  reader: nothing in the search resets it.
- **A blank key entry (127) is refused by the SAM with `6985`** after the card
  accepted part 1; the kill that follows gives `9000`, and the next
  authentication starts clean.
- **PICC master key 0 authenticates through SAM entry 2** (native DES, the
  factory key of the card): the card and the SAM accepted each other, in about
  200 ms (350 ms for a whole relay of the application, which includes the
  search, the look at the card and the power cycle of the SAM). The first
  select after the refusal above answered `91CA`, the card's answer to a
  command after an unfinished authentication; the session sent it once more,
  as designed.
- **The library's own DESFire calls work afterwards:** a new search for a
  card, then GetKeySettings through Elatec.NET gives the key settings of the
  card as before (the key type is DES).
- An AES authentication on this card is refused by the card at once (`91AE`,
  the card holds a DES key): the case ends as `CardRefusedBeforeSam`.

What has not been tried on a TWN4: a key change, AES keys and application
keys (the spare card holds a DES key), and the task commands in the
application window. `SAM-PICC.md` has the cases.

The trace (the second run of the spike, then the two runs of the classes of
the application; the SAM is entry 2 for the card key and entry 127 for the
blank one, nothing here is secret):

```text
19:55:17.419  ##### stage: twn4-relay  (trace file C:\tmp\sam-session\trace-
    20261117.txt)
19:55:17.432  ##### twn4-relay on COM8 (authentication only, no key change,
    nothing is written)
19:55:17.500  connect: True
19:55:17.543  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011 A
    V3=True
19:55:17.555  === H2: SAM_KillAuthentication without Le (80 CA 01 00)
19:55:17.558  -> 80CA0100
19:55:17.597  <- SW=9000
19:55:17.598  H2 RESULT SW=9000 (no Le was sent)
19:55:17.599  === H3: a tag search on the same reader, with the SAM powered
19:55:17.606  slot status before: IccStatus PresentAndActive, CommandStatus
    0, Error 0, ClockStatus Running
19:55:17.706  tag: chip type MIFARE, id bits 56 (UID not shown)
19:55:17.710  slot status after: IccStatus PresentAndActive, CommandStatus 0
    , Error 0, ClockStatus Running
19:55:17.711  === card key settings BEFORE (library, after a fresh select of
     the PICC level)
19:55:17.810  tag: MIFARE
19:55:17.836  key settings through the library: AccessRights=KS_FREE_CREATE_
    DELETE_WITHOUT_MK, KS_DEFAULT, NumberOfKeys=1, KeyType=DF_KEY_DES
19:55:17.838  === H4a: blank key entry 127 (native): the card accepts part 1
    , the SAM is expected to refuse (kill frame follows)
19:55:17.844  -> 80CA0100
19:55:17.870  <- SW=9000
19:55:17.874  CARD -> 905A00000300000000
19:55:17.886  CARD <- 9100
19:55:17.889  CARD -> 900A0000010000
19:55:17.905  CARD <- 97F64C437DF25D3691AF
19:55:17.908  -> 800A00000A7F0097F64C437DF25D3600
19:55:17.950  <- SW=6985
19:55:17.952  -> 80CA0100
19:55:17.980  <- SW=9000
19:55:17.983  H4a RESULT Error=KeyUnavailable Failure=SamRefused SAM=6985 ca
    rd=0000 keyMayHaveChanged=False  (271 ms)
19:55:17.984  === H4b: PICC master key 0 with SAM entry 2 (native DES, the f
    actory key of the card)
19:55:17.985  -> 80CA0100
19:55:18.010  <- SW=9000
19:55:18.011  CARD -> 905A00000300000000
19:55:18.024  CARD <- 91CA
19:55:18.026  CARD -> 905A00000300000000
19:55:18.035  CARD <- 9100
19:55:18.036  CARD -> 900A0000010000
19:55:18.050  CARD <- 3971593AC97A9FDE91AF
19:55:18.051  -> 800A00000A02003971593AC97A9FDE00
19:55:18.120  <- 8F06E47C4AB1E2805FAFA082E9AF4AC5 SW=90AF
19:55:18.123  CARD -> 90AF0000108F06E47C4AB1E2805FAFA082E9AF4AC500
19:55:18.140  CARD <- 08EB5BEC296D93AE9100
19:55:18.141  -> 800A00000808EB5BEC296D93AE
19:55:18.185  <- SW=9000
19:55:18.186  H4b RESULT Error=NoError Failure=None SAM=0000 card=0000 keyMa
    yHaveChanged=False  (202 ms)
19:55:18.187  === H5: the library's own DESFire calls after a raw relay (a f
    resh tag search first)
19:55:18.285  tag: MIFARE
19:55:18.310  key settings through the library: AccessRights=KS_FREE_CREATE_
    DELETE_WITHOUT_MK, KS_DEFAULT, NumberOfKeys=1, KeyType=DF_KEY_DES
19:55:18.315  power off SAM1
20:29:49.397  ##### stage: twn4-prod  (trace file C:\tmp\sam-session\trace-2
    0261117.txt)
20:29:49.410  ##### twn4-prod on COM8 (production relay, authentication only
    )
20:29:49.474  connect: True
20:29:49.832  PROD blank entry 127: Error=KeyUnavailable Failure=SamRefused
    SAM=6985 card=0000 keyMayHaveChanged=False  (355 ms)
20:29:50.204  PROD entry 2, native DES: Error=NoError Failure=None SAM=0000
    card=0000 keyMayHaveChanged=False  (370 ms)
20:29:50.210  slot status after: IccStatus PresentAndInactive, CommandStatus
     0, Error 0, ClockStatus Running
21:06:30.478  ##### stage: twn4-prod  (trace file C:\tmp\sam-session\trace-2
    0261117.txt)
21:06:30.491  ##### twn4-prod on COM8 (production relay, authentication only
    )
21:06:30.555  connect: True
21:06:30.906  PROD blank entry 127: Error=KeyUnavailable Failure=SamRefused
    SAM=6985 card=0000 keyMayHaveChanged=False  (348 ms)
21:06:31.264  PROD entry 2, native DES: Error=NoError Failure=None SAM=0000
    card=0000 keyMayHaveChanged=False  (356 ms)
21:06:31.274  slot status after: IccStatus PresentAndInactive, CommandStatus
     0, Error 0, ClockStatus Running
```
