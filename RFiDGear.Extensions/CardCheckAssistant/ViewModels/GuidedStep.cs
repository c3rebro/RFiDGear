using System;

using RFiDGear.Extensions.CardCheckAssistant.Workflow;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>What the window shows: one section for each step of the guided check.</summary>
    internal enum GuidedStep
    {
        /// <summary>The list of orders, from which one is chosen.</summary>
        Orders,

        /// <summary>The chosen order is being prepared.</summary>
        Prepare,

        /// <summary>The question whether the reports of an earlier check are replaced.</summary>
        Replace,

        /// <summary>The chip is awaited, or it has been seen and the check can be started.</summary>
        Card,

        /// <summary>The check runs, or it failed and can be repeated.</summary>
        Check,

        /// <summary>The verdict is shown and the order can be finished.</summary>
        Verdict,

        /// <summary>The finished report is made and stored, or that failed and can be repeated.</summary>
        Finish,

        /// <summary>The check has ended: finished, cancelled or faulted.</summary>
        End
    }

    /// <summary>Which step of the window shows a state of the workflow.</summary>
    internal static class GuidedSteps
    {
        /// <summary>The step that shows a state.</summary>
        /// <param name="state">The state of the workflow.</param>
        /// <returns>The step.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not a defined state.</exception>
        public static GuidedStep Of(WorkflowState state) => state switch
        {
            WorkflowState.Idle => GuidedStep.Orders,
            WorkflowState.LoadingOrders => GuidedStep.Orders,
            WorkflowState.Starting => GuidedStep.Prepare,
            WorkflowState.ConfirmingReplace => GuidedStep.Replace,
            WorkflowState.WaitingForCard => GuidedStep.Card,
            WorkflowState.Checking => GuidedStep.Check,
            WorkflowState.RunFailed => GuidedStep.Check,
            WorkflowState.Assessing => GuidedStep.Verdict,
            WorkflowState.Programming => GuidedStep.Verdict,
            WorkflowState.WritingReport => GuidedStep.Verdict,
            WorkflowState.ProgrammingFailed => GuidedStep.Verdict,
            WorkflowState.ReadingOut => GuidedStep.Check,
            WorkflowState.ReadOutFailed => GuidedStep.Check,
            WorkflowState.Reviewing => GuidedStep.Finish,
            WorkflowState.Completing => GuidedStep.Finish,
            WorkflowState.CompletionFailed => GuidedStep.Finish,
            WorkflowState.Completed => GuidedStep.End,
            WorkflowState.Cancelled => GuidedStep.End,
            WorkflowState.Faulted => GuidedStep.End,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state.")
        };
    }
}
