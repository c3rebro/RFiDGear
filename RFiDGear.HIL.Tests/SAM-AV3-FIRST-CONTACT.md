---
title: MIFARE SAM AV3, first sessions with a real SAM (2026-11-15 and 16)
---

This is the record of the first two sessions with a real MIFARE SAM AV3 in a
PC/SC reader: what was sent, what the SAM answered, and what we concluded from
it. It is kept so that none of it is lost. The first session (2026-11-15
evening) set up the SAM. The second (2026-11-15 night) used the SAM with a
DESFire card; it starts at "The second session" below. The traces are
complete, with the exceptions that are named above each of them. What is taken
out is the UID of the SAM (bytes 14 to 20 of the answer to SAM_GetVersion), and
the UID and the production batch of the card; they are shown as `EE`. The keys
used are the factory master key of the SAM and the factory key of the card
(sixteen zero bytes each, public) and a test key that is no secret, so the
traces hold nothing that needs protecting. Times are local.

**The setup:**

- A HID OMNIKEY 5422, the SAM in the contact slot (reader "HID Global OMNIKEY
  5422 Smartcard Reader 0"), reached through LibLogicalAccess (the data
  transport, not the reader card adapter, so that every status word comes
  through). The ATR is
  `3BDF18FF81F1FE43003F07834D49464152452053414D204156333011` (T=1,
  historical bytes "MIFARE SAM AV30").
- A throwaway console that used the same code as the window (`SamReaderFinder`,
  `LlaSamApduChannel`, `Av3HostSession`) and wrote every APDU and answer to a
  file. It is not part of the repository. Every step that changes the SAM
  (activation, key write) ran only after the maintainer had said go for that
  step. The SAM was declared disposable.
- The stages: `observe` (SAM_GetVersion, an instruction that does not exist,
  SAM_AuthenticateHost part 1), `activate`, `authenticate`, `writekey`,
  `readentry`.

**What we found:**

1. A new SAM answers SAM_AuthenticateHost, part 1, with `6985`.
2. SAM_LockUnlock with P1 `03` (the activation), key entry 0, version 0, the
   factory key, in three passes, is accepted: `90AF`, `90AF`, `9000`, and the
   16 bytes of proof that the SAM returns with the last answer check out. The
   frame that came from an AV2 library works on an AV3.
3. Before and after the activation only one thing changed in the answer to
   SAM_GetVersion: its **last byte went from `03` to `A3`** (bits `0x80` and
   `0x20` set). What that byte means is not known. SAM_AuthenticateHost part 1
   went from `6985` to `90AF` with a 12-byte random number.
4. After the activation the host authentication with the factory master key
   works (`90AF`, `90AF`, `9000`, mutual proof checked).
5. **The activation is persistent.** After the SAM was taken out of the slot
   and put back, the last byte of the version was still `A3` and part 1 still
   answered `90AF`. No way back was found, and none was tried; it is treated as
   irreversible. In NXP's own words for the SAM AV3 the SAM is "activated"
   (the AV2 data sheet speaks of a switch from AV1 to AV2 mode; nothing we saw
   says that this is that), so the word stays.
6. SAM_ChangeKeyEntry for entry 1 (AES-128, key class PICC, version 0, the
   master key as the key that may change it, no key usage counter) answered
   `9000` with a CMAC that checked out.
7. SAM_GetKeyEntry (`80 64 <entry> 00 00`, under the secure session) answers
   `9000` and 13 bytes: key versions A, B, C (3), DF_AID (3), DF_KeyNo,
   KeyNoCEK, KeyVCEK, RefNoKUC, SET (2 bytes, low byte first), ExtSET. The
   order is inferred from what was written. It holds no key. Read back:

   | Entry | Answer | SET | ExtSET |
   |---|---|---|---|
   | 1 (written) | `000000000000000000FF200001` | `0x0020` | `01` |
   | 0 (master) | `000000000000000000FF200000` | `0x0020` | `00` |
   | 2 (blank) | `000000000000000000FF200200` | `0x0220` | `00` |

8. **A blank entry is disabled.** NXP AN12701, table 2, page 15 (read by the
   maintainer): SET bit 9 (`0x0200`) is "disable key entry", and a
   SAM_ChangeKeyEntry that clears it enables the entry. The blank entry has it
   set, the written entry does not. SET bits 3 to 5 are the key type (`100b` is
   AES-128), and SET says nothing about what the key is for: the key class is
   in the low bits of ExtSET (`001b` PICC, `000b` host). The entry that we
   wrote is enabled, AES-128 and PICC. The setup reads this back after every
   write.
9. The master entry has no key usage counter (RefNoKUC `FF`), so a wrong key
   counts against nothing. SET bit 10 (SAM_LockUnlock needed after every
   reset) is clear, which fits finding 5. SET bit 8 (the library's "host
   authentication key") is clear as well, and host authentication works.

**What went wrong, and what it taught:**

- The first try of the key write (20:27:42) did nothing: the stage had ended
  its "before" check with SAM_AuthenticateHost part 1, which leaves the SAM
  waiting for part 2, and the real authentication that followed was answered
  `6700` (wrong length). Never send part 1 right before a real authentication
  on the same SAM. The window does not: it asks SAM_GetVersion only.
- A plain command inside an authenticated session is refused: SAM_GetVersion
  right after the write answered `6A84`. A new connection (no reset needed)
  showed the SAM normal again.

**What is still open:**

- Whether the activation can be undone, and what the last byte of
  SAM_GetVersion means.
- That the key in entry 1 is the key that was typed: a key cannot be read back
  and a PICC key cannot be proved by a host authentication. Answered by the
  second session: it is, and it authenticates a PICC (but see finding 9 of the
  second session).
- The window itself (search, check, authenticate, write, read-back) has been
  clicked with fakes only; `RFiDGear.HIL.Tests/SAM-AV3.md` is the checklist.
- The TWN4 does not detect the SAM (Elatec.NET issue 28).

**The trace of the first session:**

```text
20:07:52.848  ##### stage: observe  (trace file C:\tmp\sam-session\trace-202
    61115.txt)
20:07:53.861  search: enumerationFailed=False readers=1
20:07:53.864  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
20:07:54.008  === observe: now
20:07:54.011  -> 8060000000
20:07:54.039  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    003 SW=9000   (UID bytes 14..20 shown as EE)
20:07:54.041  -> 80FF000000
20:07:54.045  <- SW=6D00
20:07:54.046  -> 80A400000300000200
20:07:54.051  <- SW=6985
20:07:54.053  ##### end of stage observe
20:23:30.222  ##### stage: activate  (trace file C:\tmp\sam-session\trace-20
    261115.txt)
20:23:31.140  search: enumerationFailed=False readers=1
20:23:31.143  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
20:23:31.266  === observe: BEFORE the activation
20:23:31.271  -> 8060000000
20:23:31.302  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    003 SW=9000   (UID bytes 14..20 shown as EE)
20:23:31.304  -> 80FF000000
20:23:31.310  <- SW=6D00
20:23:31.311  -> 80A400000300000200
20:23:31.318  <- SW=6985
20:23:31.319  === SAM_LockUnlock (activation), key entry 0, version 0, facto
    ry key
20:23:31.323  -> 8010030005000000000000
20:23:31.331  <- 9E16F12C1A2A9B282069E01D SW=90AF
20:23:31.347  -> 8010000014B06F920E55DA02E879E98BF3C984CF16CDF85E0300
20:23:31.390  <- 0A4A8E0142447DB5561BB010FDD36A9B5F40F81ACD70CA7F SW=90AF
20:23:31.392  -> 8010000020376DBF5F79F8DF3056A3846DCFF3979DD42D30F819DAE216D
    BB7A0C3C42924E500
20:23:31.429  <- 12F67889452AF53CFA1F4F01D1B3618D SW=9000
20:23:31.431  RESULT activation call returned without an exception
20:23:31.432  === observe: AFTER the activation
20:23:31.433  -> 8060000000
20:23:31.462  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
20:23:31.463  -> 80FF000000
20:23:31.469  <- SW=6D00
20:23:31.470  -> 80A400000300000200
20:23:31.476  <- C8BC0A5FA8458B3F78DE9C7A SW=90AF
20:23:31.477  ##### end of stage activate
20:23:49.397  ##### stage: authenticate  (trace file C:\tmp\sam-session\trac
    e-20261115.txt)
20:23:50.313  search: enumerationFailed=False readers=1
20:23:50.317  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
20:23:50.438  === host authentication, key entry 0, version 0, factory key
20:23:50.444  -> 80A400000300000200
20:23:50.474  <- 38835CCCC28458A7E9592429 SW=90AF
20:23:50.487  -> 80A400001405FD241816091692487ADB6061A2B2C95BEFFC3E00
20:23:50.511  <- BE1379A645B5954745026A7D6DF88908C7DBC8AC17A66781 SW=90AF
20:23:50.513  -> 80A400002000252B08954762393708C41A26D51A5ACDF7A5A8B35109B2F
    9542312EC92B93300
20:23:50.528  <- 343C5045212814689AD2574FF4848B46 SW=9000
20:23:50.530  RESULT authenticated=True
20:23:50.531  ##### end of stage authenticate
20:26:14.584  ##### stage: observe  (trace file C:\tmp\sam-session\trace-202
    61115.txt)
20:26:15.506  search: enumerationFailed=False readers=1
20:26:15.509  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
20:26:15.652  === observe: now
20:26:15.655  -> 8060000000
20:26:15.685  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
20:26:15.687  -> 80FF000000
20:26:15.691  <- SW=6D00
20:26:15.692  -> 80A400000300000200
20:26:15.699  <- 089338E87D0481600F656512 SW=90AF
20:26:15.700  ##### end of stage observe
20:27:42.812  ##### stage: writekey  (trace file C:\tmp\sam-session\trace-20
    261115.txt)
20:27:43.728  search: enumerationFailed=False readers=1
20:27:43.732  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
20:27:43.849  === observe: BEFORE the key write
20:27:43.852  -> 8060000000
20:27:43.880  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
20:27:43.882  -> 80FF000000
20:27:43.888  <- SW=6D00
20:27:43.889  -> 80A400000300000200
20:27:43.895  <- FAABA6DF9F4901F03ED882FE SW=90AF
20:27:43.897  === host authentication (factory key), then SAM_ChangeKeyEntry
    : key entry 1, AES-128 PICC key (a test key that is no secret), key vers
    ion 0
20:27:43.901  -> 80A400000300000200
20:27:43.905  <- SW=6700
20:27:43.909  RESULT SamException SW=6700 type=SamException
20:27:43.910  === observe: AFTER the key write
20:27:43.911  -> 8060000000
20:27:43.918  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
20:27:43.919  -> 80FF000000
20:27:43.923  <- SW=6D00
20:27:43.924  -> 80A400000300000200
20:27:43.930  <- DDF04328475878E6AECDB4F8 SW=90AF
20:27:43.931  ##### end of stage writekey
20:28:10.786  ##### stage: writekey  (trace file C:\tmp\sam-session\trace-20
    261115.txt)
20:28:11.726  search: enumerationFailed=False readers=1
20:28:11.729  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
20:28:11.870  === observe: BEFORE the key write (without SAM_AuthenticateHos
    t part 1: it leaves the SAM waiting for part 2)
20:28:11.873  -> 8060000000
20:28:11.902  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
20:28:11.904  -> 80FF000000
20:28:11.909  <- SW=6D00
20:28:11.910  === host authentication (factory key), then SAM_ChangeKeyEntry
    : key entry 1, AES-128 PICC key (a test key that is no secret), key vers
    ion 0
20:28:11.914  -> 80A400000300000200
20:28:11.922  <- 1EA3C7FEFB76E8023C095FAA SW=90AF
20:28:11.934  -> 80A40000143F3AA7F27ED8944560E531D90FFE04032EF19F3D00
20:28:11.951  <- 80D32D7D3DA73C5EE34E507D8B5AB07710C859CCE141B8E3 SW=90AF
20:28:11.954  -> 80A4000020A0F221EEDC8169A6F9D70F7EBEE73CA0456FCCEBB407481F8
    FBF953F9DB9205B00
20:28:11.969  <- B2B4B6F3E1337D34CE80C3AC888C7749 SW=9000
20:28:11.971  authenticated=True
20:28:11.977  -> 80C101FF48CB7D79E2191BE3DC6FD55AC9882A48A4C6187EE9F6799A6D8
    58761020BCC565CACF7796CCA31BD53919D825CBD5A4F8BF8AD69593EC1DA9B99A01632F
    1537BF2665E73358DE9819D
20:28:12.019  <- D38E7D7DDB86BE49 SW=9000
20:28:12.022  RESULT key entry 1 written: the SAM answered success and the C
    MAC of its answer checked out
20:28:12.023  === observe: AFTER the key write
20:28:12.025  -> 8060000000
20:28:12.029  <- SW=6A84   (UID bytes 14..20 shown as EE)
20:28:12.030  -> 80FF000000
20:28:12.035  <- SW=6D00
20:28:12.036  -> 80A400000300000200
20:28:12.064  <- 2A0FA5E08C6EA45B1A372AC5 SW=90AF
20:28:12.066  ##### end of stage writekey
20:28:28.052  ##### stage: observe  (trace file C:\tmp\sam-session\trace-202
    61115.txt)
20:28:28.964  search: enumerationFailed=False readers=1
20:28:28.967  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
20:28:29.083  === observe: now
20:28:29.086  -> 8060000000
20:28:29.115  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
20:28:29.117  -> 80FF000000
20:28:29.121  <- SW=6D00
20:28:29.122  -> 80A400000300000200
20:28:29.128  <- 016824584F1781D08DD42B53 SW=90AF
20:28:29.129  ##### end of stage observe
20:31:47.013  ##### stage: readentry  (trace file C:\tmp\sam-session\trace-2
    0261115.txt)
20:31:47.945  search: enumerationFailed=False readers=1
20:31:47.950  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
20:31:48.067  === observe: BEFORE the read (without SAM_AuthenticateHost par
    t 1: it leaves the SAM waiting for part 2)
20:31:48.070  -> 8060000000
20:31:48.098  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
20:31:48.100  -> 80FF000000
20:31:48.106  <- SW=6D00
20:31:48.107  === host authentication (factory key), then SAM_GetKeyEntry (I
    NS 0x64, P1 = key entry, P2 = 0x00, Le = 0x00): read-only, the answer ho
    lds the settings of an entry and no key
20:31:48.112  -> 80A400000300000200
20:31:48.118  <- 52164D5D9880A2A009F399B0 SW=90AF
20:31:48.131  -> 80A4000014745E5ABA75560F7E1DD5C24E82DB97C2D9DF7A2900
20:31:48.148  <- A9FDB4A022A44B8B13F8A7DB2F788471BA75C3D912777762 SW=90AF
20:31:48.150  -> 80A4000020EEB68D9A85AF0F3AE6A0FBEF90AD7A0228146600135685234
    0A8F24272630C2900
20:31:48.185  <- 73376B8940D8262F7B0011612193BED2 SW=9000
20:31:48.188  authenticated=True
20:31:48.190  -> 8064010008E5A1C9B8DB2828F800
20:31:48.206  <- F3D716C4FECE74E3A6C6A0BF823FE5DB117635AEE08F973A SW=9000
20:31:48.209  RESULT entry 1: 13 bytes: 000000000000000000FF200001
20:31:48.211  -> 8064000008528190077BE88D2600
20:31:48.226  <- 16AED66B352AED5AA5F33AA7F01B46F2506BED7515A8CE83 SW=9000
20:31:48.228  RESULT entry 0: 13 bytes: 000000000000000000FF200000
20:31:48.229  -> 8064020008DE044989388B21F800
20:31:48.245  <- CF5F4AFDD48669DE37D74DDEEEFAC6237D442CCC42A756C3 SW=9000
20:31:48.246  RESULT entry 2: 13 bytes: 000000000000000000FF200200
20:31:48.248  ##### end of stage readentry
```

**The second session: a DESFire card through the SAM:**

The same SAM, now activated, and a spare MIFARE DESFire EV3 (8 KB) in the
contactless slot of the same reader ("HID Global OMNIKEY 5422CL Smartcard
Reader 0", ATR `3B8180018080`; LibLogicalAccess says "DESFire" before it
connects to the card and "DESFireEV3" after). The SAM sits in the contact slot,
so the PC is the go-between for both (NXP calls that the S interface). The
maintainer said go for the steps ("start the whole process and stop only on
errors") and later asked whether the SAM can authenticate the PICC with the same
key slot that changed its key. The throwaway console got more stages:
`desfire-probe` (read only), `writekey2`, `layout-check` (offline, no reader),
`desfire-sam-auth` (the SAM support of LibLogicalAccess), `picc-auth`,
`picc-change`, `picc-auth-aes` and `fixkey1`. For the PICC steps our own code
sends the SAM frames, and the card is reached through the data transport of
LibLogicalAccess too, so that every APDU on both sides is in the trace. The
instruction codes and the option bits come from NXP's public application note
AN12696 ("MIFARE SAM AV3 - For MIFARE DESFire") and from NXP's old reader
library for the SAM AV2 (public mirrors of it).

**What we found in the second session:**

1. **The card.** Read only, no authentication: GetVersion answers
   `04010233001A05`, `04010103001A05` and the UID with the production data
   (masked): a DESFire EV3 with 8192 bytes. It is in its factory state:
   GetKeySettings `0F01` (master key changeable, directory listing and
   create/delete free, configuration changeable, one key of type DES/2K3DES),
   key 0 at version 0, free memory `002000` (8192 bytes: no application).
2. **Elatec.NET writes AES-128 key entries only.** Its `ChangeKeyEntryAsync`
   throws `NotSupportedException` for any other type, before it sends
   anything. The frame of SAM_ChangeKeyEntry (INS `C1`, P1 the entry, P2 `FF`,
   under the secure session) was therefore built by us. Its data field is: key
   A, key B, key C (16 bytes each; the library puts a copy of key A into B and
   C), DF_AID (3), DF_KeyNo, KeyNoCEK, KeyVCEK, RefNoKUC, SET (2 bytes, low
   byte first), version A, B, C, ExtSET. **That is not the order of the answer
   to SAM_GetKeyEntry**, where the versions come first. The layout was checked
   offline, with no reader: with the same session keys and counter the frame of
   the library for an AES entry and ours are equal byte for byte, and
   `ExecuteSecureAsync` sends the same frame as `ChangeKeyEntryAsync`. The
   library sends no Le byte (our first comparison had one, and the MAC
   differed).
3. **The key type is in SET bits 3 to 5** (LibLogicalAccess's table and NXP
   AN12696, table 1): `000` DESFire native TDEA (DES and 2K3DES, 16 bytes),
   `011` 3TDEA ISO 10116, `100` AES-128, `110` TDEA ISO 10116 (32-bit CRC,
   8-byte MAC). Entry 2 was written with the factory key of the card (sixteen
   zero bytes) as a DES/2K3DES key of the class PICC: SET `0x0000`, ExtSET `01`.
   The SAM answered `9000` with a CMAC that checked out, and the same session
   read the entry back as written. That is the read-back that the setup window
   does after every write, now seen on hardware.
4. **The SAM support of LibLogicalAccess does not take this SAM.** With the
   name of the SAM reader set in the configuration of the card reader,
   connecting fails. Type `SAM_AV3` or `SAM_AUTO`: "The SAM Detected is not
   the SAM waited." Type `SAM_AV1` or `SAM_AV2`: "SAM on the reader is not the
   same type as selected." With the default `SAM_NONE` a key with SAM key
   storage is refused: "SAMKeyStorage set on the key but no SAM reader has been
   set." Its own trace could not be switched on (guesses at
   `LibLogicalAccess.config` did nothing and were removed again). Hence our
   own frames.
5. **SAM_AuthenticatePICC works, with the DES key of entry 2, at the first
   try.** Card: `90 0A 00 00 01 00 00` (native authentication, key 0) answers
   `91AF` and E(RndB). SAM part 1: `80 0A 00 00 <Lc> <entry> <version>
   <E(RndB)> 00` answers `90AF` and E(RndA, RndB'). P1 is `00`: no key
   diversification, the key is chosen by the SAM key entry number (the same
   for every card command, `0A`, `AA`; for EV2 `71` AN12696 uses `80`). Card:
   `90 AF 00 00 <Lc> <that> 00` answers `9100` and E(RndA'). SAM part 2: `80 0A
   00 00 <Lc> <E(RndA')>` (no Le) answers `9000`. **No host authentication is
   needed**: the SAM was in a fresh connection, nothing but the PICC
   authentication was sent.
6. **SAM_ChangeKeyPICC works.** After finding 5: `80 C4 01 10 04 <current
   entry> <current version> <new entry> <new version> 00` answers 24 bytes and
   `9000`. P1 bit 0 is set because the key that is changed is the one that was
   authenticated with, P2 bit 4 because it is the card master key. Card: `90 C4
   00 00 <Lc> 80 <cryptogram> 00` answers `9100`; the key number byte `80` is
   key 0 with a new key of type AES (`00` is DES, `40` is 3K3DES). AN12696,
   section 2.7.1 ("TDES native to AES") has the same frame. The key settings of
   the card changed from `0F01` to `0F81`: its master key is now AES.
7. **The key is the typed key.** The maintainer authenticated to the PICC level
   of the card himself, from the host side, with the test key that was typed
   for entry 1. So the bytes in entry 1 are the typed bytes, and
   SAM_ChangeKeyPICC put exactly that key on the card.
8. **The AES authentication through entry 1 failed with `901E`** at part 2,
   twice (right after the change, and later in a fresh connection). The card
   accepted the SAM's part 1 (it answered `9100`), so the keys were equal; the
   SAM did not accept the answer of the card.
9. **The cause is SET bit 2, "Keep IV".** NXP AN12696, table 1, "SAM key entry
   setting for different MIFARE DESFire keys": for a DESFire AES key (and for
   3TDEA and for TDEA ISO) b2 "Keep IV" must be `1`, for a native TDEA key it
   must be `0`, and "the incorrect setting will result to authentication
   error". Entry 1 had SET `0x0020`, as `SamAv3KeyEntry.CreateAes128PiccKey`
   of Elatec.NET and the setup window write it: AES-128 and no Keep IV. The
   examples of AN12696 use SET "2400" (bytes low first: `0x0024`). The DES
   entry 2 (SET `0x0000`) was right from the start, which is why the first
   authentication worked.
10. **Fixed and proved.** Entry 1 was rewritten with the same key and SET
    `0x0024` (`9000`; read back as `0x0024`, ExtSET `01`). In a fresh
    connection the AES authentication through entry 1 then worked: part 1
    `90AF`, the card `9100`, part 2 `9000`. **The SAM can authenticate the PICC
    with the same key slot that changed the key of the card.**

**What went wrong, and what it taught (second session):**

- The first try of `writekey2` stopped at the library: `NotSupportedException`
  (finding 2). Nothing had been sent to the SAM.
- A key that the SAM accepts and the card accepts can still fail at the last
  step, the check of the card's answer, and the status word `901E` does not say
  why. NXP's table 1 did. Check the SET bits against it before looking for a
  fault in the frames.
- Switching on the own trace of LibLogicalAccess was tried at several places for
  `LibLogicalAccess.config`; none worked. The files were removed again.

**What the second session means for the application:**

- **The setup window writes a key that a DESFire card cannot use.** It writes
  SET `0x0020`; a DESFire AES key needs `0x0024`. The SAM accepts the entry, the
  read-back says "enabled, AES-128, PICC class", and the card's authentication
  then fails at part 2. The write has to set Keep IV, and the read-back has to
  check it. The helper of Elatec.NET writes the same value.
- A DES or 2K3DES key of a card (the factory key) is an entry with SET
  `0x0000`, and Elatec.NET cannot write it: the frame of finding 2 is needed.
- SAM_ChangeKeyPICC is allowed by the entries we wrote: SET bit 11 "disable
  SAM_ChangeKeyPICC" is clear (NXP sets it for check-in and check-out
  terminals). The other bits of AN12696, table 1: b0 allow dumping the session
  key (up to the application), b7 PL key `0`, b8 host authentication key `0`,
  b9 disable entry `0`, b10 lock key `0`; ExtSET b2..b0 `001` (PICC key), b3
  allow dumping the secret key (not recommended, `0`), b4 restricted for
  diversification (strongly recommended `1`; not set here, the examples of
  AN12696 use ExtSET `01`).
- Using a SAM key needs both readers: native commands to the card in the
  contactless slot and SAM commands to the SAM in the contact slot, with the PC
  relaying. A task cannot be tied to one reader any more.

**What is still open (after the second session):**

- Untried: the other authentication modes (ISO `1A` with 3K3DES, EV2 `71`,
  P1 `80` and `C0`), key diversification, an application key (by DESFire key
  number, with SAM_SelectApplication), SAM_KillAuthentication, using the session
  with the SAM (reading and writing files), a second authentication on the same
  SAM connection after SAM_ChangeKeyPICC.
- Whether the activation can be undone, and what the last byte of
  SAM_GetVersion means (from the first session).
- The window itself has not been clicked with a real SAM, and the TWN4 does not
  detect the SAM (Elatec.NET issue 28).

**The trace of the second session:**

Complete, except two things. The first runs of the offline `layout-check` (they
compared frames that differed, because of the Le byte, see finding 2) are left
out; the last run, which shows equal frames, is in. And two runs of the probe
that only tested whether LibLogicalAccess writes its own log are left out (they
show what the first run shows).

```text
23:40:43.474  ##### stage: desfire-probe  (trace file C:\tmp\sam-session\tra
    ce-20261115.txt)
23:40:43.505  === DESFire probe: read-only (no authentication, nothing writt
    en, UID and batch masked, the SAM reader is not opened)
23:40:43.801  reader 'HID Global OMNIKEY 5422 Smartcard Reader 0': ATR 3BDF1
    8FF81F1FE43003F07834D49464152452053414D204156333011 chip type seen by Li
    bLogicalAccess before any connect: SAM_AV3
23:40:43.803    not a candidate (a SAM, or no ATR)
23:40:43.811  reader 'HID Global OMNIKEY 5422CL Smartcard Reader 0': ATR 3B8
    180018080 chip type seen by LibLogicalAccess before any connect: DESFire
23:40:43.814  reader 'Windows Hello for Business 1': left alone (a virtual r
    eader)
23:40:43.902  connected to the card in 'HID Global OMNIKEY 5422CL Smartcard 
    Reader 0'; chip type after connect: DESFireEV3
23:40:43.908  --- UID (pseudo-APDU of the reader)
23:40:43.910  -> FFCA000000
23:40:43.919  <- EEEEEEEEEEEEEE SW=9000   (UID/batch shown as EE)
23:40:43.920  --- DESFire GetVersion
23:40:43.922  -> 9060000000
23:40:43.926  <- 04010233001A05 SW=91AF
23:40:43.928  -> 90AF000000
23:40:43.933  <- 04010103001A05 SW=91AF
23:40:43.935  -> 90AF000000
23:40:43.939  <- EEEEEEEEEEEEEEEEEEEEEEEEC825 SW=9100   (UID/batch shown as 
    EE)
23:40:43.941  SUMMARY hardware: vendor 0x04, type 0x01, subtype 0x02, versio
    n 51.0 => DESFire EV3; storage code 0x1A = 8192 bytes; protocol 0x05
23:40:43.942  --- SelectApplication 000000 (the card's own level)
23:40:43.945  -> 905A00000300000000
23:40:43.950  <- SW=9100
23:40:43.951  --- GetKeySettings of the card's level
23:40:43.953  -> 9045000000
23:40:43.957  <- 0F01 SW=9100
23:40:43.958  SUMMARY key settings 0x0F: master key changeable=True, directo
    ry listing without the master key=True, create/delete without the master
     key=True, configuration changeable=True; byte 1 = 0x01: keys=1, key typ
    e DES / 2K3DES   (decoded from the DESFire EV1 documentation by memory)
23:40:43.960  --- GetKeyVersion of key 0
23:40:43.961  -> 90640000010000
23:40:43.966  <- 00 SW=9100
23:40:43.968  --- GetFreeMemory
23:40:43.969  -> 906E000000
23:40:43.974  <- 002000 SW=9100
23:48:56.613  ##### stage: writekey2  (trace file C:\tmp\sam-session\trace-2
    0261115.txt)
23:48:56.971  search: enumerationFailed=False readers=1
23:48:56.975  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
23:48:57.098  === observe: BEFORE the key write (entry 2) (without SAM_Authe
    nticateHost part 1: it leaves the SAM waiting for part 2)
23:48:57.102  -> 8060000000
23:48:57.132  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
23:48:57.134  -> 80FF000000
23:48:57.140  <- SW=6D00
23:48:57.142  === host authentication (factory key), then SAM_ChangeKeyEntry
    : key entry 2 = the factory key of the DESFire card (sixteen zero bytes,
     public) as a DES/2K3DES key, PICC class, version 0
23:48:57.147  -> 80A400000300000200
23:48:57.155  <- 2E23B5B0CB3F417D33518160 SW=90AF
23:48:57.170  -> 80A4000014ED6DDFFC683351B7D2A84856BCF4F36B309AA0D000
23:48:57.187  <- 885946BAF74D8E5B85AE4D688E7F16BEA37119DC75D71CEE SW=90AF
23:48:57.191  -> 80A40000207E9A8F013E7654E10CF3C0BED4EF7C0FE9948B1F9B7D2955C
    B5939F00933AAA400
23:48:57.206  <- 56ECBA5AA40DB5C0127D2378B5EFE974 SW=9000
23:48:57.209  authenticated=True
23:48:57.215  -> 80640200088799F1D5CA325F9400
23:48:57.233  <- CF3F0EEDA860ECFB18F3280807FAA29EBDFDA256C6932EB6 SW=9000
23:48:57.238  entry 2 before: SET=0x0220 ExtSET=0x00 enabled=False RefNoKuc=
    0xFF
23:48:57.240  template: SET=0x0020 ExtSET=0x01 RefNoKuc=0xFF KeyNoCek=0 KeyA
     length 16
23:48:57.243  sending SET=0x0000 (key type DES/2K3DES), ExtSET=0x01
23:48:57.247  STOPPED: NotSupportedException
23:54:35.331  ##### stage: layout-check  (trace file C:\tmp\sam-session\trac
    e-20261115.txt)
23:54:35.357  === layout check (OFFLINE: no reader, no SAM): my plaintext fo
    r SAM_ChangeKeyEntry against what the library sends
23:54:35.362  entry: KeyA 16 KeyB  KeyC  DfAid 3 SET=0x0020 ExtSET=0x01 RefN
    oKuc=0xFF KeyNoCek=0 KeyVCek=0 VersionA=0
23:54:35.383  (the library refused the made-up answer after the capture, as 
    expected: SamException)
23:54:35.387  library : 80C102FF4814E3F5328224C312D8EBEAB846FC83983D524B8342
    8984D6066EC4BFA13DC88A79E7517C31CA64601F25D0044A2A41431DB0CD0C2B1829B888
    E3B7F5D5214384DB042838CBCDDBF3
23:54:35.391  plaintext (61 bytes), B and C zero
23:54:35.393  mine    : 80C102FF4814E3F5328224C312D8EBEAB846FC8398E14158F55E
    6445925655C6D70165F2FF65AD33C188B582BEE97ECA1F279C7193550E7C96FD46A4DEED
    EECD050C17EC2D56A7C3E0B61FA537
23:54:35.394  different with B and C zero
23:54:35.397  plaintext (61 bytes), B and C copies of A
23:54:35.399  mine    : 80C102FF4814E3F5328224C312D8EBEAB846FC83983D524B8342
    8984D6066EC4BFA13DC88A79E7517C31CA64601F25D0044A2A41431DB0CD0C2B1829B888
    E3B7F5D5214384DB042838CBCDDBF3
23:54:35.401  RESULT: IDENTICAL with B and C copies of A: the layout is righ
    t
23:54:35.404  (refused the made-up answer after the capture: SamException)
23:54:35.406  RESULT: ExecuteSecureAsync sends the SAME frame as ChangeKeyEn
    tryAsync
23:56:17.733  ##### stage: writekey2  (trace file C:\tmp\sam-session\trace-2
    0261115.txt)
23:56:18.046  search: enumerationFailed=False readers=1
23:56:18.050  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
23:56:18.167  === observe: BEFORE the key write (entry 2) (without SAM_Authe
    nticateHost part 1: it leaves the SAM waiting for part 2)
23:56:18.171  -> 8060000000
23:56:18.202  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
23:56:18.205  -> 80FF000000
23:56:18.210  <- SW=6D00
23:56:18.213  === host authentication (factory key), then SAM_ChangeKeyEntry
    : key entry 2 = the factory key of the DESFire card (sixteen zero bytes,
     public) as a DES/2K3DES key, PICC class, version 0. The frame is built 
    here (the library only writes AES-128 entries) and was checked offline a
    gainst the library's own frame.
23:56:18.218  -> 80A400000300000200
23:56:18.226  <- 40EBCEEE5E95FE2B44338265 SW=90AF
23:56:18.239  -> 80A4000014D945ED8A02375753B8950B50115A9106CBBF005500
23:56:18.257  <- C5F5733EF816B6A6E8F60D756EA33467E4A1259289AE6DCC SW=90AF
23:56:18.261  -> 80A4000020A802981A84BDFC0945755E8CA2BB6F208852329A0BC14B19F
    CEFB3AFDAAC0BE400
23:56:18.276  <- AEA68166ADF1DD160DB5DE76A58C570E SW=9000
23:56:18.280  authenticated=True
23:56:18.283  -> 8064020008CCAAFF9E93D1B40100
23:56:18.300  <- BB9D23A2AB8152B6058E0FC59EBE2EAFE98F76423CDEA4AD SW=9000
23:56:18.304  entry 2 before: SET=0x0220 ExtSET=0x00 enabled=False RefNoKuc=
    0xFF
23:56:18.307  sending SET=0x0000 (key type DES/2K3DES), ExtSET=0x01, plainte
    xt 61 bytes (public factory key, so it is shown): 0000000000000000000000
    000000000000000000000000000000000000000000000000000000000000000000000000
    00000000000000FF000000000001
23:56:18.310  -> 80C102FF489086D1A7491814D6B0E8566928F3809F70CDF0637BF55E6A2
    1FDCEF0A67CB46579EAE1C2A9D2B560FFDEF95B79635798BF0F066797703B45CC05BDA78
    E5AC2F2C9D34E2C17F1F3DA
23:56:18.355  <- B6B1F5FA86F0F104 SW=9000
23:56:18.357  RESULT key entry 2 written: the SAM answered success and the C
    MAC of its answer checked out
23:56:18.359  -> 806402000890D0073EA27E553800
23:56:18.374  <- 74C50DB8E78EF00DB5BCF71282DF66E679E3A20AF71111FD SW=9000
23:56:18.377  entry 2 read back in the same session: SET=0x0000 ExtSET=0x01 
    enabled=True aes128=False class=1 RefNoKuc=0xFF raw=000000000000000000FF
    000001
23:56:18.379  === observe: AFTER the key write (entry 2) (without SAM_Authen
    ticateHost part 1: it leaves the SAM waiting for part 2)
23:56:18.381  -> 8060000000
23:56:18.387  <- SW=6A84   (UID bytes 14..20 shown as EE)
23:56:18.389  -> 80FF000000
23:56:18.394  <- SW=6D00
23:56:18.396  ##### end of stage writekey2
23:59:25.020  ##### stage: desfire-sam-auth  (trace file C:\tmp\sam-session\
    trace-20261115.txt)
23:59:25.048  === DESFire authentication THROUGH THE SAM with LibLogicalAcce
    ss: card key = SAM key entry 2 (DES/2K3DES, the factory key). An authent
    ication only: nothing is written to the card or the SAM by this stage.
23:59:25.328  card reader: 'HID Global OMNIKEY 5422CL Smartcard Reader 0'; S
    AM reader: 'HID Global OMNIKEY 5422 Smartcard Reader 0'
23:59:25.333  configured: SAM type SAM_AV3, SAM reader name set, auto connec
    t off, use SAM authenticate host=False
00:00:09.516  ##### stage: desfire-sam-auth  (trace file C:\tmp\sam-session\
    trace-20261116.txt)
00:00:09.539  === DESFire authentication THROUGH THE SAM with LibLogicalAcce
    ss: card key = SAM key entry 2 (DES/2K3DES, the factory key). An authent
    ication only: nothing is written to the card or the SAM by this stage.
00:00:09.796  card reader: 'HID Global OMNIKEY 5422CL Smartcard Reader 0'; S
    AM reader: 'HID Global OMNIKEY 5422 Smartcard Reader 0'
00:00:09.798  default SAM type of the configuration: SAM_NONE
00:00:09.802  configured: SAM type SAM_NONE, SAM reader name set, auto conne
    ct off, use SAM authenticate host=False
00:00:09.901  card connected; chip type DESFireEV3
00:00:09.904  connectToSAM returned; SAM chip (none)
00:00:09.906  commands object: DESFireEV3ISO7816Commands
00:00:09.912  key storage: SAM, slot 2, absolute=True, dump key=False
00:00:09.921  RESULT authenticate(0, SAM key slot 2) FAILED: LibLogicalAcces
    sException: SAMKeyStorage set on the key but no SAM reader has been set.
00:01:07.609  ##### stage: desfire-sam-auth  (trace file C:\tmp\sam-session\
    trace-20261116.txt)
00:01:07.632  === DESFire authentication THROUGH THE SAM with LibLogicalAcce
    ss: card key = SAM key entry 2 (DES/2K3DES, the factory key). An authent
    ication only: nothing is written to the card or the SAM by this stage.
00:01:07.883  card reader: 'HID Global OMNIKEY 5422CL Smartcard Reader 0'; S
    AM reader: 'HID Global OMNIKEY 5422 Smartcard Reader 0'
00:01:07.885  default SAM type of the configuration: SAM_NONE; asked for: SA
    M_AV2
00:01:07.888  configured: SAM type SAM_AV2, SAM reader name set, auto connec
    t off, use SAM authenticate host=False
00:01:07.989  STOP: connecting the card (and with it the SAM) FAILED: LibLog
    icalAccessException: SAM on the reader is not the same type as selected.
00:01:08.233  ##### stage: desfire-sam-auth  (trace file C:\tmp\sam-session\
    trace-20261116.txt)
00:01:08.257  === DESFire authentication THROUGH THE SAM with LibLogicalAcce
    ss: card key = SAM key entry 2 (DES/2K3DES, the factory key). An authent
    ication only: nothing is written to the card or the SAM by this stage.
00:01:08.510  card reader: 'HID Global OMNIKEY 5422CL Smartcard Reader 0'; S
    AM reader: 'HID Global OMNIKEY 5422 Smartcard Reader 0'
00:01:08.512  default SAM type of the configuration: SAM_NONE; asked for: SA
    M_AUTO
00:01:08.514  configured: SAM type SAM_AUTO, SAM reader name set, auto conne
    ct off, use SAM authenticate host=False
00:01:08.580  STOP: connecting the card (and with it the SAM) FAILED: LibLog
    icalAccessException: The SAM Detected is not the SAM waited.
00:01:08.838  ##### stage: desfire-sam-auth  (trace file C:\tmp\sam-session\
    trace-20261116.txt)
00:01:08.860  === DESFire authentication THROUGH THE SAM with LibLogicalAcce
    ss: card key = SAM key entry 2 (DES/2K3DES, the factory key). An authent
    ication only: nothing is written to the card or the SAM by this stage.
00:01:09.118  card reader: 'HID Global OMNIKEY 5422CL Smartcard Reader 0'; S
    AM reader: 'HID Global OMNIKEY 5422 Smartcard Reader 0'
00:01:09.120  default SAM type of the configuration: SAM_NONE; asked for: SA
    M_AV1
00:01:09.123  configured: SAM type SAM_AV1, SAM reader name set, auto connec
    t off, use SAM authenticate host=False
00:01:09.188  STOP: connecting the card (and with it the SAM) FAILED: LibLog
    icalAccessException: SAM on the reader is not the same type as selected.
00:10:04.481  ##### stage: picc-auth  (trace file C:\tmp\sam-session\trace-2
    0261116.txt)
00:10:04.496  ##### PICC authentication through the SAM, own frames. mode=pi
    cc-auth
00:10:04.977  CARD -> 9045000000   GetKeySettings (PICC level)
00:10:04.982  CARD <- 0F01 SW=9100
00:10:04.985  --- authenticate the card with key 0 through SAM key entry 2 (
    native DES/2K3DES, the factory key)
00:10:04.987  CARD -> 900A0000010000   part 1 (native authenticate, key 0)
00:10:04.997  CARD <- 5E1394ADB9CC39BF SW=91AF
00:10:05.001  -> 800A00000A02005E1394ADB9CC39BF00
00:10:05.034  <- 115CFFC2B0B32102069C552569785F21 SW=90AF
00:10:05.036  CARD -> 90AF000010115CFFC2B0B32102069C552569785F2100   part 2 
    (additional frame: the SAM's encrypted RndA and RndB')
00:10:05.043  CARD <- 4260826A832C9977 SW=9100
00:10:05.044  -> 800A0000084260826A832C9977
00:10:05.054  <- SW=9000
00:10:05.056  RESULT: MUTUALLY AUTHENTICATED: the card accepted the SAM's pa
    rt 1 and the SAM accepted the card's part 2 (key entry 2)
00:10:05.057  ##### end of picc-auth: authenticated, nothing was changed
00:10:31.049  ##### stage: picc-change  (trace file C:\tmp\sam-session\trace
    -20261116.txt)
00:10:31.064  ##### PICC authentication through the SAM, own frames. mode=pi
    cc-change
00:10:31.546  CARD -> 9045000000   GetKeySettings (PICC level)
00:10:31.552  CARD <- 0F01 SW=9100
00:10:31.555  --- authenticate the card with key 0 through SAM key entry 2 (
    native DES/2K3DES, the factory key)
00:10:31.556  CARD -> 900A0000010000   part 1 (native authenticate, key 0)
00:10:31.567  CARD <- A28FE7F3D9DF565D SW=91AF
00:10:31.571  -> 800A00000A0200A28FE7F3D9DF565D00
00:10:31.604  <- AFF31419CBD2C9119EF5CD3BB391559B SW=90AF
00:10:31.606  CARD -> 90AF000010AFF31419CBD2C9119EF5CD3BB391559B00   part 2 
    (additional frame: the SAM's encrypted RndA and RndB')
00:10:31.613  CARD <- 10F4214A80834985 SW=9100
00:10:31.615  -> 800A00000810F4214A80834985
00:10:31.625  <- SW=9000
00:10:31.626  RESULT: MUTUALLY AUTHENTICATED: the card accepted the SAM's pa
    rt 1 and the SAM accepted the card's part 2 (key entry 2)
00:10:31.628  --- SAM_ChangeKeyPICC: the PICC master key (key 0) becomes the
     AES-128 key of SAM key entry 1, version 0
00:10:31.629  -> 80C40110040200010000
00:10:31.641  <- 48B85C5A34BAA8E5844FE136C61347C19D00CB62FF69C242 SW=9000
00:10:31.642  CARD -> 90C40000198048B85C5A34BAA8E5844FE136C61347C19D00CB62FF
    69C24200   ChangeKey (key number 0x80 = PICC master key, new key type AE
    S) with the SAM's cryptogram
00:10:31.670  CARD <- SW=9100
00:10:31.672  RESULT: the card accepted the ChangeKey
00:10:31.673  CARD -> 9045000000   GetKeySettings after the change
00:10:31.677  CARD <- 0F81 SW=9100
00:10:31.678  --- authenticate the card with key 0 through SAM key entry 1 (
    AES-128, the new key from the SAM)
00:10:31.680  CARD -> 90AA0000010000   part 1 (native authenticate, key 0)
00:10:31.686  CARD <- 432EAE12FC5FA9D3D1F6098BA6FA0F65 SW=91AF
00:10:31.688  -> 800A0000120100432EAE12FC5FA9D3D1F6098BA6FA0F6500
00:10:31.701  <- 91011084304D4F9F54B55C0046E7471CF60638DA92B8A6609DC259BFF36
    967A1 SW=90AF
00:10:31.703  CARD -> 90AF00002091011084304D4F9F54B55C0046E7471CF60638DA92B8
    A6609DC259BFF36967A100   part 2 (additional frame: the SAM's encrypted R
    ndA and RndB')
00:10:31.711  CARD <- E44B28EB24B398660C89FF61562B36CF SW=9100
00:10:31.713  -> 800A000010E44B28EB24B398660C89FF61562B36CF
00:10:31.720  <- SW=901E
00:10:31.721  STOP: SAM_AuthenticatePICC part 2 answered SW=901E: the card's
     answer was not accepted
00:28:15.773  ##### stage: picc-auth-aes  (trace file C:\tmp\sam-session\tra
    ce-20261116.txt)
00:28:15.788  ##### PICC authentication through the SAM, own frames. mode=pi
    cc-auth-aes
00:28:16.261  CARD -> 9045000000   GetKeySettings (PICC level)
00:28:16.267  CARD <- 0F81 SW=9100
00:28:16.270  --- authenticate the card with key 0 through SAM key entry 1 (
    AES-128, the key that the SAM put on the card)
00:28:16.271  CARD -> 90AA0000010000   part 1 (native authenticate, key 0)
00:28:16.281  CARD <- 825E2B1A71603BDBB2D13C3847A058D9 SW=91AF
00:28:16.285  -> 800A0000120100825E2B1A71603BDBB2D13C3847A058D900
00:28:16.319  <- 7DF26238BCF20992A84C9FCBF87E425F25CF92F858EB49872C8B9DBA887
    87822 SW=90AF
00:28:16.321  CARD -> 90AF0000207DF26238BCF20992A84C9FCBF87E425F25CF92F858EB
    49872C8B9DBA8878782200   part 2 (additional frame: the SAM's encrypted R
    ndA and RndB')
00:28:16.329  CARD <- E5CC3B822AFC44F353469088176F5C4A SW=9100
00:28:16.330  -> 800A000010E5CC3B822AFC44F353469088176F5C4A
00:28:16.338  <- SW=901E
00:28:16.339  STOP: SAM_AuthenticatePICC part 2 answered SW=901E: the card's
     answer was not accepted
00:34:50.769  ##### stage: fixkey1  (trace file C:\tmp\sam-session\trace-202
    61116.txt)
00:34:51.084  search: enumerationFailed=False readers=1
00:34:51.087  ATR 3BDF18FF81F1FE43003F07834D49464152452053414D204156333011
00:34:51.209  === observe: BEFORE rewriting entry 1 (Keep IV) (without SAM_A
    uthenticateHost part 1: it leaves the SAM waiting for part 2)
00:34:51.212  -> 8060000000
00:34:51.241  <- 0401010500230104010105011F01EEEEEEEEEEEEEE20253900001801160
    0A3 SW=9000   (UID bytes 14..20 shown as EE)
00:34:51.243  -> 80FF000000
00:34:51.248  <- SW=6D00
00:34:51.249  === host authentication (factory key), then SAM_ChangeKeyEntry
    : key entry 1 REWRITTEN with the same test key and SET 0x0020 -> 0x0024 
    (SET b2 'Keep IV' set: NXP AN12696 Table 1 asks for it for a DESFire AES
     key)
00:34:51.253  -> 80A400000300000200
00:34:51.260  <- 4C22580E2CB070416CE403FF SW=90AF
00:34:51.273  -> 80A400001461026ED18097C0E34D7C6AA68141CE59675936DA00
00:34:51.290  <- A5B9E8DE88BA1A34F3B5938168A0377DFA539E21EC568C19 SW=90AF
00:34:51.293  -> 80A40000200B9B08BBD8F5AC93A3563851A6D7FB8362325FC8F4B196A17
    AB7EF7B6EB2810F00
00:34:51.308  <- 932BF7107318E62722BB47474D11B6ED SW=9000
00:34:51.310  authenticated=True
00:34:51.313  -> 8064010008C2B045F76EC552FD00
00:34:51.329  <- 599A509F86825D47B1BD80FE094DFA5FAED19A300EA80F7A SW=9000
00:34:51.333  entry 1 before: SET=0x0020 ExtSET=0x01 enabled=True aes128=Tru
    e class=1
00:34:51.336  sending SET=0x0024 ExtSET=0x01 (the same test key, version 0)
00:34:51.339  -> 80C101FF48E9467153C870608C7171688DD6DE399D2638D499FB3911E1E
    4E906C1D3187F9F145FFAAB43DF6DF125E69D7158951677AE145E019C36697762A9A99C9
    25ED514CB92B9E0133677BB
00:34:51.381  <- FF35BB29C233BA6D SW=9000
00:34:51.383  RESULT key entry 1 rewritten: the SAM answered success and the
     CMAC of its answer checked out
00:34:51.385  -> 8064010008846E544B4A3077C100
00:34:51.399  <- DDA6F9ED6809A9D8159F2038E6BBED9444DF94466ABED22E SW=9000
00:34:51.401  entry 1 read back in the same session: SET=0x0024 ExtSET=0x01 
    enabled=True aes128=True class=1 raw=000000000000000000FF240001
00:34:51.402  ##### end of stage fixkey1
00:34:59.508  ##### stage: picc-auth-aes  (trace file C:\tmp\sam-session\tra
    ce-20261116.txt)
00:34:59.523  ##### PICC authentication through the SAM, own frames. mode=pi
    cc-auth-aes
00:35:00.002  CARD -> 9045000000   GetKeySettings (PICC level)
00:35:00.008  CARD <- 0F81 SW=9100
00:35:00.011  --- authenticate the card with key 0 through SAM key entry 1 (
    AES-128, the key that the SAM put on the card)
00:35:00.013  CARD -> 90AA0000010000   part 1 (native authenticate, key 0)
00:35:00.026  CARD <- A527BE71077E8F118D203B4DFA7E5414 SW=91AF
00:35:00.031  -> 800A0000120100A527BE71077E8F118D203B4DFA7E541400
00:35:00.066  <- 2E771D4EDCE7E566A1FD15D00D800113BFF57BBB99A6F1A1B147181305C
    B4832 SW=90AF
00:35:00.069  CARD -> 90AF0000202E771D4EDCE7E566A1FD15D00D800113BFF57BBB99A6
    F1A1B147181305CB483200   part 2 (additional frame: the SAM's encrypted R
    ndA and RndB')
00:35:00.077  CARD <- 0C8B2C57D6D32D7B8FD274B707D68AE9 SW=9100
00:35:00.079  -> 800A0000100C8B2C57D6D32D7B8FD274B707D68AE9
00:35:00.087  <- SW=9000
00:35:00.089  RESULT: MUTUALLY AUTHENTICATED: the card accepted the SAM's pa
    rt 1 and the SAM accepted the card's part 2 (key entry 1)
```
