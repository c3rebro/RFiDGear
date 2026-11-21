#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <param name="Answer">What this client sent: <c>approved</c>, <c>declined</c>, or <c>late</c> (RFiDGear no longer waited for the card when the answer came).</param>
    /// <param name="Reason">How RFiDGear ended the wait (<see cref="PresentationEndReasons"/>): the truth about what happened to the card.</param>
    /// <param name="RunId">The run that was admitted for an approved card.</param>
    public sealed record CardOutcome(string PresenceId, string Uid, string Answer, string Reason, string? RunId);

    /// <summary>
    /// The loop of a program that decides about every card in automatic mode (<c>autoMode.approval</c>): RFiDGear shows each card that
    /// is put on the reader and runs the project for it only when this loop approves it, once. The rules it keeps are the ones of the
    /// guide: <list type="bullet">
    /// <item>Whatever is not an approval ends as a refusal: a decision that throws, a project whose state cannot be told, a variable
    /// the project does not accept.</item>
    /// <item>A late answer (<c>approvalNotPending</c>) is not an error: the card was given up already, and the loop goes on with the next.</item>
    /// <item>The mode ends with the connection (<c>stopOnDisconnect</c>), and it is stopped when the loop ends.</item>
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
        /// <param name="decide">The program's own decision about a card: the job, the profile, the list of cards that were written already. It is called once per card and nothing was started when it is called.</param>
        /// <param name="approvalTimeout">How long RFiDGear waits for the decision about a card; its default (15 seconds) when <see langword="null"/>.</param>
        /// <param name="onOutcome">Called for every card when its wait has ended, with how it ended. Keep it short.</param>
        /// <param name="cancellationToken">Ends the loop (the mode is stopped); it does not stop a run that was started.</param>
        /// <returns>How every card that was shown ended.</returns>
        /// <exception cref="NotSupportedException">RFiDGear does not offer the approval.</exception>
        /// <exception cref="InvalidOperationException">RFiDGear started the mode but without the approval: it would run every card, so the mode is stopped again.</exception>
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

            var events = Channel.CreateUnbounded<BridgeEvent>();
            var answers = new Dictionary<string, (string Uid, string Answer)>(StringComparer.Ordinal);
            var outcomes = new List<CardOutcome>();

            // The handler only queues: it must be short and never call RFiDGear (that is what the loop below is for).
            void Queue(BridgeEvent e) => events.Writer.TryWrite(e);

            _client.EventReceived += Queue;

            try
            {
                var started = await _client.Service.StartAutoMode(new StartAutoModeRequest
                {
                    StopOnDisconnect = true,
                    RequireApproval = true,
                    ApprovalTimeoutMs = approvalTimeout == null ? null : (int)approvalTimeout.Value.TotalMilliseconds,
                }).WaitAsync(cancellationToken).ConfigureAwait(false);

                if (!started.RequireApproval)
                {
                    // A mode that runs every card is the one thing this loop must never leave switched on.
                    throw new InvalidOperationException("RFiDGear started automatic mode without waiting for approval; the mode is stopped.");
                }

                while (await NextAsync(events.Reader, cancellationToken).ConfigureAwait(false) is { } e)
                {
                    if (e is AutoModeChangedEvent { Enabled: false })
                    {
                        break;
                    }

                    if (e is CardPresentedEvent shown)
                    {
                        answers[shown.PresenceId] = (shown.Uid, await AnswerAsync(shown, decide, cancellationToken).ConfigureAwait(false));
                    }
                    else if (e is CardPresentationEndedEvent ended)
                    {
                        answers.TryGetValue(ended.PresenceId, out var answered);
                        var outcome = new CardOutcome(ended.PresenceId, answered.Uid ?? string.Empty, answered.Answer ?? "none", ended.Reason, ended.RunId);
                        answers.Remove(ended.PresenceId);
                        outcomes.Add(outcome);
                        onOutcome?.Invoke(outcome);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The caller ended the loop: what was shown and answered is in the list; the mode is stopped below.
            }
            finally
            {
                _client.EventReceived -= Queue;
                await StopQuietlyAsync().ConfigureAwait(false);
            }

            return outcomes;
        }

        // The next event, or null when the connection ended. Cancelling throws.
        private async Task<BridgeEvent?> NextAsync(ChannelReader<BridgeEvent> reader, CancellationToken cancellationToken)
        {
            var read = reader.ReadAsync(cancellationToken).AsTask();
            var finished = await Task.WhenAny(read, _client.Completion).ConfigureAwait(false);

            return finished == read ? await read.ConfigureAwait(false) : null;
        }

        // Decides, and sends the answer. Returns what was sent.
        private async Task<string> AnswerAsync(CardPresentedEvent shown, Func<CardPresentedEvent, CancellationToken, Task<CardDecision>> decide, CancellationToken cancellationToken)
        {
            CardDecision decision;

            try
            {
                decision = await decide(shown, cancellationToken).ConfigureAwait(false) ?? CardDecision.Decline;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // The program could not decide: that is no approval.
                decision = CardDecision.Decline;
            }

            // An approval that holds the project to the state the card was shown with needs that state. Without it there is nothing to hold.
            if (decision.Approve && decision.PinRevision && string.IsNullOrEmpty(shown.ProjectRevision))
            {
                decision = CardDecision.Decline;
            }

            cancellationToken.ThrowIfCancellationRequested();

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
    }
}
