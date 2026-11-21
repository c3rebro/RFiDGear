---
title: Writing a client of the RFiDGear external control bridge
---

This guide is for the author of a program that drives RFiDGear: load a project,
run its tasks on a card, read what happened, and supply the card keys from its
own vault. `README.md` next to it lists the protocol; this file says what a
client has to do, and above all what it must not. The sample client
`RFiDGear.Bridge.SampleClient` does all of it in about 300 lines, and its
tests (`BridgeSampleClientTests`) are the proof that the advice here works.

**The one rule:**

RFiDGear writes cards. A request that may have reached it has to be treated as
if it had, until you have asked. So: when a `startRun` gets no answer (a
timeout, a lost connection, an error without `neverStarted: true`), **do not
make a new request id and do not start the job again.** Ask about the same
request id with `getRunResult`. Only an error that carries
`neverStarted: true` says that nothing was started, and only then may the job
be tried again.

**Connecting:**

1. Open the pipe `RFiDGear.Bridge.<SID>` of the Windows user that runs
   RFiDGear with `PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly`
   (`BridgeSampleClient.DefaultPipeName` makes the name). `CurrentUserOnly` on
   your side makes you refuse a pipe that another user created under the name.
2. Wrap it in a `JsonRpc` with `HeaderDelimitedMessageHandler` and the
   `SystemTextJsonFormatter`, with the options of `BridgeJson.CreateClientOptions()`.
3. **Override `GetErrorDetailsDataType`** of `JsonRpc` and return
   `typeof(BridgeErrorData)`. Without it the library reads `error.data` as its
   own type and the data of the bridge (the kind, the error id, `neverStarted`)
   is lost, and you cannot tell a refusal from a doubt.
4. Register your object as the local target of `IBridgeClientEvents` and
   `IBridgeClientKeys` with `CommonMethodNameTransforms.CamelCase`, and attach
   `IBridgeService` as the proxy with the same transform. A method of the
   contract is called by its camel-case name.
5. Start listening, then call `hello` with the protocol major version you speak
   (1), and the events you want.

An unknown method and a request that does not fit both reach you as
`RemoteMethodNotFoundException`; tell them apart by the error code, `-32601`
for a method that does not exist and `-32602` for parameters that do not fit.
If you call only what the capabilities in `hello` allow, you should not see
either.

**What to do from what `hello` says:**

- Check the capability before you call: `execution.run` for `startRun`,
  `autoMode`, `keys.provider`, and so on. A host that does not offer one fails
  the call with `capabilityNotSupported`; there is no silent no-op.
- Keep the `instanceId`. It is the id of the RFiDGear process: when it changes
  between two connections, RFiDGear was restarted, the result numbers start
  again, and the results you held are not in it.
- Read the limits from the answer and keep inside them.
- Until `getStatus` says `startupComplete`, everything else answers
  `notReady`; wait and ask again.

**Running a job:**

1. Make the request id from your job (an order number plus an attempt number,
   say) and **write it down before you call**. It is the only way to ask
   about the run if you lose the connection, and it is what makes a repeated
   request recognisable: while RFiDGear still knows the id (the last 1024, and
   after a restart only with the capability `execution.journal`), the same id
   with the same content never starts a second run. Past that it can: this is
   why a client asks about an id after a doubt and does not send it again.
2. Check `getStatus` if you like (a project is loaded, the reader sees the
   card), and pass `expectProjectPath` and `expectTaskCount` so that a project
   that was replaced meanwhile is refused instead of run. They cannot tell a
   project whose task was changed in RFiDGear's window after you looked: the
   path and the count are the same. With the capability `project.revision`,
   pass `expectProjectRevision` as well, the `revision` that `loadProject` or
   `getStatus` gave you for the state you approved (see below).
3. Call `startRun`. It answers when the host has admitted the run (`running`),
   not when it ends. If it answers with an error:
   - `neverStarted: true` (`busy`, `noProjectLoaded`, `noReader`,
     `projectMismatch`, `uiUnresponsive`, `shuttingDown`, `notReady` and
     others): nothing was started. Fix the cause or wait, and try again.
   - no `neverStarted`, a timeout, or a lost connection: **ask**, do not send.
     (An `invalidRequest` is a mistake of the program: the details name the
     field. It carries no flag, so a client that wants to be certain asks too.)
4. Wait for the end with `getRunResult` by request id and `waitMs` up to
   30000; it returns as soon as the run ends or when the wait runs out, and
   you ask again while it says `running`. Or listen to `runCompleted`, which is
   sent once per run, and use `getRunResult` only as the fallback.
5. Read the result. A run is only a success when `status` is `success` (or
   `completedWithIntentionalFailures`, which the project asked for). Look at
   `taskOutcomes`: one per task, `isSuccess` is missing for a task that was not
   reached. `bridgeFault` as the reason means that RFiDGear could not give you a
   complete result and that the card may have been touched.

**The revision of the project (`project.revision`):**

`loadProject` and the `project` of `getStatus` carry a `revision`: an opaque
text that says in which state the project is in RFiDGear now. It covers what a
save would write, so a change that was made in the window and not saved is
part of it, and it does not change when a run only leaves its results on the
tasks: the data that a task read from a card is no part of it, nor is what a
task that reads holds in its data buffer, nor which task has the focus. The
data that a write task is going to write is part of it, the bytes that would go
to the card, a selected part of its buffer included. (A write task that is
set to take its data from a file just before it writes does not: the revision
says what is in the project, not what is in a file on the disk.) A load gives
the revision of the project as it was put in place, so an edit that follows is
a change of it. `startRun` with `expectProjectRevision` is refused before
anything is touched (`projectMismatch`, field `expectProjectRevision`,
`neverStarted`) when the project is in another state. The result of the run
carries `projectRevision`: the state it started with, which is what you note
down as what was run.

- Take the revision from `loadProject`, or from `getStatus` after you decided
  to approve that state, and pass it to `startRun`. Compare nothing else about
  it: do not parse it, and do not try to derive it from the file.
- A revision is valid for **one RFiDGear process**. It is made with a key that
  RFiDGear picks when it starts (the text of a project can hold typed card
  keys, and a plain hash of it would let anybody who sees a revision test
  guesses for one). After a restart, and in another RFiDGear process, the same
  project has another revision, and the `instanceId` tells the processes apart.
  Read the revision again after you loaded the project; do not store one for
  later.
- A run changes the project it runs: tasks write what they found into their
  own settings (the application a task created, the key version it read, the
  size of a file). That is no edit. The state a run that was started with
  `startRun` leaves behind is recognized as the project it started with, so the
  revision you approved is still the project for the next card, and `getStatus`
  after the run may show another value. What is not recognized: a change made
  in the window after the run, what a run left that was started in the window,
  by automatic mode or by an extension (the bridge did not see where it
  started), and any run for which RFiDGear could not read the state at its end
  (the window did not answer in time). An edit that comes after the end of the
  run is never taken for what the run did. Such a state is refused, which is the
  safe way to be wrong: read the revision again and approve it.
- A RFiDGear that cannot tell a revision refuses a run that expects one, and
  does not offer the capability. A run never starts on a revision it cannot
  check. If it offers the capability and the answer to `loadProject` has no
  `revision` (it could not make one for this project), do not start: a run
  without the revision is only held back by the path and the task count, which
  cannot tell a task that was changed in the window.
- The result of a run of automatic mode carries `projectRevision` as well: the
  state the run started with, which the main window read when it took the run.
  Every card of the mode starts with the state that the card before it left
  (the tasks write some of their own settings), so the values differ from card
  to card; note the one of the result in the record of the card.

**After a doubt, a lost connection or a restart:**

- Connect again, make the handshake, call `getRunResult` with the request id.
- `running`: the run is going on; keep asking. `completed`: it ended; the result
  is there unless the history dropped it (then you know it ended, but not how).
- `interruptedByRestart`: RFiDGear was closed or died while the run was going
  on. The card may have been written. Look at the card (read it with a task
  that does not write) before you decide.
- `unknown`: RFiDGear does not know the id. Either the request never arrived,
  or it is older than the last 1024. **That is no proof that nothing was
  started.** Look at the card before you start the job again.
- Only when the capability `execution.journal` is offered does RFiDGear still
  know, after a restart, which request ids it had admitted; without it a
  restart forgets all of them.

**Catching up on results:**

`getStatus` tells you the number of the latest result (`lastResultSeq`).
`listRunResults` with the number of the last result you have returns the
results after it, oldest first, those of automatic mode too, up to the last
64. `gap: true` says that some were dropped from the history in between and
are lost to you. A number that RFiDGear has not given out (you kept it over a
restart) is refused with `invalidRequest`: check the `instanceId` first. Events
are hints and can be missed; this is the way to be sure you have every result.

**Events:**

Ask only for the events you use. Keep the handler short and never call RFiDGear
from it: the notifications are delivered one after the other, and a handler
that waits holds up the rest. A client that does not read for ten seconds is
disconnected, and so is one that lets more than 64 results wait; the results
stay in the history, and you catch up after you reconnect.

**Automatic mode:**

`startAutoMode` makes RFiDGear run the project once for every card that is put
on the reader. It is refused when the user has switched the mode on
(`autoModeActive`): a client does not take over the mode of the user. By
default the mode ends when your connection does (`stopOnDisconnect`); set it to
`false` only if you want the mode to go on while you reconnect, and then read
what you missed with `listRunResults`. A run that fails does not stop the mode;
a fault, a timeout or a lost reader does, and `autoModeChanged` says why
(`lastStopReason`). While the mode is on, `loadProject` and `startRun` are
refused. You can stop the mode whoever started it.

This mode runs every card that is put on the reader. A station that writes
cards one after the other does not use it: it uses the mode that waits for
your approval, described below (`autoMode.approval`).

**Supplying card keys:**

A project can name a key by a reference to a provider instead of containing it.
Call `registerKeyProvider` after `hello`; the answer gives the provider id
(`bridge-client`) to name in projects, and the time you have to answer. A
person binds a key slot in a task dialog to that provider and types a
reference of your choosing, such as `orders/4711`. When a task needs the key,
RFiDGear calls `resolveKey` on you, with the reference, what the key is for
(algorithm, usage, application, key number), the run, the task, and the UID of
the card.

- Implement `resolveKey` asynchronously and honour the token. It is cancelled
  when the time is up (10 seconds, 5 minutes when a person may be asked) or the
  run is given up, and a key that arrives later is thrown away.
- **Check the reference against what you serve.** Any program of the user can
  name any reference in a project, and every run asks you while you are
  registered, whoever started it. Answer `notAuthorized` for a reference you do
  not recognise, and `notFound` for one you recognise without a key.
- The answer is `resolved` with the key as base64 (1 to 32 bytes) or one of
  `notFound`, `notAuthorized`, `invalidKey`, `unsupported`. Anything else, or an
  exception, is a failure of your side.
- Without a registered client, a task that names the provider ends with
  `ERROR.KeyUnavailable` and does not touch the card; the key typed into the
  project is no fallback.
- RFiDGear does not ask you for the password or the PACK of a MIFARE Ultralight
  EV1 or an NTAG: this protocol has no name for those two kinds of key yet, so a
  slot of that kind that is bound to `bridge-client` ends with
  `ERROR.KeyUnavailable`, and no `resolveKey` call is made. The key of an
  Ultralight C is a `tripleDes2K` key.
- Never log the request or the answer, and do not keep the key longer than the
  answer needs. RFiDGear never logs either, and keeps its copy only for the
  card operation, in a buffer that it erases. **Neither side can erase the
  JSON text of the message**: this protects against logs, files and command
  lines, not against a memory dump of a process of the same user.
- The registration ends with the connection; register again after you
  reconnect.

**Whose run is it, and what that is worth (`keys.context`):**

When RFiDGear offers `keys.context`, every request for a key says where its run
comes from:

- `source`: `bridge` for a run of a client of this bridge (any client, so
  compare `requestId` to know whether it was you), `autoMode`, `gui` for a run
  that the user started in the main window or with a start-up argument, and
  `other` for everything else (an extension, a button in a task dialog, a run
  RFiDGear cannot place). A request without a `source` reads as `other`: a
  missing value is never taken for your own run.
- `requestId`: for a run of a client of this bridge, the id that the client
  chose in `startRun`. It is yours when you sent it. It makes the match without
  the `runId` in the answer to `startRun`, which the first request for a key can
  overtake: write the request id down before you call `startRun`.
- `projectRevision`: for a run of a client, the revision the run **started
  with** (see `project.revision`), not the one of the project now. Compare it
  with the revision you approved for that request.

These are information. Whether you are asked, and what you may answer, does not
change if you ignore them, and the decision stays with your vault. A vault that
gives out a key only for a request that is `source: bridge`, whose `requestId`
it sent, whose `projectRevision` it approved and whose `cardUid` is the card it
approved, has a release that is bound to one job.

`ownRunsOnly` in `registerKeyProvider` (default off) goes one step further:
RFiDGear then answers `notAuthorized` itself for every run that was not started
over **this connection**, and does not call you for it. That includes a run
that the user starts in the window, a button in a task dialog, and a run that an
earlier connection left behind (a client that went away mid-run, even if it was
you before you reconnected): a project that names your provider ends
`KeyUnavailable` there, and you ask for the result of your earlier run by its
request id as usual. The run of a card that **you** approved in automatic mode
(below) is your own as well: RFiDGear runs it as a run of the mode (`source`
stays `autoMode`, there is no `requestId`) and notes which connection approved
it before the notice that names the run and before its first task. A run of the
mode that nobody approved (the mode of the user, a card you did not approve) is
nobody's. You learn the id of the run from `cardPresentationEnded` (`approved`,
with `runId`), and the request for a key can be here first, so wait for the
notice a moment before you refuse (the sample does: `ApprovedRunWait`). The
answer to the registration
says whether the restriction is in force (`ownRunsOnly: true`); check it. Send
the field only when `keys.context` is offered: an older RFiDGear rejects a
registration that carries a member it does not know, while a registration
without the field is the same message as always. In any case, do your own check
as in the sample (`StartedByThisClient`). The latest registration of a
connection counts.

**What this protects against, and what it does not.** The fields and the
restriction protect against mix-ups and mistakes: a run from the window while
your job was meant, a run of automatic mode, a project that was changed after
you approved it, a request for a card you did not approve. They do not protect
against a hostile program of the same Windows user. Such a program has the same
rights as the user: it can use the pipe (when you are not connected), put any
reference in a project and run it, and RFiDGear can only say what it knows
about a run, not prove who is behind it. Keep your keys as you would for any
program of the user, and treat a release that is bound to a run as a second
lock, not as the only one.

**Approving a card before it is written (Check UID with a variable):**

A card should be written only after the program that owns the job has looked at
it (its UID, the order, a duplicate check) and approved it. The project has a
place for that: the task **Check UID** can name a variable instead of a UID,
written as a dollar sign and a name, for example `$EXPECTEDUID`. The caller
supplies the UID it has approved as that variable: with `startRun` it is one of
`variables` (`{ "name": "$EXPECTEDUID", "value": "04A1B2C3D4E5F6" }`; a start-up
argument or an extension supplies variables the same way). The project does not
change from card to card, so its revision does not either.

1. `scanReader`, and look at the UID of the card that is on the reader
   (`credentials`; upper-case hexadecimal digits, as RFiDGear writes them in
   the result).
2. Decide: the order, the duplicate check, the profile. This is yours.
3. `startRun` with the variable set to that UID (and the revision of the
   project, see `project.revision`).

The task compares the UID with the one of the card that **the run itself read
at its start** (the `cardUid` of the result), so a card that was swapped between
the scan and the start is not the approved one. A card that is not the
approved one ends the Check UID with `CardNotExpected`, as does a variable that
is not given, is empty, or holds anything but the UID of the card. Every task
that depends on the check is skipped, and the run ends `failure`: never
`success`, and never `completedWithIntentionalFailures`, which only a question
answered "no" can end with.

- Make the check the first task, and make **every task that touches the card**
  depend on it (its execute condition: the check, error `NoError`, and a task
  behind that one on the one before it). RFiDGear cannot tell which tasks
  write: a task that does not depend on the check runs whatever the check said.
- A UID typed into the task is another thing: a question ("is this card that
  card?"), answered `isNotTrue`, which a project may branch on and a run may
  end with as an answer it asked for. Only a variable is an approval.
- The variable is the one of the run. In the plain automatic mode the
  variables are those of the start-up (they do not change from card to
  card), so a variable there approves every card with that UID again: do not
  approve a write that way. Use single runs, as above, or the mode that waits
  for your approval (below), where every approval brings the variables of its
  card.

**Reference flow: one card, one approval, one run:**

This is the flow for a job that writes a card, as the sample client does it
(`BridgeSampleClient.RunApprovedCardAsync`, command `approved-run`; the tests of
`BridgeSampleClientTests` run it against the bridge). Use it for writes of one
card at a time. For a station that handles one card after the other, use the
mode that waits for your approval (below); the plain automatic mode runs every
card and is not for writing.

Once per project (and again after RFiDGear was restarted: a revision is valid
for one process):

- `hello`. This flow needs `project.load`, `reader.scan` and `execution.run`,
  and should have `project.revision`; call nothing that is not offered.
- `loadProject`. Note the `path`, the `taskCount` and the `revision` as the
  state you approved for this job. A change made in RFiDGear afterwards makes the
  run refuse, before anything is touched.

For every card:

1. `scanReader`. Go on only when the reader is `ready` and shows exactly one
   card, which is the main one. No reader, busy, no card or more than one card is
   a stop, and nothing has been started.
2. Decide, in your own program: the job, the profile, the serial number, the
   list of cards that were written already. That is the approval. The UID of the
   scan is what you approve; it identifies the card for the job, it does not prove
   that the card is genuine.
3. `startRun` with the request id of this job and this card (written down before
   the call, never reused for another card), the variable `$EXPECTEDUID` (or the
   name your project uses) set to the approved UID, and `expectProjectPath`,
   `expectTaskCount` and `expectProjectRevision` of the load. Never send it
   again: ask by the request id.
4. Wait for the result (`getRunResult` with `waitMs`, or the event). Record the
   card as written only when all of this holds: the status is `success` (or
   `completedWithIntentionalFailures`, if that is what your project means by
   done) and `cardUid` is the approved UID. The sample says it in one flag,
   `ApprovedRunOutcome.IsConfirmed`. Put the `projectRevision` of the result into
   the record of the job: it is the state the run started with. After an earlier
   run of yours it can be another than the one you approved, because the tasks
   write some of their own settings; RFiDGear refused the run before it started
   if the project was in a state that is neither the approved one nor one that
   a run of yours left behind.
5. After a doubt (a timeout, a lost connection, a restart) ask `getRunResult`
   by the request id and never start the job again before you have looked at the
   card.
6. To read it back, load a separate project that only reads (a new
   `loadProject` and a new revision) and run it the same way. Do not read back
   with the project that writes.

What the project that RFiDGear runs must look like (your generator's part):

- The first task is Check UID with the variable `$EXPECTEDUID`, and every task that
  touches the card depends on it (execute condition: the check, error `NoError`;
  a task behind that one on the one before it). RFiDGear cannot tell which tasks
  write: one that does not depend on the check runs whatever the check said.
- A card that was swapped or taken away after the approval ends the check with
  `CardNotExpected` (or the run with no card at all), the tasks that depend on it
  are skipped, and the run is a `failure`.
- Nothing in the project holds the UID of one card, so the project, and with it
  its revision, does not change from card to card.

**Automatic mode that waits for your approval (`autoMode.approval`):**

The reference flow is one card at a time. A station that handles one card after
the other uses this mode: RFiDGear shows every card that is put on the reader
to the client that started the mode, and runs the project for it only when
that client approves it, once, for that card. Nothing is run for a card that
nobody approved. The sample does all of it (`ApprovalModeLoop`; the command
`auto --approve-with=$EXPECTEDUID` tries it by hand), and
`BridgeSampleClientTests` run it against the bridge.

Start it with `startAutoMode` and `requireApproval: true`. It needs
`stopOnDisconnect` (the default), because nobody would be left to answer
otherwise. `approvalTimeoutMs` (2000 to 120000, default 15000) is how long a
card is waited for. Check `autoMode.approval` first, because an older RFiDGear
rejects the fields, and ask `hello` for `cardPresented`,
`cardPresentationEnded` and `autoModeChanged`. A start with approval is never
agreed on again (`autoModeActive`): stop the mode and start the one you mean.
A refused start leaves nothing to clean up: the mode that is on is not yours,
so do not stop it. `stopAutoMode` switches the mode off whoever switched it on,
so ask `getStatus` first (the mode has to be `enabled` and `startedBy` `bridge`:
a mode of a user is `other`, and RFiDGear serves one client at a time), as the
sample's loop does, or close your connection, which ends the mode that it
started and no other. If RFiDGear answers the start without `requireApproval`,
the mode runs every card: stop it at once, and give up.

For every card:

1. `cardPresented` says the `presenceId`, the `uid`, the `projectRevision` (the
   state of the project now, read when the card was shown) and when the wait
   ends (`expiresAt`). A presence is the card as it was put on the reader: the
   same card taken away and put back is another presence with another id, and
   RFiDGear asks again. Whether this UID was written already is yours to know:
   RFiDGear does not remember what you approved.
2. Decide, in your own program: the job, the profile, the serial number, the
   list of cards that were written already. That is the approval. The events go
   on while you decide, and the wait for the card can end under you (declined,
   expired, taken away, the mode stopped): drop the decision then and send
   nothing (the sample cancels the token of the decision).
3. `approveCard` with the `presenceId`, the `variables` of this one run (the
   Check UID task of the project reads `$EXPECTEDUID` from here, set to the UID
   you approved) and `expectProjectRevision` (the one that came with the card:
   the run then starts only when the project is in exactly that state), or
   `declineCard`. Answer once, on the connection that started the mode. A
   variable that cannot be used is `invalidRequest` and the card still waits:
   a report target that is not a full path to a file in a folder that exists,
   a template that does not exist. The report variables are those of this card
   only, and the report of the card is built apart from the next one.
4. `cardPresentationEnded` says how the wait ended, once for every card:
   `approved` (with the `runId` of the run that was admitted); `declined`,
   `expired` (no answer in time), `stopped` (the mode was stopped) or
   `clientLost`: nothing was run; `projectChanged` (the project was not in the
   state you named), `notAdmitted` (something else held the reader or the
   execution slot) or `cardChanged` (the card was taken away or replaced while
   you decided, or another run was made on it): nothing was run, and a card
   that is on the reader and was not processed is shown again as a new
   presence.
5. The result of the run (`autoRunCompleted`, `listRunResults`) carries the
   `presenceId`, the `projectRevision` the run started with (exactly the one
   you named: the check is exact, but the one in the next `cardPresented` is
   not the same, because a run changes the project) and the `cardUid` the run
   itself read. Record the card as written only when the status is what your
   project means by done and the `cardUid` is the UID you approved.

**What that mode holds to, and what stays with you:**

- Without your approval nothing runs. A card that you decline, that you do not
  answer in time or that is taken away is not run, and a declined or expired
  card is not shown again until it was taken away.
- An approval is for one card and one run. The run reads the card itself, and
  that read is held to the approved UID before anything else is sent to the
  card: another card, or none, ends the run before its first task and stops
  the mode. The approval is used up by the first run, whatever it ends with.
- A late, repeated or foreign answer is `approvalNotPending` (1018, with
  `neverStarted`): treat it as "this presence is over" and go on. Only the
  connection that started the mode can answer.
- A lost connection ends the mode (`stopOnDisconnect`) and runs nothing more.
  **Never start a writing job again by yourself after you reconnected.** Start
  the mode again on purpose: the card that is on the reader is shown as a new
  presence and needs a new approval.
- `loadProject` and `startRun` are refused while the mode is on, and a project
  that was changed after you saw the card is `projectChanged` when you named
  its revision. Name it.
- RFiDGear holds the card to the UID, but only your project says which tasks
  write. Keep the Check UID with `$EXPECTEDUID` as the first task and make every
  task that touches the card depend on it, as in the reference flow.
- A run that fails does not stop the mode; a fault, a timeout or a lost reader
  does (`autoModeChanged` says why). After one of them look at the card before
  you start again.
- A card that is taken away and put back faster than the reader is polled is
  not seen, and counts as the same presence.
- Notices are hints, and the truth is `getStatus`. A notice that the mode
  stopped can be about an earlier mode (you listen from before your own start,
  so it can come first): ask the status before you give up the mode, as the
  sample's loop does. The loop ends on that notice and does not poll; a program
  that must not run on with a mode that ended without a notice asks `getStatus`
  now and then as well.

**Going from a project that writes to one that only reads:**

Never with the mode on. Stop the mode (`stopAutoMode`), wait for
`autoModeChanged` with `enabled: false` and for `getStatus` to say that no run
is going on, `loadProject` the other project and note its revision, and start
the mode again (with approval too, when the project that reads also needs your
decision for every card). Take the card away between the two: a card that was
processed is not shown to the new mode until it was taken away.

**What the tests prove without a reader, and what only a card can:**

A reader and a card are needed for the hands-on cases (`EXTERNAL-CONTROL.md`,
from case 52). Everything about the decision is tested without them, with the
real bridge on a real pipe, the real state machine of automatic mode and the
real main window against a scripted reader:

- Nothing runs without an approval, and an approval is used once, for that
  presence: `AutoModeApprovalControllerTests`, `AutoModeApprovalGateTests`,
  `BridgeAutoModeApprovalTests`.
- A decline, no answer in time, a stop, a lost connection, a card taken away or
  replaced (also by the card that was shown before), a project that was
  changed, a reader that is busy or cannot be looked at:
  `AutoModeApprovalControllerTests`, `BridgeAutoModeApprovalTests`,
  `BridgeAutoModeRealHostTests`.
- Only the starting connection answers, an answer after the limit is no answer,
  and what belongs to one connection never reaches another:
  `AutoModeApprovalGateTests`, `BridgeServerTests`,
  `BridgeAutoModeApprovalTests`.
- The run's own read is held to the approved UID before anything else is sent
  to the card: `TaskExecutionServiceTests` (`ExpectedCardUid`).
- The variables and the report of a card do not carry over to the next:
  `BridgeAutoModeRealHostTests`.
- The keys of an approved run with `ownRunsOnly`: `ApprovedRunTests`,
  `BridgeKeyContextTests`, `BridgeSampleClientTests`.
- The loop of a client: `BridgeSampleClientTests` (`TheLoop_...`).

What only a card shows: how a real reader reports a card that is taken away
between two polls, how long a real read takes against the time you allow, and
what a real task does to a real card.

**Why a check failed (`execution.verificationDetails`):**

A task that compares values can say what it compared, so that the record of a
failed check (an acceptance record, for instance) holds more than "failed". The
outcome of such a task carries `verificationDetails`; today only the DESFire task
"Check: File Settings" has them. The details of a task that has none, that did
not run, or that failed before it could compare (the login was refused, the card
went away) are absent, and `isSuccess` and `normalizedErrorCode` say what
happened. They repeat nothing of the run, the request or the task position: that
is the result around them.

- `schemaVersion` (1), `kind` (`desfire.fileSettings`), `overallMatch` (the
  verdict of the task), `compareMode` (`all` or `any`: whether all the compared
  settings had to match or one was enough), for the kind `applicationId` and
  `fileNumber`, and `errorCode` (`notRead` when a setting that was selected could
  not be read).
- `checks`: one entry per setting, in a fixed order (`fileType`, `fileSize`,
  `communicationMode`, `accessRead`, `accessWrite`, `accessReadWrite`,
  `accessChange`), whether the task compared it or not. `compared` says which,
  and `result` is `match`, `mismatch`, `notRead` or `notCompared`. A setting that
  was compared has `expected` and, unless it could not be read, `actual`, both
  numbers: the file type and the communication mode as DESFire numbers them, the
  size in bytes, and an access right as the number of the key (0 to 13), 14 for
  "free" and 15 for "never". The size also has a `rule` (`exact`, `atLeast`,
  `atMost`): how what was read has to relate to what was expected. A setting that
  was not compared has no values at all.
- `notRead`: a data file that reports a size of 0 cannot be told from one whose
  size could not be read. The check does not call that a mismatch; it says so, and
  the verdict is made from the other settings when there are any.
- Only numbers and fixed words, never a key, a text or the content of a file.
  Something you do not know (another `kind`, a later `schemaVersion`, a result
  word) is no error: read `checks`, show what you do not understand as it is, and
  never decide anything on a word you do not know.

**What to log and what not:**

Log the request id, the run id, the result number, the status and the kinds of
errors. Do not log variables, paths, card UIDs or keys, and do not copy the
text of an exception into a message for people without looking at it. RFiDGear
keeps the same rules: its own log has the type of an exception, the request id
and the numbers, and never a value you sent.

**What not to do:**

- Do not send a job again under a new request id after a doubt.
- Do not poll `getStatus` in a tight loop to find out when a run ended; use
  `getRunResult` with `waitMs`, or the events.
- Do not assume a card is on the reader because the last status said so: read
  `scanReader` or let the run's own result tell you.
- Do not treat `unknown`, or the end of the connection, as "nothing happened".
- Do not run RFiDGear and the client with different rights, and do not start
  more than one RFiDGear.
- Do not depend on the order of events of different kinds, other than what the
  README says (`autoRunCompleted` before the `autoModeChanged` that follows it).

**Trying it:**

Start RFiDGear with `EXTERNALCONTROL=1`, then `RFiDGear.Bridge.SampleClient
status`. The sample's `run <requestId> <project.rfPrj> $JOBNUMBER=4711` loads
a project and runs it; `result <requestId>` asks what became of a request;
`results` lists what a client missed; `auto` switches automatic mode on until
you press Ctrl+C.
