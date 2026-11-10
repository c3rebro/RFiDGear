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

        public TaskCompletionSource<bool>? ListGate { get; set; }

        public Result? FinalizeAnswer { get; set; }

        public TaskCompletionSource<bool>? FinalizeGate { get; set; }

        public int ListCalls { get; private set; }

        public List<(string Id, byte[] Report)> Finalized { get; } = new List<(string Id, byte[] Report)>();

        public async Task<Result<IReadOnlyList<OrderRecord>>> ListAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
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
                : Task.FromResult(new CardWaitResult(CardWaitOutcome.Done, Snapshots.Card("04AABBCCDD")));
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
