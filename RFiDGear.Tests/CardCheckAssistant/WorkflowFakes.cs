using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The store and settings the workflow is given, set by the test.</summary>
    internal sealed class FakeContextSource : IWorkflowContextSource
    {
        private int _calls;

        public Result<WorkflowContext> Answer { get; set; } =
            Result<WorkflowContext>.Fail(new Failure(FailureKind.NotConfigured, "settings.none", "No settings."));

        public int Calls => Volatile.Read(ref _calls);

        public Result<WorkflowContext> Resolve()
        {
            Interlocked.Increment(ref _calls);

            return Answer;
        }
    }

    /// <summary>
    /// The made-up order store with hooks: a list or an upload answer the test chooses, a gate that holds either open
    /// (like the real store, a held call that is cancelled answers "cancelled"), and a record of what was uploaded.
    /// </summary>
    internal sealed class ScriptedOrderStore : IOrderStore
    {
        private readonly IOrderStore _inner;

        public ScriptedOrderStore(IOrderStore inner) => _inner = inner;

        public Result<IReadOnlyList<OrderRecord>>? ListAnswer { get; set; }

        public Exception? ListThrows { get; set; }

        public TaskCompletionSource<bool>? ListGate { get; set; }

        public Result? FinalizeAnswer { get; set; }

        public TaskCompletionSource<bool>? FinalizeGate { get; set; }

        public int ListCalls { get; private set; }

        public List<(string Id, byte[] Report)> Finalized { get; } = new List<(string Id, byte[] Report)>();

        public async Task<Result<IReadOnlyList<OrderRecord>>> ListAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            if (ListThrows != null)
            {
                throw ListThrows;
            }

            if (ListGate != null)
            {
                await ListGate.Task;
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result<IReadOnlyList<OrderRecord>>.Fail(new Failure(FailureKind.Cancelled, "orders.cancelled", "Cancelled."));
                }
            }

            return ListAnswer ?? await _inner.ListAsync(cancellationToken);
        }

        public Task<Result<byte[]>> DownloadReportAsync(string orderId, CancellationToken cancellationToken) =>
            _inner.DownloadReportAsync(orderId, cancellationToken);

        public Task<Result> ReplaceReportAsync(string orderId, byte[] report, CancellationToken cancellationToken) =>
            _inner.ReplaceReportAsync(orderId, report, cancellationToken);

        public async Task<Result> FinalizeAsync(string orderId, byte[] report, CancellationToken cancellationToken)
        {
            Finalized.Add((orderId, (byte[])report.Clone()));
            if (FinalizeGate != null)
            {
                await FinalizeGate.Task;
            }

            return FinalizeAnswer ?? await _inner.FinalizeAsync(orderId, report, cancellationToken);
        }

        public Task<Result> TouchChangedDateAsync(string orderId, CancellationToken cancellationToken) =>
            _inner.TouchChangedDateAsync(orderId, cancellationToken);

        public Task<Result> TestConnectionAsync(CancellationToken cancellationToken) =>
            _inner.TestConnectionAsync(cancellationToken);
    }

    /// <summary>A check runner the test scripts: what it answers, the phases it reports, and a gate that holds it open (the host cancels only between tasks, so a held run ignores the token).</summary>
    internal sealed class FakeCheckRunner : ICheckRunner
    {
        public Func<CheckRunRequest, CheckRunResult>? Answer { get; set; }

        public TaskCompletionSource<bool>? Gate { get; set; }

        public Exception? Throw { get; set; }

        public IReadOnlyList<CheckPhase> Phases { get; set; } =
            new[] { CheckPhase.Preparing, CheckPhase.LoadingProject, CheckPhase.Running, CheckPhase.ReadingReport };

        public List<CheckRunRequest> Requests { get; } = new List<CheckRunRequest>();

        public List<CancellationToken> Tokens { get; } = new List<CancellationToken>();

        public async Task<CheckRunResult> RunAsync(CheckRunRequest request, Action<CheckPhase>? phaseChanged, CancellationToken ct)
        {
            Requests.Add(request);
            Tokens.Add(ct);

            foreach (var phase in Phases)
            {
                phaseChanged?.Invoke(phase);
            }

            if (Gate != null)
            {
                await Gate.Task;
            }

            if (Throw != null)
            {
                throw Throw;
            }

            return Answer!(request);
        }
    }

    /// <summary>A card waiter the test scripts, one answer per call; with nothing scripted it sees a card at once.</summary>
    internal sealed class FakeCardWaiter : ICardWaiter
    {
        private readonly Queue<Func<TimeSpan?, CancellationToken, Task<CardWaitResult>>> _script =
            new Queue<Func<TimeSpan?, CancellationToken, Task<CardWaitResult>>>();

        public List<TimeSpan?> Timeouts { get; } = new List<TimeSpan?>();

        public void ThenSees(ReaderSnapshot card) =>
            _script.Enqueue((_, _) => Task.FromResult(new CardWaitResult(CardWaitOutcome.Done, card)));

        public void ThenFindsTheReaderEmpty() =>
            _script.Enqueue((_, _) => Task.FromResult(new CardWaitResult(CardWaitOutcome.TimedOut, Snapshots.NoCard())));

        /// <summary>The next wait for a card lasts until it is cancelled, and then says so without an answer of the reader.</summary>
        public void ThenWaitsUntilCancelled() =>
            _script.Enqueue(async (_, ct) =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    // The cancel is what is waited for.
                }

                return new CardWaitResult(CardWaitOutcome.Cancelled, null);
            });

        public void ThenGetsNoClearAnswer() =>
            _script.Enqueue((_, _) => Task.FromResult(new CardWaitResult(CardWaitOutcome.TimedOut, Snapshots.Busy())));

        // Holds the wait open and then sees the card whatever the token says, as a look that has begun does: it is the
        // workflow, not the waiter, that has to throw the answer away after a cancel.
        public void ThenHoldsUntil(TaskCompletionSource<bool> gate, ReaderSnapshot card) =>
            _script.Enqueue(async (_, _) =>
            {
                await gate.Task;

                return new CardWaitResult(CardWaitOutcome.Done, card);
            });

        public Task<CardWaitResult> WaitForCardAsync(TimeSpan? timeout, CancellationToken ct)
        {
            Timeouts.Add(timeout);

            return _script.Count > 0
                ? _script.Dequeue()(timeout, ct)
                : Task.FromResult(new CardWaitResult(CardWaitOutcome.Done, Snapshots.Card(DefaultUid)));
        }

        // ── the card's way off the reader and back on (a script of its own, so the waits for a card are not disturbed) ──

        /// <summary>The UID of the card the waiter shows when nothing else is scripted.</summary>
        public const string DefaultUid = "04AABBCCDD";

        private readonly Queue<Func<string?, Action<CardMoveStage>?, CancellationToken, Task<CardMoveResult>>> _moves =
            new Queue<Func<string?, Action<CardMoveStage>?, CancellationToken, Task<CardMoveResult>>>();

        /// <summary>The calls of the wait for the card to be taken off and put back: the time, and the UID that was given as the card before.</summary>
        public List<(TimeSpan? Timeout, string? UidBefore)> Moves { get; } = new List<(TimeSpan? Timeout, string? UidBefore)>();

        /// <summary>The next move ends well: the card is taken off and this one is put on (<c>null</c> if no UID is known).</summary>
        public void ThenTheCardIsMoved(string? before, string? after) =>
            _moves.Enqueue((_, stages, _) =>
            {
                Announce(stages, CardMoveStage.WaitingForReinsertion);

                return Task.FromResult(new CardMoveResult(
                    CardWaitOutcome.Done,
                    CardMoveStage.WaitingForReinsertion,
                    before,
                    after,
                    after == null ? Snapshots.NoCard() : Snapshots.Card(after)));
            });

        /// <summary>The next move runs out of time in this stage.</summary>
        public void ThenTheMoveTimesOut(CardMoveStage stage) =>
            _moves.Enqueue((uid, stages, _) =>
            {
                Announce(stages, stage);

                return Task.FromResult(new CardMoveResult(
                    CardWaitOutcome.TimedOut,
                    stage,
                    uid,
                    null,
                    stage == CardMoveStage.WaitingForRemoval ? Snapshots.Card(uid ?? DefaultUid) : Snapshots.NoCard()));
            });

        /// <summary>The next move waits in this stage until it is cancelled, and then says so.</summary>
        public void ThenTheMoveWaitsInStageUntilCancelled(CardMoveStage stage) =>
            _moves.Enqueue(async (uid, stages, ct) =>
            {
                Announce(stages, stage);

                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    // The cancel is what is waited for.
                }

                return new CardMoveResult(CardWaitOutcome.Cancelled, stage, uid, null, null);
            });

        /// <summary>The next move is held until the gate opens and then ends well whatever the token says, as a look that has begun does.</summary>
        public void ThenTheMoveHoldsUntil(TaskCompletionSource<bool> gate, string? after) =>
            _moves.Enqueue(async (uid, stages, _) =>
            {
                Announce(stages, CardMoveStage.WaitingForReinsertion);
                await gate.Task;

                return new CardMoveResult(CardWaitOutcome.Done, CardMoveStage.WaitingForReinsertion, uid, after, Snapshots.Card(after ?? DefaultUid));
            });

        /// <summary>The next move announces the first stage and then fails with an exception.</summary>
        public void ThenTheMoveThrows(Exception exception) =>
            _moves.Enqueue((_, stages, _) =>
            {
                Announce(stages, CardMoveStage.WaitingForRemoval);

                throw exception;
            });

        public Task<CardMoveResult> WaitForRemovalAndReinsertionAsync(TimeSpan? timeout, string? cardBeforeUid, Action<CardMoveStage>? stageChanged, CancellationToken ct)
        {
            Moves.Add((timeout, cardBeforeUid));
            if (_moves.Count > 0)
            {
                return _moves.Dequeue()(cardBeforeUid, stageChanged, ct);
            }

            // Nothing scripted: the card that was on the reader is taken off and put back at once.
            var uid = cardBeforeUid ?? DefaultUid;
            Announce(stageChanged, CardMoveStage.WaitingForReinsertion);

            return Task.FromResult(new CardMoveResult(CardWaitOutcome.Done, CardMoveStage.WaitingForReinsertion, uid, uid, Snapshots.Card(uid)));
        }

        // The stages are announced as the real waiter does: the removal first (the card is still there), then the reinsertion.
        private static void Announce(Action<CardMoveStage>? stages, CardMoveStage upTo)
        {
            stages?.Invoke(CardMoveStage.WaitingForRemoval);
            if (upTo == CardMoveStage.WaitingForReinsertion)
            {
                stages?.Invoke(CardMoveStage.WaitingForReinsertion);
            }
        }
    }

    /// <summary>A report service whose "lock the fields" says yes and writes nothing, so the finished report is not there.</summary>
    internal sealed class NoFileReportService : IReportService
    {
        public Result<ReportAnalysis> Analyze(string reportPath) => throw new NotSupportedException();

        public Result WriteFields(string sourcePath, string targetPath, IReadOnlyDictionary<string, string> fields) => throw new NotSupportedException();

        public Result SetFieldsReadOnly(string sourcePath, string targetPath, bool readOnly) => Result.Ok();
    }

    /// <summary>The report files with a cleanup that throws, to show that a cleanup never undoes a finished order.</summary>
    internal sealed class ThrowingCleanupFiles : IReportFiles
    {
        private readonly IReportFiles _inner;

        public ThrowingCleanupFiles(IReportFiles inner) => _inner = inner;

        public Result<OrderReportPaths> Resolve(ReportLocation location, string jobNumber, string chipNumber) =>
            _inner.Resolve(location, jobNumber, chipNumber);

        public Result EnsureOrderFolder(OrderReportPaths paths) => _inner.EnsureOrderFolder(paths);

        public Result PrepareStep(OrderReportPaths paths, ReportFile file) => _inner.PrepareStep(paths, file);

        public CleanupReport Cleanup(OrderReportPaths paths, CleanupScope scope, bool removeTemporaryReports) =>
            throw new System.IO.IOException("secret path of the share");
    }
}
