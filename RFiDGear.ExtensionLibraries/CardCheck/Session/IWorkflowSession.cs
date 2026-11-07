using System;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>The order a check is running for, as far as the workflow needs to know it.</summary>
    /// <param name="Id">The order's identifier in the order table.</param>
    /// <param name="JobNumber">The order's job number; names the report files.</param>
    /// <param name="ChipNumber">The order's chip number; names the report files.</param>
    public sealed record WorkflowOrder(string Id, string JobNumber, string ChipNumber);

    /// <summary>
    /// Everything one window knows about the check that is running in it. A snapshot never changes; each change
    /// produces a new one, so a reader always sees a consistent picture.
    /// </summary>
    public sealed record SessionSnapshot
    {
        /// <summary>The snapshot of a session in which nothing has begun.</summary>
        public static SessionSnapshot Empty { get; } = new SessionSnapshot();

        /// <summary>The order being checked, or <c>null</c> before one has begun.</summary>
        public WorkflowOrder? Order { get; init; }

        /// <summary>The language of this check.</summary>
        public ReportLanguage Language { get; init; } = ReportLanguage.German;

        /// <summary>What the first report said, or <c>null</c> until it has been read.</summary>
        public ReportAnalysis? Findings { get; init; }

        /// <summary>What the rules make of the findings; <c>null</c> exactly when <see cref="Findings"/> is.</summary>
        public TemplateSelection? Selection { get; init; }

        /// <summary>The template chosen for the report; one of <c>Selection.Available</c>, or <c>null</c>.</summary>
        public LsmTemplate? ChosenTemplate { get; init; }

        /// <summary>How many changes this session has had; grows by one with each change.</summary>
        public long Version { get; init; }
    }

    /// <summary>
    /// The state of one check in one window. Every window has its own instance (created by
    /// <see cref="CardCheckServices.CreateWorkflowSession"/>): nothing is shared between windows, and nothing is static.
    /// </summary>
    public interface IWorkflowSession
    {
        /// <summary>The current state; never <c>null</c>.</summary>
        SessionSnapshot Current { get; }

        /// <summary>
        /// Raised after every change, with the new snapshot, without any lock held, one event at a time and in the
        /// order of the versions: no handler sees an older snapshot after a newer one. It is raised on the thread that
        /// made the change, except that a change made while another change's event is being raised (by a handler, or by
        /// another thread) is raised afterwards by the thread that is already raising, so that call can return before
        /// its event has been delivered. A handler that throws does not stop the others or undo the change; the fault
        /// is logged.
        /// </summary>
        event EventHandler<SessionSnapshot>? Changed;

        /// <summary>Starts a check for an order, forgetting everything about the previous one.</summary>
        /// <param name="order">The order; its identifier must not be empty.</param>
        /// <param name="language">The language of this check.</param>
        /// <returns>Success, or why the order cannot be used.</returns>
        Result Begin(WorkflowOrder order, ReportLanguage language);

        /// <summary>
        /// Records what the first report said and what follows from it. The preselected template (if any) becomes
        /// the chosen one.
        /// </summary>
        /// <param name="analysis">What the first report said.</param>
        /// <returns>Success, or why it cannot be recorded (no check has begun).</returns>
        Result RecordFindings(ReportAnalysis analysis);

        /// <summary>Chooses one of the templates that fit the chip.</summary>
        /// <param name="template">The template; it must be one of the available ones.</param>
        /// <returns>Success, or why it cannot be chosen.</returns>
        Result ChooseTemplate(LsmTemplate template);

        /// <summary>Forgets the check, order included.</summary>
        void Reset();
    }
}
