#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using StreamJsonRpc;

namespace RFiDGear.Bridge.SampleClient
{
    /// <summary>What the program that decides says about a card that was put on the reader.</summary>
    /// <param name="Approve"><see langword="true"/> to let RFiDGear run the project for this card, once; <see langword="false"/> to decline it.</param>
    /// <param name="Variables">The variables of this one run (for example <c>$EXPECTEDUID</c> set to the UID that was approved); <see langword="null"/> for none.</param>
    /// <param name="PinRevision">Whether the run may start only when the project is in the state that the card was shown with (<c>projectRevision</c> of the event). Leave it on.</param>
    public sealed record CardDecision(bool Approve, IReadOnlyDictionary<string, string>? Variables = null, bool PinRevision = true)
    {
        /// <summary>Gets the decision that runs nothing.</summary>
        public static CardDecision Decline { get; } = new CardDecision(false);

        /// <summary>Makes the decision that approves the card.</summary>
        /// <param name="variables">The variables of the run.</param>
        /// <param name="pinRevision">Whether the project has to be in the state the card was shown with.</param>
        /// <returns>The decision.</returns>
        public static CardDecision ApproveWith(IReadOnlyDictionary<string, string>? variables = null, bool pinRevision = true) =>
            new CardDecision(true, variables, pinRevision);
    }

    /// <summary>How a card that was shown ended: what this client answered, and what RFiDGear said about the end.</summary>
    /// <param name="PresenceId">The presence of the card (a card that is taken away and put back is another one).</param>
    /// <param name="Uid">The UID that was shown.</param>
    /// <param name="Answer">What this client sent: <c>approved</c>, <c>declined</c>, <c>late</c> (RFiDGear no longer waited for the card when the answer came), or <c>none</c> (the wait ended, or the loop was cancelled, before the program had decided: nothing was sent).</param>
    /// <param name="Reason">How RFiDGear ended the wait (<see cref="PresentationEndReasons"/>): the truth about what happened to the card.</param>
    /// <param name="RunId">The run that was admitted for an approved card.</param>
    public sealed record CardOutcome(string PresenceId, string Uid, string Answer, string Reason, string? RunId);

    /// <summary>
    /// The loop of a program that decides about every card in automatic mode (<c>autoMode.approval</c>): RFiDGear shows each card that
    /// is put on the reader and runs the project for it only when this loop approves it, once. The rules it keeps are the ones of the
    /// guide: <list type="bullet">
    /// <item>Whatever is not an approval ends as a refusal: a decision that throws (a time limit of its own included), a project whose
    /// state cannot be told, a variable the project does not accept.</item>
    /// <item>The decision about a card never holds up the events: it runs on its own, and it is dropped, with nothing answered, when
    /// RFiDGear ends the wait for that card or the loop ends.</item>
    /// <item>A late answer (<c>approvalNotPending</c>) is not an error: the card was given up already, and the loop goes on with the next.</item>
    /// <item>The mode ends with the connection (<c>stopOnDisconnect</c>), and it is stopped when the loop ends, but only a mode that this
    /// loop started: a mode that was on already (<c>autoModeActive</c>) is never touched.</item>
    /// <item>It never decides for a card it was not shown: the decision gets the event, and the answer carries its presence id.</item>
    /// </list>
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class ApprovalModeLoop
    {
        private readonly BridgeSampleClient _client;

        /// <summary>Creates the loop for a connected client.</summary>
        /// <param name="client">The client; it has to offer <see cref="BridgeCapabilities.AutoMode"/> and <see cref="BridgeCapabilities.AutoModeApproval"/>.</param>
        public ApprovalModeLoop(BridgeSampleClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>Switches automatic mode on for approval and decides about every card until the loop is cancelled, the mode stops or the connection ends.</summary>
        /// <param name="decide">The program's own decision about a card: the job, the profile, the list of cards that were written already. It is called once per card and nothing was started when it is called. Its token is cancelled when the wait for the card ends or the loop ends; a decision that ignores it is abandoned.</param>
        /// <param name="approvalTimeout">How long RFiDGear waits for the decision about a card; its default (15 seconds) when <see langword="null"/>.</param>
        /// <param name="onOutcome">Called for every card when its wait has ended, with how it ended. Keep it short.</param>
        /// <param name="cancellationToken">Ends the loop (the mode is stopped); it does not stop a run that was started.</param>
        /// <returns>How every card that was shown ended.</returns>
        /// <exception cref="NotSupportedException">RFiDGear does not offer the approval.</exception>
        /// <exception cref="InvalidOperationException">RFiDGear started the mode but without the approval: it would run every card, so the mode is stopped again.</exception>
        /// <exception cref="RemoteInvocationException">RFiDGear refused the start (<c>autoModeActive</c> when a mode is on already, which is left as it is).</exception>
        public async Task<IReadOnlyList<CardOutcome>> RunAsync(
            Func<CardPresentedEvent, CancellationToken, Task<CardDecision>> decide,
            TimeSpan? approvalTimeout = null,
            Action<CardOutcome>? onOutcome = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(decide);

            if (!_client.Offers(BridgeCapabilities.AutoMode) || !_client.Offers(BridgeCapabilities.AutoModeApproval))
            {
                throw new NotSupportedException("This RFiDGear does not offer the capabilities " + BridgeCapabilities.AutoMode + " and " + BridgeCapabilities.AutoModeApproval + ".");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var queue = Channel.CreateUnbounded<object>();
            var pending = new Dictionary<string, PendingCard>(StringComparer.Ordinal);
            var outcomes = new List<CardOutcome>();
            var weStartedTheMode = false;

            // The handler only queues: it must be short and never call RFiDGear (that is what the loop below is for).
            void Queue(BridgeEvent e) => queue.Writer.TryWrite(e);

            _client.EventReceived += Queue;

            try
            {
                // The answer to the start is never abandoned: a start that was given up on could still have switched the mode on, and
                // whether this loop owns the mode is what decides below if it may stop it.
                var started = await _client.Service.StartAutoMode(new StartAutoModeRequest
                {
                    StopOnDisconnect = true,
                    RequireApproval = true,
                    ApprovalTimeoutMs = approvalTimeout == null ? null : (int)approvalTimeout.Value.TotalMilliseconds,
                }).ConfigureAwait(false);

                weStartedTheMode = true;

                if (!started.RequireApproval)
                {
                    // A mode that runs every card is the one thing this loop must never leave switched on.
                    throw new InvalidOperationException("RFiDGear started automatic mode without waiting for approval; the mode is stopped.");
                }

                while (await NextAsync(queue.Reader, cancellationToken).ConfigureAwait(false) is { } item)
                {
                    if (item is AutoModeChangedEvent { Enabled: false })
                    {
                        // The mode is off already: a stop now could stop a mode that somebody else switched on since.
                        weStartedTheMode = false;

                        break;
                    }

                    if (item is AnswerFailed failed)
                    {
                        ExceptionDispatchInfo.Capture(failed.Error).Throw();
                    }
                    else if (item is CardPresentedEvent shown)
                    {
                        pending[shown.PresenceId] = Decide(shown, decide, queue.Writer, cancellationToken);
                    }
                    else if (item is CardPresentationEndedEvent ended)
                    {
                        var outcome = await EndAsync(ended, pending).ConfigureAwait(false);
                        outcomes.Add(outcome);
                        onOutcome?.Invoke(outcome);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller ended the loop: what was shown and answered is in the list; the mode is stopped below.
            }
            finally
            {
                _client.EventReceived -= Queue;

                // Whatever is still being decided is dropped first, so that nothing is answered for a mode that is being stopped.
                foreach (var card in pending.Values)
                {
                    card.Cancel.Cancel();
                }

                if (weStartedTheMode)
                {
                    await StopQuietlyAsync().ConfigureAwait(false);
                }

                await ObserveAsync(pending.Values).ConfigureAwait(false);
            }

            return outcomes;
        }

        // The next event, or null when the connection ended. Cancelling throws.
        private async Task<object?> NextAsync(ChannelReader<object> reader, CancellationToken cancellationToken)
        {
            var read = reader.ReadAsync(cancellationToken).AsTask();
            var finished = await Task.WhenAny(read, _client.Completion).ConfigureAwait(false);

            return finished == read ? await read.ConfigureAwait(false) : null;
        }

        // Starts the decision about a card on its own, so that the events go on while the program thinks. A failure of the answer is
        // handed to the loop as soon as it happens.
        private PendingCard Decide(CardPresentedEvent shown, Func<CardPresentedEvent, CancellationToken, Task<CardDecision>> decide, ChannelWriter<object> queue, CancellationToken cancellationToken)
        {
            var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var answer = Task.Run(() => AnswerAsync(shown, decide, cancel.Token), CancellationToken.None);

            _ = answer.ContinueWith(
                task => queue.TryWrite(new AnswerFailed(task.Exception!.GetBaseException())),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return new PendingCard(shown.Uid, cancel, answer);
        }

        // The wait for a card ended: a decision that is still under way is dropped, and nothing is answered for the card.
        private static async Task<CardOutcome> EndAsync(CardPresentationEndedEvent ended, Dictionary<string, PendingCard> pending)
        {
            var uid = string.Empty;
            var answered = "none";

            if (pending.Remove(ended.PresenceId, out var card))
            {
                uid = card.Uid;

                try
                {
                    card.Cancel.Cancel();
                    answered = await card.Answer.ConfigureAwait(false);
                }
                finally
                {
                    card.Cancel.Dispose();
                }
            }

            return new CardOutcome(ended.PresenceId, uid, answered, ended.Reason, ended.RunId);
        }

        // Waits for the decisions that were dropped to be over; their failures are of no interest any more.
        private static async Task ObserveAsync(IEnumerable<PendingCard> cards)
        {
            foreach (var card in cards)
            {
                try
                {
                    await card.Answer.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The loop ended, and so did this card.
                }
                finally
                {
                    card.Cancel.Dispose();
                }
            }
        }

        // Decides, and sends the answer. Returns what was sent (none when the card was given up before the program had decided).
        private async Task<string> AnswerAsync(CardPresentedEvent shown, Func<CardPresentedEvent, CancellationToken, Task<CardDecision>> decide, CancellationToken cardToken)
        {
            CardDecision decision;

            try
            {
                // A decision that ignores its token is abandoned, not waited for: the loop would never end otherwise.
                var deciding = Task.Run(() => decide(shown, cardToken), CancellationToken.None);

                _ = deciding.ContinueWith(
                    task => _ = task.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                decision = await deciding.WaitAsync(cardToken).ConfigureAwait(false) ?? CardDecision.Decline;
            }
            catch (OperationCanceledException) when (cardToken.IsCancellationRequested)
            {
                // The wait for the card ended or the loop did: there is nobody to answer.
                return "none";
            }
            catch (Exception)
            {
                // The program could not decide (a time limit of its own is that too): that is no approval.
                decision = CardDecision.Decline;
            }

            // An approval that holds the project to the state the card was shown with needs that state. Without it there is nothing to hold.
            if (decision.Approve && decision.PinRevision && string.IsNullOrEmpty(shown.ProjectRevision))
            {
                decision = CardDecision.Decline;
            }

            if (cardToken.IsCancellationRequested)
            {
                return "none";
            }

            try
            {
                if (!decision.Approve)
                {
                    await _client.Service.DeclineCard(new DeclineCardRequest { PresenceId = shown.PresenceId }).ConfigureAwait(false);

                    return "declined";
                }

                await _client.Service.ApproveCard(new ApproveCardRequest
                {
                    PresenceId = shown.PresenceId,
                    ExpectProjectRevision = decision.PinRevision ? shown.ProjectRevision : null,
                    Variables = (decision.Variables ?? new Dictionary<string, string>()).Select(pair => new VariablePair { Name = pair.Key, Value = pair.Value }).ToList(),
                }).ConfigureAwait(false);

                return "approved";
            }
            catch (RemoteInvocationException failure) when (BridgeSampleClient.ErrorDataOf(failure)?.Kind == BridgeErrorKinds.ApprovalNotPending)
            {
                // The card was given up already (the time was up, it was taken away, the mode stopped): nothing to do but go on.
                return "late";
            }
            catch (RemoteInvocationException failure) when (decision.Approve && BridgeSampleClient.ErrorDataOf(failure)?.Kind == BridgeErrorKinds.InvalidRequest)
            {
                // The approval was refused for what it said (a variable, a revision) and the card still waits: decline it, do not leave it to expire.
                await DeclineQuietlyAsync(shown.PresenceId).ConfigureAwait(false);

                return "declined";
            }
        }

        private async Task DeclineQuietlyAsync(string presenceId)
        {
            try
            {
                await _client.Service.DeclineCard(new DeclineCardRequest { PresenceId = presenceId }).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is RemoteInvocationException || failure is ConnectionLostException || failure is ObjectDisposedException)
            {
                // It ended meanwhile, or the connection is gone (which ends the mode): nothing is left to decline.
            }
        }

        private async Task StopQuietlyAsync()
        {
            try
            {
                await _client.Service.StopAutoMode(new StopAutoModeRequest()).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is RemoteInvocationException || failure is ConnectionLostException || failure is ObjectDisposedException)
            {
                // The connection is gone, and the mode ends with it.
            }
        }

        // A card whose decision is under way: what was shown, how to drop the decision, and what it will have answered.
        private sealed record PendingCard(string Uid, CancellationTokenSource Cancel, Task<string> Answer);

        // What the loop is told when the answer for a card failed (the connection, RFiDGear): it ends the loop with that failure.
        private sealed record AnswerFailed(Exception Error);
    }
}
