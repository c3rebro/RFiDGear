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
    /// <param name="Answer">What this client sent: <c>approved</c>, <c>declined</c>, <c>late</c> (RFiDGear no longer waited for the card when the answer came), <c>pending</c> (the answer was sent and RFiDGear had not replied to it when the wait ended; its reply is not waited for any longer), <c>none</c> (the wait ended, or the loop was cancelled, before the program had decided: nothing was sent), or <c>failed</c> (the request failed after the wait had ended; what RFiDGear says about the card is <paramref name="Reason"/>).</param>
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
    /// RFiDGear ends the wait for that card or the loop ends. Its token is cancelled on the thread pool and without waiting, so that a
    /// callback of the program that blocks, or throws, holds up nothing. An answer that RFiDGear has not replied to yet is not waited for
    /// longer than <see cref="Grace"/>, and is never sent twice.</item>
    /// <item>A late answer (<c>approvalNotPending</c>) is not an error: the card was given up already, and the loop goes on with the next.</item>
    /// <item>The mode ends with the connection (<c>stopOnDisconnect</c>). When the loop ends it stops the mode, but only a mode that it
    /// started and that RFiDGear still reports as the mode of a client (<c>startedBy</c> <c>bridge</c>; a mode of a user is
    /// <c>other</c>): a mode that was on already (<c>autoModeActive</c>), one that ended, and one that a user switched on since are
    /// never touched. A mode that RFiDGear started without waiting for approval is such a mode too, and is stopped. A program that owns
    /// its connection ends its mode without any such question by closing it. Neither request waits for longer than
    /// <see cref="Grace"/>, and whatever they do, the loop ends.</item>
    /// <item>A notice that a mode ended is checked against the status of RFiDGear before it ends the loop: the loop listens from before
    /// its own start, so a notice about an earlier mode can be the first thing it reads, and its own mode is on.</item>
    /// <item>Every card that was shown is reported when RFiDGear says how its wait ended, also when the loop is cancelled or the mode
    /// stops meanwhile (for as long as <see cref="Grace"/>; what RFiDGear does not say is not made up). A card that was shown and that
    /// the loop had not looked at yet when it ended is reported as one that got no answer. The end of a wait for a card that this loop
    /// was never shown (a late notice of an earlier mode on the same connection) is ignored.</item>
    /// <item>It never decides for a card it was not shown: the decision gets the event, and the answer carries its presence id.</item>
    /// </list>
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class ApprovalModeLoop
    {
        private readonly BridgeSampleClient _client;
        private readonly IBridgeService _service;

        /// <summary>Creates the loop for a connected client.</summary>
        /// <param name="client">The client; it has to offer <see cref="BridgeCapabilities.AutoMode"/> and <see cref="BridgeCapabilities.AutoModeApproval"/>.</param>
        public ApprovalModeLoop(BridgeSampleClient client)
            : this(client, (client ?? throw new ArgumentNullException(nameof(client))).Service)
        {
        }

        // The service is a parameter for the tests, which put a stand-in between the loop and RFiDGear to make a request slow.
        internal ApprovalModeLoop(BridgeSampleClient client, IBridgeService service)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>Gets how long the loop waits, at the most, for an answer that RFiDGear has not replied to yet, for each of the two requests that stop the mode, and for the end of the waits that were still going on when the loop ended.</summary>
        public TimeSpan Grace { get; init; } = TimeSpan.FromSeconds(2);

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

            return await new Run(this, decide, onOutcome).RunAsync(approvalTimeout, cancellationToken).ConfigureAwait(false);
        }

        // One run of the loop: what is shown and not ended yet, and what has ended.
        private sealed class Run
        {
            private readonly ApprovalModeLoop _owner;
            private readonly Func<CardPresentedEvent, CancellationToken, Task<CardDecision>> _decide;
            private readonly Action<CardOutcome>? _onOutcome;
            private readonly Channel<object> _queue = Channel.CreateUnbounded<object>();
            private readonly Dictionary<string, PendingCard> _pending = new Dictionary<string, PendingCard>(StringComparer.Ordinal);
            private readonly List<CardOutcome> _outcomes = new List<CardOutcome>();
            private bool _weStartedTheMode;

            internal Run(ApprovalModeLoop owner, Func<CardPresentedEvent, CancellationToken, Task<CardDecision>> decide, Action<CardOutcome>? onOutcome)
            {
                _owner = owner;
                _decide = decide;
                _onOutcome = onOutcome;
            }

            internal async Task<IReadOnlyList<CardOutcome>> RunAsync(TimeSpan? approvalTimeout, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // The handler only queues: it must be short and never call RFiDGear (that is what the loop below is for).
                void Queue(BridgeEvent e) => _queue.Writer.TryWrite(e);

                _owner._client.EventReceived += Queue;

                try
                {
                    await StartAsync(approvalTimeout).ConfigureAwait(false);

                    while (await NextAsync(_queue.Reader, cancellationToken).ConfigureAwait(false) is { } item)
                    {
                        if (item is AutoModeChangedEvent { Enabled: false })
                        {
                            var ownModeIsOn = await OwnModeIsOnAsync().ConfigureAwait(false);

                            if (ownModeIsOn == true)
                            {
                                // A notice about a mode that ended before this loop's own was started: the loop listens from before the
                                // start, so it can come first. The loop's own mode is on, and nobody else decides about its cards.
                                continue;
                            }

                            // The mode is off already (a stop now could stop a mode that somebody else switched on since), or RFiDGear
                            // cannot say, and then the stop below asks once more.
                            _weStartedTheMode = ownModeIsOn == null;

                            break;
                        }

                        await HandleAsync(item).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The caller ended the loop: what was shown and answered is in the list; the mode is stopped below.
                }
                finally
                {
                    try
                    {
                        await ShutDownAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        _owner._client.EventReceived -= Queue;
                    }
                }

                return _outcomes;
            }

            private async Task StartAsync(TimeSpan? approvalTimeout)
            {
                // The answer to the start is never abandoned: a start that was given up on could still have switched the mode on, and
                // whether this loop owns the mode is what decides if it may stop it.
                var started = await _owner._service.StartAutoMode(new StartAutoModeRequest
                {
                    StopOnDisconnect = true,
                    RequireApproval = true,
                    ApprovalTimeoutMs = approvalTimeout == null ? null : (int)approvalTimeout.Value.TotalMilliseconds,
                }).ConfigureAwait(false);

                _weStartedTheMode = true;

                if (!started.RequireApproval)
                {
                    // A mode that runs every card is the one thing this loop must never leave switched on.
                    throw new InvalidOperationException("RFiDGear started automatic mode without waiting for approval; the mode is stopped.");
                }
            }

            // The next item, or null when the connection ended. Cancelling throws.
            private async Task<object?> NextAsync(ChannelReader<object> reader, CancellationToken cancellationToken)
            {
                var read = reader.ReadAsync(cancellationToken).AsTask();
                var finished = await Task.WhenAny(read, _owner._client.Completion).ConfigureAwait(false);

                return finished == read ? await read.ConfigureAwait(false) : null;
            }

            private async Task HandleAsync(object item)
            {
                switch (item)
                {
                    case AnswerFailed failed:
                        // An answer that failed for a card that is over already is of no interest any more.
                        if (_pending.ContainsKey(failed.PresenceId))
                        {
                            ExceptionDispatchInfo.Capture(failed.Error).Throw();
                        }

                        break;
                    case CardPresentedEvent shown:
                        _pending[shown.PresenceId] = StartDeciding(shown);
                        break;
                    case CardPresentationEndedEvent ended:
                        if (await EndAsync(ended).ConfigureAwait(false) is { } outcome)
                        {
                            Report(outcome);
                        }

                        break;
                    default:
                        // Whatever else was queued is no business of the loop.
                        break;
                }
            }

            // Starts the decision about a card on its own, so that the events go on while the program thinks. A failure of the answer
            // is handed to the loop as soon as it happens. The token of the decision is not linked to the token of the caller: that
            // would run the callbacks that the program registered on it inside the caller's own Cancel, where one that throws would
            // surface in the caller. The loop cancels it itself, when the wait for the card ends or the loop does.
            private PendingCard StartDeciding(CardPresentedEvent shown)
            {
                var cancel = new CancellationTokenSource();
                var answer = Task.Run(() => AnswerAsync(shown, cancel.Token), CancellationToken.None);

                _ = answer.ContinueWith(
                    task => _queue.Writer.TryWrite(new AnswerFailed(shown.PresenceId, task.Exception!.GetBaseException())),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return new PendingCard(shown.Uid, cancel, answer);
            }

            // The wait for a card ended: a decision that is still under way is dropped, and nothing is answered for the card. An answer
            // that RFiDGear has not replied to is waited for a moment and then left alone (it is never sent again). The end of a wait for
            // a card that this loop was never shown is nobody's card here, and nothing is reported for it.
            private async Task<CardOutcome?> EndAsync(CardPresentationEndedEvent ended)
            {
                if (!_pending.Remove(ended.PresenceId, out var card))
                {
                    // A notice of an earlier mode on the same connection that was still on its way when this loop began to listen.
                    return null;
                }

                string answered;

                try
                {
                    card.Drop();
                    answered = await card.Answer.WaitAsync(_owner.Grace).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    answered = "pending";
                }
                catch (Exception)
                {
                    // The request failed after the wait for the card had ended: what RFiDGear says about the card is still reported.
                    answered = "failed";
                }

                return new CardOutcome(ended.PresenceId, card.Uid, answered, ended.Reason, ended.RunId);
            }

            private void Report(CardOutcome outcome)
            {
                _outcomes.Add(outcome);
                _onOutcome?.Invoke(outcome);
            }

            // The loop is over, for whatever reason: nothing more is decided, the mode is stopped when it is this loop's, and the
            // cards whose wait RFiDGear has not ended yet get their end (it follows the stop).
            private async Task ShutDownAsync()
            {
                foreach (var card in _pending.Values)
                {
                    card.Drop();
                }

                if (_weStartedTheMode)
                {
                    await StopOwnModeAsync().ConfigureAwait(false);
                }

                await CollectEndsAsync().ConfigureAwait(false);
                await LetGoAsync().ConfigureAwait(false);
            }

            // Stopping the mode switches it off whoever switched it on, and the notice that this loop's own mode ended may be on its way
            // while somebody else's mode is already on. So the mode is asked about first, and stopped only when RFiDGear says that it is a
            // mode of a client (a mode of a user is never that, and RFiDGear serves one client at a time, so it is this loop's own; it may
            // be a mode that runs every card, which is the mode that must never be left on). What is left is the time between the question
            // and the stop: a program that owns its connection closes it instead, which ends its mode and no other. Neither request is
            // waited for longer than the grace, and nothing they do keeps the loop from ending: a mode that is left on ends with the
            // connection.
            private async Task StopOwnModeAsync()
            {
                if (await OwnModeIsOnAsync().ConfigureAwait(false) != true)
                {
                    return;
                }

                try
                {
                    await _owner._service.StopAutoMode(new StopAutoModeRequest()).WaitAsync(_owner.Grace).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The connection is gone (and the mode ends with it), or RFiDGear did not answer in time or answered with a failure: the
                    // loop has to end anyway.
                }
            }

            // Whether the mode that is on is a mode of a client, which is this loop's own because RFiDGear serves one client at a time.
            // Nothing when RFiDGear cannot say: it does not answer in time, answers with a failure, or the connection is gone.
            private async Task<bool?> OwnModeIsOnAsync()
            {
                try
                {
                    var status = (await _owner._service.GetStatus(new GetStatusRequest()).WaitAsync(_owner.Grace).ConfigureAwait(false)).AutoMode;

                    return status is { Enabled: true } && status.StartedBy == RunSources.Bridge;
                }
                catch (Exception)
                {
                    return null;
                }
            }


            // What was queued when the loop ended is looked at first: a card that was shown and not looked at yet is in it, and gets no
            // decision. After the stop RFiDGear ends the wait for the card that was shown last; it is reported when that notice comes, for
            // a moment.
            private async Task CollectEndsAsync()
            {
                using var grace = new CancellationTokenSource(_owner.Grace);

                try
                {
                    while (true)
                    {
                        if (!_queue.Reader.TryRead(out var item))
                        {
                            if (_pending.Count == 0)
                            {
                                break;
                            }

                            item = await NextAsync(_queue.Reader, grace.Token).ConfigureAwait(false);

                            if (item == null)
                            {
                                break;
                            }
                        }

                        await CollectAsync(item).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // The time is up: what RFiDGear did not say is not made up.
                }
                catch (Exception)
                {
                    // An answer that failed while the loop ended: there is nobody left to tell.
                }
            }

            private async Task CollectAsync(object item)
            {
                if (item is CardPresentedEvent shown && !_pending.ContainsKey(shown.PresenceId))
                {
                    _pending[shown.PresenceId] = new PendingCard(shown.Uid, new CancellationTokenSource(), Task.FromResult("none"));
                }
                else if (item is CardPresentationEndedEvent ended && await EndAsync(ended).ConfigureAwait(false) is { } outcome)
                {
                    Report(outcome);
                }
            }

            // What is still not over is dropped: the answers are not waited for longer than the grace, and what they say later is of no use.
            private async Task LetGoAsync()
            {
                var answers = _pending.Values.Select(card => card.Answer).ToArray();

                try
                {
                    await Task.WhenAll(answers).WaitAsync(_owner.Grace).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Dropped: a failure, or the time that is up.
                }

                foreach (var card in _pending.Values)
                {
                    card.Drop();
                }

                _pending.Clear();
            }

            // Decides, and sends the answer. Returns what was sent (none when the card was given up before the program had decided).
            private async Task<string> AnswerAsync(CardPresentedEvent shown, CancellationToken cardToken)
            {
                CardDecision decision;

                try
                {
                    // A decision that ignores its token is abandoned, not waited for: the loop would never end otherwise.
                    var deciding = Task.Run(() => _decide(shown, cardToken), CancellationToken.None);

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

                return cardToken.IsCancellationRequested ? "none" : await SendAsync(shown, decision).ConfigureAwait(false);
            }

            private async Task<string> SendAsync(CardPresentedEvent shown, CardDecision decision)
            {
                try
                {
                    if (!decision.Approve)
                    {
                        await _owner._service.DeclineCard(new DeclineCardRequest { PresenceId = shown.PresenceId }).ConfigureAwait(false);

                        return "declined";
                    }

                    await _owner._service.ApproveCard(new ApproveCardRequest
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
                    await _owner._service.DeclineCard(new DeclineCardRequest { PresenceId = presenceId }).ConfigureAwait(false);
                }
                catch (Exception failure) when (failure is RemoteInvocationException || failure is ConnectionLostException || failure is ObjectDisposedException)
                {
                    // It ended meanwhile, or the connection is gone (which ends the mode): nothing is left to decline.
                }
            }

            // A card whose decision is under way: what was shown, the source of the token of the decision, and what the decision will
            // have answered.
            private sealed class PendingCard
            {
                private readonly CancellationTokenSource _cancel;
                private int _dropped;

                internal PendingCard(string uid, CancellationTokenSource cancel, Task<string> answer)
                {
                    Uid = uid;
                    _cancel = cancel;
                    Answer = answer;
                }

                internal string Uid { get; }

                internal Task<string> Answer { get; }

                // Cancels the token of the decision without waiting for the callbacks that the program registered on it: Cancel runs them
                // on the calling thread, and one that blocks would hold up the events and the stop of the mode, one that throws would end
                // the loop. The source is let go of once they have run. Doing it twice does nothing.
                internal void Drop()
                {
                    if (Interlocked.Exchange(ref _dropped, 1) != 0)
                    {
                        return;
                    }

                    _ = Task.Run(CancelAndDispose);
                }

                private void CancelAndDispose()
                {
                    try
                    {
                        _cancel.Cancel();
                    }
                    catch (Exception)
                    {
                        // The token is cancelled all the same, and what the callbacks threw is of no interest to anybody.
                    }
                    finally
                    {
                        _cancel.Dispose();
                    }
                }
            }

            // What the loop is told when the answer for a card failed (the connection, RFiDGear): it ends the loop with that failure.
            private sealed record AnswerFailed(string PresenceId, Exception Error);
        }
    }
}
