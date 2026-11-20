#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Services.AutoMode;

using Serilog;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// The one client that decides about the cards of a mode it started: every card that automatic mode finds is shown to it as an
    /// event, and runs only if the client approves it, once, within the time limit. A card is waited for one at a time (automatic
    /// mode handles one card at a time), and everything that is not an approval ends the wait with nothing run: a refusal, no
    /// answer, a card that is no longer the one on the reader, a mode that was stopped, a connection that ended.
    /// <para>
    /// What the gate never does: it never approves anything itself, it never keeps an approval for a later card (the wait ends with
    /// the answer, whichever it is), it accepts an answer only from the connection that started the mode, and it accepts none after
    /// the time limit, whether or not its timer has noticed yet. A gate belongs to one start of the mode: once it was cancelled it
    /// shows no more cards.
    /// </para>
    /// </summary>
    internal sealed class AutoModeApprovalGate : IAutoModeApproval
    {
        private static ILogger Logger => Log.ForContext<AutoModeApprovalGate>();

        private readonly string _sessionId;
        private readonly TimeSpan _timeout;
        private readonly Func<string?> _readRevision;
        private readonly Action<string, bool, BridgeEventFactory> _publish;
        private readonly Func<DateTimeOffset> _now;
        private readonly object _gate = new object();
        private Pending? _pending;

        // Why the start this gate belongs to is over, once it is; guarded by _gate. A card that arrives after that is not shown.
        private string? _endedWith;

        /// <summary>Creates the gate of one start of the mode.</summary>
        /// <param name="sessionId">The connection that started the mode; only it can answer.</param>
        /// <param name="timeout">How long a card is waited for.</param>
        /// <param name="readRevision">Reads the revision of the project as it is now, on the thread that asks (the one of the main window); may return <see langword="null"/>.</param>
        /// <param name="publish">Hands an event to the client (name, whether it is durable, the maker of the event).</param>
        /// <param name="now">The clock, for the time the wait ends; the system clock when <see langword="null"/>.</param>
        internal AutoModeApprovalGate(
            string sessionId,
            TimeSpan timeout,
            Func<string?> readRevision,
            Action<string, bool, BridgeEventFactory> publish,
            Func<DateTimeOffset>? now = null)
        {
            _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            _timeout = timeout;
            _readRevision = readRevision ?? throw new ArgumentNullException(nameof(readRevision));
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
            _now = now ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>Gets how long a card is waited for.</summary>
        internal TimeSpan Timeout => _timeout;

        /// <summary>Gets the id of the presence that is waited for right now, or <see langword="null"/>.</summary>
        internal string? PendingPresenceId
        {
            get
            {
                lock (_gate)
                {
                    return _pending?.PresenceId;
                }
            }
        }

        // What ended the wait: an approval, or the reason that there is none.
        private readonly record struct Decision(AutoModeApproval? Approval, string Reason);

        private sealed class Pending
        {
            internal Pending(string presenceId, string uid, DateTimeOffset deadline)
            {
                PresenceId = presenceId;
                Uid = uid;
                Deadline = deadline;
            }

            internal string PresenceId { get; }

            internal string Uid { get; }

            // The time the wait ends, as the event said. An answer at or after it is no answer.
            internal DateTimeOffset Deadline { get; }

            // Completed on the thread of whoever decides, never inline: the machine goes on on its own thread.
            internal TaskCompletionSource<Decision> Completion { get; } = new TaskCompletionSource<Decision>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <inheritdoc/>
        public async Task<AutoModeApproval?> RequestAsync(AutoModePresence presence, CancellationToken withdrawn)
        {
            ArgumentNullException.ThrowIfNull(presence);

            var expiresAt = _now() + _timeout;
            var pending = new Pending(presence.PresenceId, presence.Uid, expiresAt);
            Pending? replaced;

            lock (_gate)
            {
                if (_endedWith != null)
                {
                    // The start this gate belongs to is over (the mode was stopped, or its connection ended) while the machine was on its
                    // way here: nobody is left to show the card to, and nobody is waited for.
                    return null;
                }

                replaced = _pending;
                _pending = pending;
            }

            // One card at a time: a wait that was left over (it cannot be, the machine handles one card at a time) ends as stopped.
            replaced?.Completion.TrySetResult(new Decision(null, PresentationEndReasons.Stopped));

            string? revision;

            try
            {
                revision = _readRevision();
            }
            catch (Exception e)
            {
                Logger.Debug("The revision of the project could not be read for a presented card ({ExceptionType})", e.GetType().FullName);
                revision = null;
            }

            _publish(
                BridgeEvents.CardPresented,
                true,
                (seq, sessionId, at) => new CardPresentedEvent
                {
                    Seq = seq,
                    SessionId = sessionId,
                    At = at,
                    PresenceId = presence.PresenceId,
                    Uid = presence.Uid,
                    ProjectRevision = revision,
                    ExpiresAt = expiresAt,
                });

            Decision decision;

            // The card is taken away or replaced while the client decides: the machine withdraws the wait, and the card is not run.
            using (withdrawn.Register(() => pending.Completion.TrySetResult(new Decision(null, PresentationEndReasons.CardChanged))))
            using (var stopWaiting = new CancellationTokenSource())
            {
                var timer = Task.Delay(_timeout, stopWaiting.Token);
                var first = await Task.WhenAny(pending.Completion.Task, timer).ConfigureAwait(false);

                if (first == pending.Completion.Task)
                {
                    stopWaiting.Cancel();
                    decision = await pending.Completion.Task.ConfigureAwait(false);
                }
                else
                {
                    // The time is up. An answer that came in the same moment wins if it got there first; the one that lost is refused
                    // by the completed task, never applied twice.
                    pending.Completion.TrySetResult(new Decision(null, PresentationEndReasons.Expired));
                    decision = await pending.Completion.Task.ConfigureAwait(false);
                }
            }

            lock (_gate)
            {
                if (ReferenceEquals(_pending, pending))
                {
                    _pending = null;
                }
            }

            if (decision.Approval == null)
            {
                PublishEnded(presence.PresenceId, decision.Reason, null);
            }

            return decision.Approval;
        }

        /// <inheritdoc/>
        public void RunRefused(string presenceId, AutoModeRefusal refusal)
        {
            var reason = refusal switch
            {
                AutoModeRefusal.ProjectChanged => PresentationEndReasons.ProjectChanged,
                AutoModeRefusal.CardChanged => PresentationEndReasons.CardChanged,
                _ => PresentationEndReasons.NotAdmitted,
            };

            PublishEnded(presenceId, reason, null);
        }

        /// <summary>
        /// Takes the approval of the card that is waited for, once. The approval is for that presence and no other, it must come before
        /// the time limit is up, and only the connection that started the mode can give it.
        /// </summary>
        /// <param name="sessionId">The connection that answers.</param>
        /// <param name="request">The answer.</param>
        /// <returns>That the approval was taken.</returns>
        internal ApproveCardResponse Approve(string sessionId, ApproveCardRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var pending = FindPending(sessionId, request.PresenceId);

            if (request.ExpectProjectRevision != null && !BridgeProtocol.IsValidRevision(request.ExpectProjectRevision))
            {
                throw BridgeFault.InvalidRequest("expectProjectRevision");
            }

            // The same checks as for the variables of startRun; a request that breaks them leaves the card waiting, so that the client can
            // answer again within the time (and nothing is started by the broken one).
            var checkedVariables = RunPlan.CheckVariables(request.Variables);
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in checkedVariables)
            {
                variables[pair.Key] = pair.Value;
            }

            var presenceId = pending.PresenceId;
            var approval = new AutoModeApproval(
                presenceId,
                pending.Uid,
                variables,
                request.ExpectProjectRevision,
                runId => PublishEnded(presenceId, PresentationEndReasons.Approved, runId));

            if (!pending.Completion.TrySetResult(new Decision(approval, PresentationEndReasons.Approved)))
            {
                throw BridgeFault.ApprovalNotPending();
            }

            return new ApproveCardResponse { PresenceId = presenceId };
        }

        /// <summary>Declines the card that is waited for: nothing is run for it.</summary>
        /// <param name="sessionId">The connection that answers.</param>
        /// <param name="request">The answer.</param>
        /// <returns>That the card was declined.</returns>
        internal DeclineCardResponse Decline(string sessionId, DeclineCardRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var pending = FindPending(sessionId, request.PresenceId);

            if (!pending.Completion.TrySetResult(new Decision(null, PresentationEndReasons.Declined)))
            {
                throw BridgeFault.ApprovalNotPending();
            }

            return new DeclineCardResponse { PresenceId = pending.PresenceId };
        }

        /// <summary>
        /// Ends the start this gate belongs to: the card that is waited for is not run, and no card that comes later is shown. Does
        /// nothing more when no card is waited for.
        /// </summary>
        /// <param name="reason">Why: <see cref="PresentationEndReasons.Stopped"/> or <see cref="PresentationEndReasons.ClientLost"/>.</param>
        internal void Cancel(string reason)
        {
            Pending? pending;

            lock (_gate)
            {
                _endedWith ??= reason;
                pending = _pending;
            }

            pending?.Completion.TrySetResult(new Decision(null, reason));
        }

        // The card that is waited for, if the answer is for it, comes from the connection that started the mode and is not late;
        // anything else is an answer to nothing.
        private Pending FindPending(string sessionId, string? presenceId)
        {
            Pending? pending;

            lock (_gate)
            {
                pending = _pending;
            }

            if (pending == null
                || !string.Equals(sessionId, _sessionId, StringComparison.Ordinal)
                || !string.Equals(presenceId, pending.PresenceId, StringComparison.Ordinal)
                || pending.Completion.Task.IsCompleted)
            {
                throw BridgeFault.ApprovalNotPending();
            }

            if (_now() >= pending.Deadline)
            {
                // The time is up whether or not the timer has noticed yet: an answer after the limit is no answer, and it ends the wait.
                pending.Completion.TrySetResult(new Decision(null, PresentationEndReasons.Expired));

                throw BridgeFault.ApprovalNotPending();
            }

            return pending;
        }

        private void PublishEnded(string presenceId, string reason, string? runId) =>
            _publish(
                BridgeEvents.CardPresentationEnded,
                true,
                (seq, sessionId, at) => new CardPresentationEndedEvent
                {
                    Seq = seq,
                    SessionId = sessionId,
                    At = at,
                    PresenceId = presenceId,
                    Reason = reason,
                    RunId = runId,
                });
    }
}
