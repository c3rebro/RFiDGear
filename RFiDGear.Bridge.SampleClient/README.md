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
RFiDGear.Bridge.SampleClient auto --approve-with=$EXPECTEDUID [--no-pin]
    [--wait=8] [--set=NAME=value]
RFiDGear.Bridge.SampleClient auto --decline
RFiDGear.Bridge.SampleClient auto --no-answer
RFiDGear.Bridge.SampleClient keys orders/4711 --own-runs-only --run=job-2
```

`run` with a project loads it first and passes what it learned (the path, the
number of tasks, and the revision of the project when RFiDGear offers
`project.revision`) with the start, so that a project that was replaced or
changed in the window in between is refused instead of run.
`--expect-revision=<revision>` passes a revision you noted from `status`
instead, to see such a refusal by hand. When RFiDGear offers the capability but
gives no revision for the project it loaded, `run` does not start.

`run` prints the request id before it calls, so that you can ask about the run
with `result` even if the program is killed on the way; a result that cannot be
told (the connection broke, RFiDGear was restarted, the id is not known) is
printed as `Unknown` with the advice to look at the card first, and the exit
code is not zero. `run` writes a card: use a test card.

`keys` serves a key for one reference (typed at a prompt that does not show
it). `--own-runs-only` asks RFiDGear to answer for every run that the sample did
not start and checks it again itself (`StartedByThisClient`); `--run=<id>` starts
a run on the same connection, because the bridge serves one client at a time and
a program that supplies keys and starts runs needs one connection for both.

The key vault is not on the command line on purpose: a key does not belong in
an argument. The tests of this sample (`BridgeSampleClientTests` in
`RFiDGear.Tests`) show a vault that answers one reference and refuses the
others, which is the shape every vault should have.

`approved-run <requestId> <project.rfPrj>` is the reference flow for one card
(see the client guide, "Reference flow: one card, one approval, one run"): it
loads the project and notes its revision, looks at the reader, shows the UID and
asks you to type `yes` (a real program asks its own job and its own list),
starts one run with that UID in the variable `$EXPECTEDUID` (`--variable=$NAME`
for another name; `--yes` answers for you, for a test), and only then compares
the card that the result says the run read with the approved one. The exit code
is 0 only for a confirmed run.

`ApprovalModeLoop` is the loop of a program that decides about every card in
automatic mode (see the client guide, "Automatic mode that waits for your
approval"): it switches the mode on with `requireApproval`, hands every card
that is shown to your decision (`CardDecision.ApproveWith` with the variables of
that card, or `Decline`), answers with the presence id, treats a decision that
throws (its own time limit too) and a project state that cannot be told as a
refusal, drops a decision whose card is given up meanwhile (a decision never
holds up the events), tolerates a late answer, ends when the mode is stopped or
the connection ends, and stops the mode when it is cancelled, but only a mode
that it switched on itself and that `getStatus` still shows as the approval mode
of a client. It reports the end of every card that was shown, also when it is
cancelled meanwhile. `auto --approve-with=$NAME` approves every card by
one fixed rule, `--decline` declines every card and `--no-answer` never
answers, so that you can see each end by hand; an option the command does not
know is refused, and only a bare `auto` is the plain mode that runs every card
(cases 52 to 65 of
`RFiDGear.HIL.Tests/EXTERNAL-CONTROL.md`). A real program decides in its own
code, with its job and its list of cards.

This is a sample, not a library: copy what you need. It keeps the rules of
the guide and nothing more; a real client also decides what its operators see,
where it writes its request ids down, and what it does when RFiDGear is not
running.
