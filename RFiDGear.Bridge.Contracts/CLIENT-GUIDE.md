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
   that was replaced meanwhile is refused instead of run.
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
- Never log the request or the answer, and do not keep the key longer than the
  answer needs. RFiDGear never logs either, and keeps its copy only for the
  card operation, in a buffer that it erases. **Neither side can erase the
  JSON text of the message**: this protects against logs, files and command
  lines, not against a memory dump of a process of the same user.
- The registration ends with the connection; register again after you
  reconnect.

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
