---
title: MIFARE SAM AV3, first contact with a real SAM (2026-11-15)
---

This is the record of the first session with a real MIFARE SAM AV3 in a PC/SC
reader: what was sent, what the SAM answered, and what we concluded from it. It
is kept so that none of it is lost. The trace below is complete. The only
thing taken out is the UID of the SAM (bytes 14 to 20 of the answer to
SAM_GetVersion), which is shown as `EE`. The keys used are the factory master
key (sixteen zero bytes, public) and a test key that is no secret, so the trace
holds nothing that needs protecting. Times are local.

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
  and a PICC key cannot be proved by a host authentication. It is proved when
  it authenticates a PICC, which is the next task.
- The window itself (search, check, authenticate, write, read-back) has been
  clicked with fakes only; `RFiDGear.HIL.Tests/SAM-AV3.md` is the checklist.
- The TWN4 does not detect the SAM (Elatec.NET issue 28).

**The trace:**

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
