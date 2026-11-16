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
