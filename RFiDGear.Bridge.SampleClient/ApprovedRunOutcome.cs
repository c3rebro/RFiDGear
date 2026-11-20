#nullable enable
using System;

namespace RFiDGear.Bridge.SampleClient
{
    /// <summary>The card a program is asked to approve: what RFiDGear saw on the reader.</summary>
    /// <param name="Uid">The UID as upper-case hexadecimal digits, as the result of a run will write it.</param>
    /// <param name="TypeCode">The number RFiDGear gives the kind of card.</param>
    /// <param name="DisplayName">The name of the kind of card, for a person.</param>
    public sealed record ApprovedCard(string Uid, int TypeCode, string DisplayName);

    /// <summary>What came of <see cref="BridgeSampleClient.RunApprovedCardAsync"/>.</summary>
    public enum ApprovedRunKind
    {
        /// <summary>The card was approved and the run was started; <see cref="ApprovedRunOutcome.Run"/> says how it went.</summary>
        Ran,

        /// <summary>The program did not approve the card. Nothing was started.</summary>
        Declined,

        /// <summary>The reader could not be looked at (no reader, busy, the card could not be read). Nothing was started.</summary>
        ReaderNotReady,

        /// <summary>There was no card on the reader. Nothing was started.</summary>
        NoCard,

        /// <summary>More than one card, or none that RFiDGear takes as the main one: one card at a time is approved. Nothing was started.</summary>
        AmbiguousCard,
    }

    /// <summary>
    /// What one approved run found, with the fact that a program checks before it believes that it wrote the card it approved: the
    /// card the run read itself. (The state of the project is not checked here: RFiDGear refuses a run whose project is not the
    /// approved one, or the state an earlier run of this client left behind, before it starts. The revision the run started with is in
    /// the result, <c>Run.Result.ProjectRevision</c>, for the record of the job; in a series of cards it may be another than the one
    /// that was approved, because the tasks write some of their own settings.)
    /// </summary>
    /// <param name="Kind">What came of it.</param>
    /// <param name="ApprovedUid">The UID the program approved, when it got that far.</param>
    /// <param name="Run">How the run went, when one was started.</param>
    /// <param name="CardUidMatches">Whether the card that the run read at its start is the one that was approved.</param>
    /// <param name="Detail">A sentence for the operator; never a key, a path or a value of a variable.</param>
    public sealed record ApprovedRunOutcome(
        ApprovedRunKind Kind,
        string? ApprovedUid,
        RunOutcome? Run,
        bool CardUidMatches,
        string Detail)
    {
        /// <summary>
        /// Gets whether the run ended the way a program may record as "the approved card was written": it completed with the status
        /// of a run that did what the project says (a success, or the failures the project asked for) and the card that the run read is the
        /// approved one. Anything else is for a person to look at, and the card is looked at before the job is started again.
        /// </summary>
        public bool IsConfirmed =>
            Kind == ApprovedRunKind.Ran
            && Run is { Kind: RunOutcomeKind.Completed, Result: { } result }
            && (result.Status == RunStatuses.Success || result.Status == RunStatuses.CompletedWithIntentionalFailures)
            && CardUidMatches;
    }
}
