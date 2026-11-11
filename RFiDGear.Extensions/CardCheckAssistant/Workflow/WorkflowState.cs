using System;
using System.Collections.Generic;
using System.Linq;

namespace RFiDGear.Extensions.CardCheckAssistant.Workflow
{
    /// <summary>Where the guided workflow is. Every state has a defined set of things the operator may do.</summary>
    internal enum WorkflowState
    {
        /// <summary>No order is being checked; the order list is shown.</summary>
        Idle,

        /// <summary>The order list is being read.</summary>
        LoadingOrders,

        /// <summary>An order was chosen and is being prepared: the list is read again, the settings and the report paths are checked.</summary>
        Starting,

        /// <summary>Reports of the order exist from an earlier attempt; the operator is asked whether to replace them.</summary>
        ConfirmingReplace,

        /// <summary>A card is awaited, or it has been seen and the check can be started.</summary>
        WaitingForCard,

        /// <summary>The check project is being run (the card is looked at once more first).</summary>
        Checking,

        /// <summary>The check ran and the verdict is shown.</summary>
        Assessing,

        /// <summary>The chip is being programmed: it is taken off the reader, programmed elsewhere and put back.</summary>
        Programming,

        /// <summary>The operator's answers are being written into the second report, and the change date of the order is set.</summary>
        WritingReport,

        /// <summary>The second report or the change date could not be written; the operator can try again or cancel.</summary>
        ProgrammingFailed,

        /// <summary>The memory of the programmed chip is being read out.</summary>
        ReadingOut,

        /// <summary>The memory could not be read out; the operator can try again, finish without the read-out, or cancel.</summary>
        ReadOutFailed,

        /// <summary>The read-out report is made and the operator checks it before the order is finished.</summary>
        Reviewing,

        /// <summary>The check run failed; the operator can try again or cancel.</summary>
        RunFailed,

        /// <summary>The finished report is being made and uploaded.</summary>
        Completing,

        /// <summary>The finished report could not be made or uploaded; the operator can try again or cancel.</summary>
        CompletionFailed,

        /// <summary>The order is finished.</summary>
        Completed,

        /// <summary>The check was cancelled and nothing was uploaded by it (after a failed upload the order may be finished all the same; the snapshot says where the cancel came).</summary>
        Cancelled,

        /// <summary>Something nobody expected went wrong.</summary>
        Faulted
    }

    /// <summary>What the operator may do in a state. A control that is not in the set is not offered.</summary>
    [Flags]
    internal enum WorkflowControls
    {
        /// <summary>Nothing.</summary>
        None = 0,

        /// <summary>Read the order list again.</summary>
        Refresh = 1,

        /// <summary>Start the check of an order.</summary>
        Start = 2,

        /// <summary>Replace the reports of the earlier attempt.</summary>
        ConfirmReplace = 4,

        /// <summary>Keep the reports of the earlier attempt and go back to the list.</summary>
        DeclineReplace = 8,

        /// <summary>Run the check with the card that was seen.</summary>
        StartCheck = 16,

        /// <summary>Make the finished report and upload it.</summary>
        Finish = 32,

        /// <summary>Try the failed step again.</summary>
        Retry = 64,

        /// <summary>Cancel what is going on.</summary>
        Cancel = 128,

        /// <summary>Go back to the order list.</summary>
        Back = 256,

        /// <summary>Begin to program the chip: wait for it to be taken off the reader and put back.</summary>
        BeginProgramming = 512,

        /// <summary>The chip was put back: write the answers into the report and read the memory out.</summary>
        ContinueProgramming = 1024,

        /// <summary>Finish the order with the report made before programming, because the memory could not be read out.</summary>
        FinishWithoutReadOut = 2048
    }

    /// <summary>What closing the window means in a state.</summary>
    internal enum CloseAdvice
    {
        /// <summary>Nothing is in progress.</summary>
        Allow,

        /// <summary>A check is in progress but nothing is running that matters (a wait for a card can be dropped): closing abandons it, after a question.</summary>
        Confirm,

        /// <summary>Something is running that cannot be stopped at once: the host stops a run only between two tasks, and an upload must be finished.</summary>
        Refuse
    }

    /// <summary>
    /// The rules of the workflow as tables: which state can follow which, what the operator may do in each, and what
    /// closing the window means. A state without an entry in every table is a defect, and a test demands them all.
    /// </summary>
    internal static class WorkflowTables
    {
        private static readonly Dictionary<WorkflowState, WorkflowState[]> Transitions = new Dictionary<WorkflowState, WorkflowState[]>
        {
            [WorkflowState.Idle] = new[] { WorkflowState.LoadingOrders, WorkflowState.Starting, WorkflowState.Faulted },
            [WorkflowState.LoadingOrders] = new[] { WorkflowState.Idle, WorkflowState.Faulted },
            [WorkflowState.Starting] = new[] { WorkflowState.ConfirmingReplace, WorkflowState.WaitingForCard, WorkflowState.Idle, WorkflowState.Faulted },
            [WorkflowState.ConfirmingReplace] = new[] { WorkflowState.WaitingForCard, WorkflowState.Idle, WorkflowState.Faulted },
            [WorkflowState.WaitingForCard] = new[] { WorkflowState.Checking, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.Checking] = new[] { WorkflowState.Assessing, WorkflowState.RunFailed, WorkflowState.WaitingForCard, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.Assessing] = new[] { WorkflowState.Completing, WorkflowState.Programming, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.Programming] = new[] { WorkflowState.WritingReport, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.WritingReport] = new[] { WorkflowState.ReadingOut, WorkflowState.ProgrammingFailed, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.ProgrammingFailed] = new[] { WorkflowState.WritingReport, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.ReadingOut] = new[] { WorkflowState.Reviewing, WorkflowState.ReadOutFailed, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.ReadOutFailed] = new[] { WorkflowState.ReadingOut, WorkflowState.Completing, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.Reviewing] = new[] { WorkflowState.Completing, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.RunFailed] = new[] { WorkflowState.WaitingForCard, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.Completing] = new[] { WorkflowState.Completed, WorkflowState.CompletionFailed, WorkflowState.Faulted },
            [WorkflowState.CompletionFailed] = new[] { WorkflowState.Completing, WorkflowState.Cancelled, WorkflowState.Faulted },
            [WorkflowState.Completed] = new[] { WorkflowState.Idle },
            [WorkflowState.Cancelled] = new[] { WorkflowState.Idle },
            [WorkflowState.Faulted] = new[] { WorkflowState.Idle },
        };

        // What the operator may do in a state when nothing is running. While something runs only Cancel is left, and
        // the engine narrows the set further (a card must have been seen to start the check, and so on).
        private static readonly Dictionary<WorkflowState, WorkflowControls> Controls = new Dictionary<WorkflowState, WorkflowControls>
        {
            [WorkflowState.Idle] = WorkflowControls.Refresh | WorkflowControls.Start,
            [WorkflowState.LoadingOrders] = WorkflowControls.Cancel,
            [WorkflowState.Starting] = WorkflowControls.Cancel,
            [WorkflowState.ConfirmingReplace] = WorkflowControls.ConfirmReplace | WorkflowControls.DeclineReplace,
            [WorkflowState.WaitingForCard] = WorkflowControls.StartCheck | WorkflowControls.Cancel,
            [WorkflowState.Checking] = WorkflowControls.Cancel,
            [WorkflowState.Assessing] = WorkflowControls.Finish | WorkflowControls.BeginProgramming | WorkflowControls.Cancel,
            [WorkflowState.Programming] = WorkflowControls.ContinueProgramming | WorkflowControls.Retry | WorkflowControls.Cancel,
            [WorkflowState.WritingReport] = WorkflowControls.Cancel,
            [WorkflowState.ProgrammingFailed] = WorkflowControls.Retry | WorkflowControls.Cancel,
            [WorkflowState.ReadingOut] = WorkflowControls.Cancel,
            [WorkflowState.ReadOutFailed] = WorkflowControls.Retry | WorkflowControls.FinishWithoutReadOut | WorkflowControls.Cancel,
            [WorkflowState.Reviewing] = WorkflowControls.Finish | WorkflowControls.Cancel,
            [WorkflowState.RunFailed] = WorkflowControls.Retry | WorkflowControls.Cancel,
            [WorkflowState.Completing] = WorkflowControls.None,
            [WorkflowState.CompletionFailed] = WorkflowControls.Retry | WorkflowControls.Cancel,
            [WorkflowState.Completed] = WorkflowControls.Back,
            [WorkflowState.Cancelled] = WorkflowControls.Back,
            [WorkflowState.Faulted] = WorkflowControls.Back,
        };

        private static readonly Dictionary<WorkflowState, CloseAdvice> Close = new Dictionary<WorkflowState, CloseAdvice>
        {
            [WorkflowState.Idle] = CloseAdvice.Allow,
            [WorkflowState.LoadingOrders] = CloseAdvice.Allow,
            [WorkflowState.Starting] = CloseAdvice.Refuse,
            [WorkflowState.ConfirmingReplace] = CloseAdvice.Confirm,
            [WorkflowState.WaitingForCard] = CloseAdvice.Confirm,
            [WorkflowState.Checking] = CloseAdvice.Refuse,
            [WorkflowState.Assessing] = CloseAdvice.Confirm,
            [WorkflowState.Programming] = CloseAdvice.Confirm,
            [WorkflowState.WritingReport] = CloseAdvice.Refuse,
            [WorkflowState.ProgrammingFailed] = CloseAdvice.Confirm,
            [WorkflowState.ReadingOut] = CloseAdvice.Refuse,
            [WorkflowState.ReadOutFailed] = CloseAdvice.Confirm,
            [WorkflowState.Reviewing] = CloseAdvice.Confirm,
            [WorkflowState.RunFailed] = CloseAdvice.Confirm,
            [WorkflowState.Completing] = CloseAdvice.Refuse,
            [WorkflowState.CompletionFailed] = CloseAdvice.Confirm,
            [WorkflowState.Completed] = CloseAdvice.Allow,
            [WorkflowState.Cancelled] = CloseAdvice.Allow,
            [WorkflowState.Faulted] = CloseAdvice.Allow,
        };

        /// <summary>Every state, in the order of the enumeration.</summary>
        public static IReadOnlyList<WorkflowState> States { get; } = (WorkflowState[])Enum.GetValues(typeof(WorkflowState));

        /// <summary>Whether a state is the end of a check: from here the only way on is back to the list.</summary>
        /// <param name="state">The state.</param>
        /// <returns><c>true</c> for <see cref="WorkflowState.Completed"/>, <see cref="WorkflowState.Cancelled"/> and <see cref="WorkflowState.Faulted"/>.</returns>
        public static bool IsEnd(WorkflowState state) =>
            state == WorkflowState.Completed || state == WorkflowState.Cancelled || state == WorkflowState.Faulted;

        /// <summary>The states that may follow a state.</summary>
        /// <param name="from">The state.</param>
        /// <returns>The states; never <c>null</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="from"/> is not a defined state.</exception>
        public static IReadOnlyList<WorkflowState> Next(WorkflowState from) =>
            Transitions.TryGetValue(from, out var next) ? next : throw new ArgumentOutOfRangeException(nameof(from), from, "Unknown state.");

        /// <summary>Whether a state may follow another. A state following itself is a change of its details, not a transition, and is not asked.</summary>
        /// <param name="from">The state the workflow is in.</param>
        /// <param name="to">The state it is to move to.</param>
        /// <returns><c>true</c> if the table allows it.</returns>
        public static bool IsAllowed(WorkflowState from, WorkflowState to) => Next(from).Contains(to);

        /// <summary>What the operator may do in a state when nothing is running.</summary>
        /// <param name="state">The state.</param>
        /// <returns>The controls.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not a defined state.</exception>
        public static WorkflowControls ControlsOf(WorkflowState state) =>
            Controls.TryGetValue(state, out var controls) ? controls : throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state.");

        /// <summary>What closing the window means in a state.</summary>
        /// <param name="state">The state.</param>
        /// <returns>The advice.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not a defined state.</exception>
        public static CloseAdvice CloseAdviceOf(WorkflowState state) =>
            Close.TryGetValue(state, out var advice) ? advice : throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state.");
    }
}
