---
title: RFiDGear.Bridge.SampleClient
---

A small client of the external control bridge of RFiDGear, to read and to try.
It is plain `net10.0`, references only `RFiDGear.Bridge.Contracts` and
StreamJsonRpc, and does what the client guide
(`RFiDGear.Bridge.Contracts/CLIENT-GUIDE.md`) asks for:

- `BridgeSampleClient` is the client: the pipe of the current user (opened
  with `CurrentUserOnly`), the error data of the bridge made readable, the
  handshake and the capability check, `RunOnceAsync` (starts a run and **never
  repeats it as a new request**: a start that is not answered is asked about
  with the same request id), `ResumeAsync` (the way back after a lost
  connection or a restart), `CatchUpAsync` (the results a client missed), and
  `KeyVault`, the hook where a program puts its own vault.
- `Program` is a command line around it. Start RFiDGear with the setting
  "external control" on, or with the argument `EXTERNALCONTROL=1`, then:

```text
RFiDGear.Bridge.SampleClient status
RFiDGear.Bridge.SampleClient run job-1 C:\projects\order.rfPrj $JOBNUMBER=4711
RFiDGear.Bridge.SampleClient result job-1
RFiDGear.Bridge.SampleClient results 12
RFiDGear.Bridge.SampleClient auto
```

`run` prints the request id before it calls, so that you can ask about the run
with `result` even if the program is killed on the way; a result that cannot be
told (the connection broke, RFiDGear was restarted, the id is not known) is
printed as `Unknown` with the advice to look at the card first, and the exit
code is not zero. `run` writes a card: use a test card.

The key vault is not on the command line on purpose: a key does not belong in
an argument. The tests of this sample (`BridgeSampleClientTests` in
`RFiDGear.Tests`) show a vault that answers one reference and refuses the
others, which is the shape every vault should have.

This is a sample, not a library: copy what you need. It keeps the rules of
the guide and nothing more; a real client also decides what its operators see,
where it writes its request ids down, and what it does when RFiDGear is not
running.
