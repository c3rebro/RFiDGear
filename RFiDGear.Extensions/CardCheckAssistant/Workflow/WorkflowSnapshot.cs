using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Workflow
{
    /// <summary>
    /// The stable codes of the failures the workflow itself ends with, for the page to turn into texts. A failure of
    /// the check run keeps its <see cref="CheckFailureCodes"/> code, and one of the library keeps the library's code.
    /// </summary>
    internal static class WorkflowFailureCodes
    {
        /// <summary>The order is not in the list of orders.</summary>
        public const string OrderUnknown = "workflow.order.unknown";

        /// <summary>The order is not in progress, so there is no check to make.</summary>
        public const string OrderNotStartable = "workflow.order.notStartable";

        /// <summary>Another window of the assistant is checking this order.</summary>
        public const string OrderInUse = "workflow.order.inUse";

        /// <summary>No project for the check is set in the settings.</summary>
        public const string ProjectNotConfigured = "workflow.project.notConfigured";

        /// <summary>Nobody put a card on the reader for a long time, so the wait ended.</summary>
        public const string WaitTimedOut = "workflow.wait.timeout";

        /// <summary>The card on the reader is another one than the operator saw.</summary>
        public const string CardChanged = "workflow.card.changed";

        /// <summary>The reader gave no clear answer (busy, failed or not there), so the check was not started.</summary>
        public const string CardUnclear = "workflow.card.unclear";

        /// <summary>The finished report was made but cannot be read.</summary>
        public const string FinalUnreadable = "workflow.final.unreadable";

        /// <summary>The project that reads out the memory of the chip is not set, or its file is not there.</summary>
        public const string MemoryProjectInvalid = "workflow.memoryProject.invalid";

        /// <summary>Nobody took the chip off the reader and put it back for a long time, so the wait ended.</summary>
        public const string CardMoveTimedOut = "workflow.card.moveTimedOut";

        /// <summary>No chip is on the reader, so the memory could not be read out.</summary>
        public const string CardMissing = "workflow.card.missing";

        /// <summary>Something nobody expected went wrong; nothing about the exception but its type is kept.</summary>
        public const string Faulted = "workflow.faulted";
    }

    /// <summary>The settings the workflow needs for one order, read when the order is started.</summary>
    /// <param name="Location">Where the reports of the order are kept (in test mode, the folder of the test reports).</param>
    /// <param name="RemoveTemporaryReports">Whether the working reports are removed after a finished check.</param>
    /// <param name="DefaultLanguage">The language the operator starts with.</param>
    /// <param name="DefaultProjectPath">The check project.</param>
    /// <param name="IsTestMode">Whether the orders are the made-up ones of the test mode.</param>
    internal sealed record WorkflowSettings(
        ReportLocation Location,
        bool RemoveTemporaryReports,
        ReportLanguage DefaultLanguage,
        string DefaultProjectPath,
        bool IsTestMode)
    {
        /// <summary>The project that reads out the memory of a programmed chip; empty if it is not usable (see <see cref="MemoryProjectProblem"/>).</summary>
        public string MemoryProjectPath { get; init; } = string.Empty;

        /// <summary>Why that project cannot be used (not set, not a full path, not there); <c>null</c> if it can. Without it a chip is not programmed, since the read-out would fail after the chip was changed.</summary>
        public Failure? MemoryProjectProblem { get; init; }

        /// <summary>The text blocks the operator can put into the notes of a report, in the order of the settings.</summary>
        public IReadOnlyList<TextTemplate> TextBlocks { get; init; } = Array.Empty<TextTemplate>();
    }

    /// <summary>The order store and the settings that belong together: an order is listed, checked and finished with the same pair.</summary>
    /// <param name="Store">The orders: the order system, or the made-up orders of the test mode.</param>
    /// <param name="Settings">The settings that go with that store.</param>
    internal sealed record WorkflowContext(IOrderStore Store, WorkflowSettings Settings);

    /// <summary>Where the workflow gets its store and settings. It is asked when the list is read and when an order is started; the answer is not kept in between.</summary>
    internal interface IWorkflowContextSource
    {
        /// <summary>Reads the settings and picks the store. It touches the disk, so the workflow calls it on a worker thread.</summary>
        /// <returns>The pair, or why there is none (no report folder, unreadable settings).</returns>
        Result<WorkflowContext> Resolve();
    }

    /// <summary>The check run, as the workflow uses it. <see cref="CheckRunner"/> is the implementation.</summary>
    internal interface ICheckRunner
    {
        /// <summary>Runs the check and reads its report; see <see cref="CheckRunner.RunAsync"/>.</summary>
        /// <param name="request">What to run.</param>
        /// <param name="phaseChanged">Called when the run enters a phase; may be <c>null</c>.</param>
        /// <param name="ct">Cancels the run.</param>
        /// <returns>How the run ended.</returns>
        Task<CheckRunResult> RunAsync(CheckRunRequest request, Action<CheckPhase>? phaseChanged, CancellationToken ct);
    }

    /// <summary>The wait for a card, as the workflow uses it. <see cref="CardWaiter"/> is the implementation.</summary>
    internal interface ICardWaiter
    {
        /// <summary>Waits until the reader shows a card; see <see cref="CardWaiter.WaitForCardAsync"/>.</summary>
        /// <param name="timeout">How long to wait; <c>null</c> to wait until cancelled. A time of zero looks once.</param>
        /// <param name="ct">Cancels the wait.</param>
        /// <returns>How the wait ended.</returns>
        Task<CardWaitResult> WaitForCardAsync(TimeSpan? timeout, CancellationToken ct);

        /// <summary>Waits until the card is taken off the reader and a card is put on it; see <see cref="CardWaiter.WaitForRemovalAndReinsertionAsync"/>.</summary>
        /// <param name="timeout">How long to wait for both together; <c>null</c> to wait until cancelled.</param>
        /// <param name="cardBeforeUid">The UID of the card that is on the reader now, if known.</param>
        /// <param name="stageChanged">Called when the stage is known and every time it changes; may be <c>null</c>.</param>
        /// <param name="ct">Cancels the wait.</param>
        /// <returns>How the wait ended, with the UIDs before and after.</returns>
        Task<CardMoveResult> WaitForRemovalAndReinsertionAsync(TimeSpan? timeout, string? cardBeforeUid, Action<CardMoveStage>? stageChanged, CancellationToken ct);
    }

    /// <summary>
    /// Everything the page needs to draw the workflow, as one immutable picture. Each change makes a new one; the
    /// workflow raises an event with it.
    /// </summary>
    internal sealed record WorkflowSnapshot
    {
        /// <summary>The state.</summary>
        public WorkflowState State { get; init; } = WorkflowState.Idle;

        /// <summary>Whether something is running (the list is read, an order prepared, a card awaited, the check or the upload running). Then only Cancel is offered, and Completing offers nothing.</summary>
        public bool IsBusy { get; init; }

        /// <summary>Whether a cancel was asked for and the running step has not ended yet; a second cancel is not accepted.</summary>
        public bool IsCancelling { get; init; }

        /// <summary>What the operator may do now.</summary>
        public WorkflowControls Controls { get; init; }

        /// <summary>What closing the window means now.</summary>
        public CloseAdvice Close { get; init; } = CloseAdvice.Allow;

        /// <summary>Whether the orders are the made-up ones of the test mode (as of the last time the list was read or an order started).</summary>
        public bool IsTestMode { get; init; }

        /// <summary>The language the operator starts with.</summary>
        public ReportLanguage DefaultLanguage { get; init; } = ReportLanguage.German;

        /// <summary>The orders as last read; a failed read leaves the last good list.</summary>
        public IReadOnlyList<OrderRecord> Orders { get; init; } = Array.Empty<OrderRecord>();

        /// <summary>Why the last read of the list failed; <c>null</c> if it did not.</summary>
        public Failure? OrdersFailure { get; init; }

        /// <summary>The order being checked; <c>null</c> in <see cref="WorkflowState.Idle"/> and while the list is read.</summary>
        public WorkflowOrder? Order { get; init; }

        /// <summary>The language of this check.</summary>
        public ReportLanguage Language { get; init; } = ReportLanguage.German;

        /// <summary>The reports of an earlier attempt that exist; asked about in <see cref="WorkflowState.ConfirmingReplace"/>.</summary>
        public IReadOnlyList<ReportFile> ExistingReports { get; init; } = Array.Empty<ReportFile>();

        /// <summary>The card that was seen on the reader; <c>null</c> while none has been.</summary>
        public ReaderSnapshot? Card { get; init; }

        /// <summary>Where the check run is, while it runs.</summary>
        public CheckPhase? Phase { get; init; }

        /// <summary>The text blocks for the notes of a report, as last read from the settings (like <see cref="DefaultLanguage"/>, they stay when the check ends).</summary>
        public IReadOnlyList<TextTemplate> TextBlocks { get; init; } = Array.Empty<TextTemplate>();

        /// <summary>Where the card's way off the reader and back on is, while the workflow waits for it; <c>null</c> before the first look.</summary>
        public CardMoveStage? MoveStage { get; init; }

        /// <summary>
        /// How the last wait for the card's way off the reader and back on ended: <see cref="CardMoveResult.Outcome"/> says
        /// whether the card was put back or the wait ran out, and <see cref="CardMoveResult.SameCard"/> whether it is the one
        /// that was taken off. <c>null</c> before that, and while it is awaited again.
        /// </summary>
        public CardMoveResult? Move { get; init; }

        /// <summary>What the run that read the memory out came to (its report is in <see cref="CheckRunResult.ReportPath"/>); <c>null</c> before it ran and while it is run again. Kept apart from <see cref="Check"/>, which is the first run.</summary>
        public CheckRunResult? ReadOut { get; init; }

        /// <summary>What the check run came to (the task outcomes are in <see cref="CheckRunResult.Execution"/>).</summary>
        public CheckRunResult? Check { get; init; }

        /// <summary>What the first report says; <c>null</c> before the check has run.</summary>
        public ReportAnalysis? Findings { get; init; }

        /// <summary>What the rules make of the findings: the verdict, the memory and the templates.</summary>
        public TemplateSelection? Selection { get; init; }

        /// <summary>Why a chip that could be programmed is not offered for programming (the project that reads out its memory cannot be used); <c>null</c> if it is offered, or if the chip cannot be programmed anyway. It is not a <see cref="Failure"/>: nothing went wrong, the page shows it next to the verdict when it has a button to explain.</summary>
        public Failure? ProgrammingProblem { get; init; }

        /// <summary>Why the last step did not work, or a note the operator should see (a refused start, a failed run or upload, a wait that ran out); <c>null</c> if there is none.</summary>
        public Failure? Failure { get; init; }

        /// <summary>The state the workflow was in when it was cancelled, so the page can say whether an upload may have happened.</summary>
        public WorkflowState? CancelledAt { get; init; }

        /// <summary>What removing the working reports did; <c>null</c> before the order is finished.</summary>
        public CleanupReport? Cleanup { get; init; }

        /// <summary>Whether removing the working reports went wrong (the order is finished all the same).</summary>
        public bool CleanupFailed { get; init; }

        /// <summary>How many changes there have been; grows by one with each.</summary>
        public long Version { get; init; }
    }
}
