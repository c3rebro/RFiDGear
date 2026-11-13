---
title: RFiDGear.Bridge.Contracts
---

RFiDGear.Bridge.Contracts is the contract of the external control bridge: what
a program that runs next to RFiDGear needs to start RFiDGear's tasks, to read
their results and to supply card keys, without parsing a log. The client
guide (`CLIENT-GUIDE.md`, next to this file) says how to write one. A working
client is `RFiDGear.Bridge.SampleClient`.

The assembly is plain `net10.0`, has no references, and keeps the assembly
version `1.0.0.0` for the whole protocol major version, so a client built once
keeps binding. It ships with RFiDGear: take `RFiDGear.Bridge.Contracts.dll`
from the install folder (there is no package yet). The JSON-RPC library is not
part of it; RFiDGear uses StreamJsonRpc 2.26.150, and a client should use the
same version or a compatible one.

The protocol is not frozen until the first hands-on test with a real reader
has passed. Until then a name may still change; after it, nothing is removed
or re-meant within protocol major version 1, and a client must read what a
newer RFiDGear adds as optional.

**Switching it on:**

RFiDGear listens only when you ask it to: the setting "Allow another program of
this Windows user to control RFiDGear (external control)" in the settings
dialog, or the start-up argument `EXTERNALCONTROL=1` for one run. Either one is
enough. A change of the setting takes effect after a restart. There is one
RFiDGear per Windows session; a second one exits at once, so a program connects
to the one that runs, or starts exactly one.

**The connection:**

- One named pipe, `\\.\pipe\RFiDGear.Bridge.<SID>`, where `<SID>` is the
  security identifier of the Windows user that runs RFiDGear
  (`BridgeProtocol.PipeNameFor`). Only that user can open it: RFiDGear creates
  it with `CurrentUserOnly`, and a client must open it with `CurrentUserOnly`
  too, so that it never talks to a pipe that somebody else made under the
  name.
- One client at a time. A second one waits until the first has left.
- A client that runs with another elevation than RFiDGear may not be able to
  open the pipe (the owner of the pipe is checked). That has not been tried
  yet, so start both the same way.
- JSON-RPC 2.0 with `Content-Length` headers (`HeaderDelimitedMessageHandler`
  of StreamJsonRpc), UTF-8 JSON. Parameters are positional with exactly one
  object, which is the request type of the method. Names are camel case and
  written explicitly (`BridgeJson` has the options); the host rejects members
  it does not know, and a client skips members it does not know.
- A message longer than 1 MiB ends the connection.

**Rules that hold for every call:**

- `hello` comes first. Anything else before it fails with
  `handshakeRequired`.
- A method whose capability the host does not offer fails with
  `capabilityNotSupported`. Nothing is a silent no-op, and a client decides what
  it may call from the capabilities in the answer to `hello`, never from the
  version of the application.
- Until RFiDGear has finished starting, everything but `hello` and `getStatus`
  fails with `notReady`.
- RFiDGear never repeats a card operation, and a client must not either: see
  `startRun`.

**Methods** (capability in brackets; every one takes one request and answers
one response):

- `hello` [session]: protocol negotiation by major version, the capabilities,
  the events that are on, the id of this RFiDGear process (`instanceId`) and
  the limits.
- `getStatus` [status]: what is possible right now: start-up complete, reader,
  project, whether a run is going on, the latest result number, and what
  automatic mode is doing. It answers within two seconds even when the main
  window does not (then `stale` is set).
- `loadProject` [project.load]: replaces the loaded project with a file. Unsaved
  changes in the window are lost. The path has to be a full path.
- `startRun` [execution.run]: runs the task list once and answers as soon as the
  run is admitted, not when it ends. The request carries an id of the client's
  choosing (1 to 64 of `A-Z a-z 0-9 . _ : -`), up to 64 variables as name and
  value pairs, and optionally the project path and the task count it expects.
- `cancelRun` [execution.cancel]: asks a run to stop between two tasks.
- `getRunResult` [execution.resultLookup]: the state and the result of a run
  by request id or run id, optionally waiting up to 30 seconds for the end.
- `listRunResults` [execution.resultLog]: the results of all runs, those of
  automatic mode too, from a number onward, with an explicit `gap` flag.
- `scanReader` [reader.scan]: reads the reader now.
- `startAutoMode`, `stopAutoMode` [autoMode]: continuous processing of cards.
- `registerKeyProvider` [keys.provider]: makes the client the supplier of card
  keys for the rest of the connection.

**Events** (notifications; a client asks for the ones it wants in `hello`, and
only those are sent; each carries `seq`, `sessionId` and `at`):

- `runStarted`, `runCompleted` [events.execution]: a run that this bridge
  started. `runCompleted` is sent once per run.
- `runningChanged` [events.running]: a task run, from anywhere, started or
  ended.
- `readerChanged` [events.reader], `projectChanged` [events.project].
- `autoModeChanged`, `autoRunCompleted` [events.autoMode]: the mode was
  switched on or off; a card was processed. The result of the run that stops
  the mode comes before the notice that it stopped.
- `shuttingDown` [events.lifecycle]: RFiDGear is closing. Best effort: the
  connection ending without it proves nothing.

Events are hints. Nothing is replayed after a reconnect; the truth is
`getStatus`, `getRunResult` and `listRunResults`.

**A call from RFiDGear to the client:**

`resolveKey` [keys.provider], on `IBridgeClientKeys`: RFiDGear asks a client
that registered for a card key. See the client guide for the rules.

**Capabilities:** `session`, `status`, `project.load`, `execution.run`,
`execution.cancel`, `execution.resultLookup`, `execution.resultLog`,
`execution.journal`, `autoMode`, `keys.provider`, `reader.scan`, and the event
groups `events.execution`, `events.running`, `events.reader`,
`events.project`, `events.autoMode`, `events.lifecycle`. `execution.journal` is
a guarantee and not a method: RFiDGear writes a run down before it starts it,
so that after a restart a request id it had admitted is still known.

**What a run reports:**

A result (`RunResultDto`) has the request id (when a client started the run),
the run id, `source` (`bridge` or `autoMode`), `status` (`success`,
`completedWithIntentionalFailures`, `failure`, `cancelled`, `timedOut`), the
`failureReason` (`none`, `cancelled`, `timedOut`, `faulted`, `bridgeFault`,
`unknown`), the counts, the times, the card UID when it is known, its number in
the sequence of results (`resultSeq`), and one outcome per task. The number of
outcomes is always the number of tasks, or the result says `bridgeFault`: a
result that is incomplete is never passed off as a complete one. The
states a lookup answers are `running`, `completed` (the result may be gone),
`interruptedByRestart` and `unknown`. `unknown` is not a proof that nothing was
started: an id that is older than the last 1024 is forgotten.

**Errors:**

An error is a JSON-RPC error with an application code and `error.data`
holding `kind`, an optional `errorId` (it also appears in the log of
RFiDGear), `neverStarted` and `details`. The text of the error is never an
exception message. A client has to override `GetErrorDetailsDataType` to read
`error.data`; the guide shows how. An unknown method and a request that does
not fit reach a client as `-32601` and `-32602`.

- 1001 `handshakeRequired`, 1002 `incompatibleProtocol` (the details carry the
  range the host speaks), 1003 `capabilityNotSupported`, 1004 `invalidRequest`
  (the details name the field, never its value), 1005 `notReady`.
- 1006 `busy` (the details say what the host is busy with: `run`, `load`,
  `edit`, `scan` or `other`), 1007 `autoModeActive`, 1014 `uiUnresponsive`,
  1015 `shuttingDown`.
- 1008 `projectNotFound`, 1009 `projectUnsupportedFormat`, 1010
  `projectLoadFailed`, 1011 `noProjectLoaded`, 1012 `projectMismatch`.
- 1013 `requestIdConflict`: the same id with other content.
- 1016 `internalError`, 1017 `noReader`.

`neverStarted: true` says that the request did not touch a card and a new
attempt is safe. An error without it does not say the opposite: ask.

**Limits** (also in `hello`): 1 MiB per message, 64 variables, 128 characters
per name, 2048 per value, 4096 per path, 64 results and 1024 request ids kept,
30 seconds of waiting per `getRunResult`, 32 bytes per key.

**Where this is documented and tested:**

`BridgeContractsApiSurfaceTests` freezes the public surface of this assembly
against `RFiDGear.Tests/ApiSurface/RFiDGear.Bridge.Contracts.api.approved`; a
change fails the test and is accepted by regenerating the snapshot
(`RFIDGEAR_UPDATE_API_SNAPSHOT=1`). `BridgeSamples` and `BridgeGolden` hold one
value of every message and its JSON, so a change of the wire is a diff in a
test. `RFiDGear.HIL.Tests/EXTERNAL-CONTROL.md` is the checklist for trying all
of it with a real reader.
