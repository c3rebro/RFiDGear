#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Services.AutoMode
{
    /// <summary>A card that was put on the reader, and waits for a decision.</summary>
    /// <param name="PresenceId">The id of this presence of the card; a card that was taken away and put back is another presence with another id.</param>
    /// <param name="Uid">The UID of the card as the poll of the reader read it.</param>
    internal sealed record AutoModePresence(string PresenceId, string Uid);

    /// <summary>
    /// What an approval gives for <b>one</b> run, on <b>one</b> card: the UID of the card it was given for (the run must find that card
    /// on the reader), the variables laid over those of the start-up for that run, the revision of the project that the approval was
    /// given for (if it says one), and the way to tell the one who approved that the run was admitted. It is used up by the run (or by
    /// the refusal to run), and a card that is presented again needs a new one.
    /// </summary>
    internal sealed class AutoModeApproval
    {
        private readonly Action<string>? _runAdmitted;
        private int _admitted;

        /// <summary>Creates the approval of one presence.</summary>
        /// <param name="presenceId">The presence it was given for.</param>
        /// <param name="uid">The UID of the card of that presence, as it was shown to the one who approved.</param>
        /// <param name="variables">The variables of the run; names are compared without regard to case.</param>
        /// <param name="expectProjectRevision">The revision the project has to have when the run is about to start, or <see langword="null"/> for none.</param>
        /// <param name="runAdmitted">Called with the id of the run when the run was admitted, or <see langword="null"/>.</param>
        internal AutoModeApproval(
            string presenceId,
            string uid,
            IReadOnlyDictionary<string, string> variables,
            string? expectProjectRevision,
            Action<string>? runAdmitted = null)
        {
            PresenceId = presenceId ?? throw new ArgumentNullException(nameof(presenceId));
            Uid = uid ?? throw new ArgumentNullException(nameof(uid));
            Variables = variables ?? throw new ArgumentNullException(nameof(variables));
            ExpectProjectRevision = expectProjectRevision;
            _runAdmitted = runAdmitted;
        }

        /// <summary>Gets the presence the approval was given for.</summary>
        internal string PresenceId { get; }

        /// <summary>Gets the UID of the card the approval was given for; the run is held to it, whatever the tasks of the project check.</summary>
        internal string Uid { get; }

        /// <summary>Gets the variables of the run; they win over the variables of the start-up.</summary>
        internal IReadOnlyDictionary<string, string> Variables { get; }

        /// <summary>Gets the revision the project must have when the run is about to start, or <see langword="null"/> when the approval does not say.</summary>
        internal string? ExpectProjectRevision { get; }

        /// <summary>Gets whether the run was admitted, that is, whether the one who approved was told so.</summary>
        internal bool RunWasAdmitted => Volatile.Read(ref _admitted) != 0;

        /// <summary>Tells the one who approved that the run was admitted. Never throws into the run.</summary>
        /// <param name="runId">The id of the run.</param>
        internal void NotifyRunAdmitted(string runId)
        {
            Volatile.Write(ref _admitted, 1);

            try
            {
                _runAdmitted?.Invoke(runId);
            }
            catch (Exception)
            {
                // What the one who approved does with the notice is no concern of the run.
            }
        }
    }

    /// <summary>Why a card that was approved (or not asked) did not get a run.</summary>
    internal enum AutoModeRefusal
    {
        /// <summary>Nothing was refused: a run was admitted.</summary>
        None,

        /// <summary>Something else held the execution slot, or the request was withdrawn; nothing ran and the card is tried again.</summary>
        NotAdmitted,

        /// <summary>The project was not in the state that the approval expected; nothing ran and the card is tried again.</summary>
        ProjectChanged,

        /// <summary>
        /// The card on the reader is not the card that was approved (it was taken away or replaced, or another run was made on it while
        /// the program decided); nothing ran. A card that is on the reader and was not processed otherwise is tried again as a new presence.
        /// </summary>
        CardChanged,

        /// <summary>The card was not approved (declined, no answer, mode stopped, connection lost); nothing ran and the card is not tried again until it was taken away.</summary>
        Declined,
    }

    /// <summary>
    /// What decides whether a card that was put on the reader gets a run: the program that started the mode. It is asked for one card
    /// at a time, and answers for that presence only.
    /// </summary>
    internal interface IAutoModeApproval
    {
        /// <summary>Tells the program about a card and waits for its decision.</summary>
        /// <param name="presence">The card.</param>
        /// <param name="withdrawn">Cancelled by the machine when the card is no longer the one on the reader (taken away or replaced) while the program decides; the wait then ends without an approval.</param>
        /// <returns>The approval for one run, or <see langword="null"/> when the card was not approved (declined, no answer within the time limit, the mode was stopped, the card was withdrawn, the program is gone).</returns>
        Task<AutoModeApproval?> RequestAsync(AutoModePresence presence, CancellationToken withdrawn);

        /// <summary>Tells the program that an approved card did not get its run.</summary>
        /// <param name="presenceId">The presence.</param>
        /// <param name="refusal">Why: <see cref="AutoModeRefusal.NotAdmitted"/>, <see cref="AutoModeRefusal.ProjectChanged"/> or <see cref="AutoModeRefusal.CardChanged"/>.</param>
        void RunRefused(string presenceId, AutoModeRefusal refusal);
    }
}
