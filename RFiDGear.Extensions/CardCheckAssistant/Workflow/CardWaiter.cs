using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;

namespace RFiDGear.Extensions.CardCheckAssistant.Workflow
{
    /// <summary>How a wait for the reader ended.</summary>
    internal enum CardWaitOutcome
    {
        /// <summary>The reader showed what was waited for.</summary>
        Done,

        /// <summary>The time was up before it did.</summary>
        TimedOut,

        /// <summary>The caller cancelled the wait.</summary>
        Cancelled
    }

    /// <summary>Where a card is on its way off the reader and back on.</summary>
    internal enum CardMoveStage
    {
        /// <summary>The card is still on the reader, or the reader says nothing yet.</summary>
        WaitingForRemoval,

        /// <summary>The reader is empty; a card is awaited.</summary>
        WaitingForReinsertion
    }

    /// <summary>What a wait for one thing on the reader came to.</summary>
    /// <param name="Outcome">How the wait ended.</param>
    /// <param name="Last">The last answer of the reader (in any state), or <c>null</c> if it was never asked.</param>
    internal sealed record CardWaitResult(CardWaitOutcome Outcome, ReaderSnapshot? Last);

    /// <summary>What a card's way off the reader and back on came to.</summary>
    /// <param name="Outcome">How the wait ended.</param>
    /// <param name="Stage">The stage the wait was in when it ended; <see cref="CardMoveStage.WaitingForReinsertion"/> when it is <see cref="CardWaitOutcome.Done"/>.</param>
    /// <param name="UidBefore">The UID of the card last seen on the reader before it was taken off, or <c>null</c> if none was seen.</param>
    /// <param name="UidAfter">The UID of the card put on the reader again, or <c>null</c> if none was.</param>
    /// <param name="Last">The last answer of the reader (in any state), or <c>null</c> if it was never asked.</param>
    internal sealed record CardMoveResult(CardWaitOutcome Outcome, CardMoveStage Stage, string? UidBefore, string? UidAfter, ReaderSnapshot? Last)
    {
        /// <summary>
        /// Whether the card put back is the one taken off: <c>null</c> when one of the two UIDs is not known.
        /// </summary>
        public bool? SameCard => UidBefore != null && UidAfter != null
            ? string.Equals(UidBefore, UidAfter, StringComparison.OrdinalIgnoreCase)
            : null;
    }

    /// <summary>
    /// Waits for things to happen on the reader: a card to arrive, to be taken off, or taken off and put back. RFiDGear
    /// does not poll the reader for an extension, so the waiter looks itself, with <see cref="IReaderHost.ScanAsync"/>,
    /// once per interval.
    /// <para>
    /// Only an answer in the state <see cref="ReaderScanState.Ready"/> says what is on the reader. <c>Busy</c>,
    /// <c>ReadFailed</c>, <c>NoReader</c> and a state this code does not know say nothing: the waiter looks again, and
    /// above all they are never taken for "the card was taken off". (A PC/SC reader cannot tell a card that is not
    /// there from one it cannot read, nor from a reader or card that another program holds, nor from a reader that
    /// was unplugged; all of them look like an empty reader, so one such answer is taken for a removal and the card
    /// seen next, even the same one, for a reinsertion.) A card that is taken off and put back between two looks is
    /// not seen, and neither is a different card put on within one look: the wait keeps waiting for an empty reader.
    /// </para>
    /// <para>
    /// A look activates the card again, which ends a session that was authenticated, and RFiDGear does not notice
    /// other users of the reader (a setup dialog, a quick check, another program). Wait only while nothing else works
    /// with the card. After an answer that says nothing the pause is four times as long, so a reader that stays
    /// unplugged does not fill the log with a failed read every half second.
    /// </para>
    /// <para>
    /// To stop a wait, cancel its token and await the task. The task ends only after a look that has started has
    /// ended (the host finishes a read it has begun), so when it ends no scan of the waiter is running. That matters
    /// before a run: the host refuses a run while a scan is going. A cancelled wait is a result, not an exception; the
    /// answer of a look that was running when the cancel came is neither used nor returned. Do not block a thread on
    /// the task (on the UI thread it would wait for itself): cancel it and await it. A callback can still arrive once
    /// after a cancel that came from another thread.
    /// </para>
    /// <para>
    /// The waiter does not switch context (it never uses <c>ConfigureAwait(false)</c>): it continues where the
    /// caller's await would, and the callbacks run there. A callback that throws ends the wait with its exception. An
    /// exception of the host (anything but a cancellation by the caller's token) is not caught: the host answers
    /// failures as states, so one is a defect of the host.
    /// </para>
    /// <para>
    /// A timeout is checked after each look, so a wait can end up to one interval and one look after the time asked.
    /// A timeout of zero or less looks once. A timeout that is no time at all (<see cref="Timeout.InfiniteTimeSpan"/>)
    /// or too long to add to the clock (<see cref="TimeSpan.MaxValue"/>) waits until cancelled, like <c>null</c>.
    /// </para>
    /// </summary>
    internal sealed class CardWaiter
    {
        /// <summary>
        /// The time between two looks, on top of the time a look takes (up to a few seconds with some readers). Short
        /// enough to see a card that is taken off and put back by hand.
        /// </summary>
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(500);

        // How many intervals to wait after an answer that says nothing (busy, failed, no reader).
        private const int QuietPauseFactor = 4;

        private readonly IReaderHost _reader;
        private readonly TimeSpan _interval;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly Func<TimeSpan> _clock;

        /// <summary>Creates a waiter for a reader.</summary>
        /// <param name="reader">The host's reader.</param>
        /// <param name="interval">The time between two looks; <see cref="DefaultInterval"/> if <c>null</c>.</param>
        /// <param name="delay">Waits for a time; <see cref="Task.Delay(TimeSpan, CancellationToken)"/> if <c>null</c>. A test replaces it.</param>
        /// <param name="clock">Tells a time that only grows; the system's monotonic clock if <c>null</c>. A test replaces it.</param>
        /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is zero or negative.</exception>
        public CardWaiter(
            IReaderHost reader,
            TimeSpan? interval = null,
            Func<TimeSpan, CancellationToken, Task>? delay = null,
            Func<TimeSpan>? clock = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _interval = interval ?? DefaultInterval;

            if (_interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), interval, "The interval must be longer than zero.");
            }

            _delay = delay ?? Task.Delay;
            _clock = clock ?? (() => Stopwatch.GetElapsedTime(0));
        }

        /// <summary>
        /// The UID of the card an answer shows: that of the primary credential, or of the first one if none is marked.
        /// </summary>
        /// <param name="snapshot">An answer of the reader.</param>
        /// <returns>The UID, or <c>null</c> if the answer shows no card.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
        public static string? PrimaryUid(ReaderSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return (snapshot.Credentials.FirstOrDefault(credential => credential.IsPrimary) ?? snapshot.Credentials.FirstOrDefault())?.Uid;
        }

        /// <summary>Waits until the reader shows a card.</summary>
        /// <param name="timeout">How long to wait; <c>null</c> to wait until cancelled. A time of zero looks once.</param>
        /// <param name="ct">Cancels the wait.</param>
        /// <returns>How the wait ended; with <see cref="CardWaitOutcome.Done"/> its last answer is the card.</returns>
        public Task<CardWaitResult> WaitForCardAsync(TimeSpan? timeout, CancellationToken ct) =>
            LookUntilAsync(HasCardOn, timeout, null, ct);

        /// <summary>
        /// Waits until the reader answers that it is empty. A reader that is empty at the first look counts: the card
        /// is gone.
        /// </summary>
        /// <param name="timeout">How long to wait; <c>null</c> to wait until cancelled. A time of zero looks once.</param>
        /// <param name="ct">Cancels the wait.</param>
        /// <returns>How the wait ended.</returns>
        public Task<CardWaitResult> WaitForEmptyReaderAsync(TimeSpan? timeout, CancellationToken ct) =>
            LookUntilAsync(IsEmpty, timeout, null, ct);

        /// <summary>
        /// Waits for a card to be taken off the reader and a card to be put on it. A reader that is already empty at
        /// the first look counts as taken off.
        /// </summary>
        /// <param name="timeout">
        /// How long to wait for both together; <c>null</c> to wait until cancelled. A time of zero looks once. When the
        /// removal has used up the time, the wait ends at once as <see cref="CardWaitOutcome.TimedOut"/>, in the stage
        /// <see cref="CardMoveStage.WaitingForReinsertion"/>, without another look.
        /// </param>
        /// <param name="cardBeforeUid">
        /// The UID of the card that was on the reader before, if the caller knows it; used when the waiter never sees a
        /// card, because the reader was already empty.
        /// </param>
        /// <param name="stageChanged">
        /// Called when the stage is known and every time it changes: with <see cref="CardMoveStage.WaitingForRemoval"/>
        /// after the first look if the reader is not empty, and with <see cref="CardMoveStage.WaitingForReinsertion"/> as
        /// soon as it is. Never called when the wait is cancelled before the first look.
        /// </param>
        /// <param name="ct">Cancels the wait.</param>
        /// <returns>How the wait ended, with the UIDs before and after.</returns>
        public async Task<CardMoveResult> WaitForRemovalAndReinsertionAsync(
            TimeSpan? timeout,
            string? cardBeforeUid,
            Action<CardMoveStage>? stageChanged,
            CancellationToken ct)
        {
            var started = _clock();
            var before = cardBeforeUid;
            var announced = false;

            var removal = await LookUntilAsync(
                IsEmpty,
                timeout,
                answer =>
                {
                    if (answer.State == ReaderScanState.Ready && answer.HasCard)
                    {
                        before = PrimaryUid(answer);
                    }

                    if (!announced && !IsEmpty(answer))
                    {
                        stageChanged?.Invoke(CardMoveStage.WaitingForRemoval);
                    }

                    announced = true;
                },
                ct);

            if (removal.Outcome != CardWaitOutcome.Done)
            {
                return new CardMoveResult(removal.Outcome, CardMoveStage.WaitingForRemoval, before, null, removal.Last);
            }

            stageChanged?.Invoke(CardMoveStage.WaitingForReinsertion);

            TimeSpan? remaining = null;

            if (!IsUnlimited(timeout))
            {
                remaining = timeout!.Value - (_clock() - started);

                // The time is spent: no further look (and a remaining time of exactly -1 millisecond is not the infinite
                // timeout). A cancel still wins.
                if (remaining <= TimeSpan.Zero)
                {
                    return new CardMoveResult(
                        ct.IsCancellationRequested ? CardWaitOutcome.Cancelled : CardWaitOutcome.TimedOut,
                        CardMoveStage.WaitingForReinsertion,
                        before,
                        null,
                        removal.Last);
                }
            }

            var reinsertion = await LookUntilAsync(HasCardOn, remaining, null, ct);

            return new CardMoveResult(
                reinsertion.Outcome,
                CardMoveStage.WaitingForReinsertion,
                before,
                reinsertion.Outcome == CardWaitOutcome.Done ? PrimaryUid(reinsertion.Last!) : null,
                reinsertion.Last ?? removal.Last);
        }

        // No timeout at all: none given, or the infinite one (which is -1 millisecond, so it must not be taken for a time).
        private static bool IsUnlimited(TimeSpan? timeout) => !timeout.HasValue || timeout.Value == Timeout.InfiniteTimeSpan;

        // The time on the clock at which a wait with this timeout ends, or null when it has no end: no timeout, an
        // infinite one, or one too long to add to the clock. A timeout of zero or less ends at once, after one look.
        private TimeSpan? DeadlineFor(TimeSpan? timeout)
        {
            if (IsUnlimited(timeout))
            {
                return null;
            }

            var now = _clock();

            return timeout!.Value >= TimeSpan.MaxValue - now ? (TimeSpan?)null : now + timeout.Value;
        }

        private static bool IsEmpty(ReaderSnapshot answer) => answer.State == ReaderScanState.Ready && !answer.HasCard;

        // Only an answer in the state Ready can list a card (the snapshot refuses a list in any other state).
        private static bool HasCardOn(ReaderSnapshot answer) => answer.HasCard;

        // Looks at the reader, waits an interval, looks again, until an answer satisfies isDone, the time is up or the
        // token is cancelled. It never returns while a look is running.
        private async Task<CardWaitResult> LookUntilAsync(
            Func<ReaderSnapshot, bool> isDone,
            TimeSpan? timeout,
            Action<ReaderSnapshot>? answered,
            CancellationToken ct)
        {
            var deadline = DeadlineFor(timeout);
            ReaderSnapshot? last = null;

            while (!ct.IsCancellationRequested)
            {
                ReaderSnapshot answer;

                try
                {
                    answer = await _reader.ScanAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }

                // A look takes the reader's time and the cancel may come while it runs; then its answer must not count.
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                last = answer;
                answered?.Invoke(answer);

                if (isDone(answer))
                {
                    return new CardWaitResult(CardWaitOutcome.Done, answer);
                }

                if (deadline.HasValue && _clock() >= deadline.Value)
                {
                    return new CardWaitResult(CardWaitOutcome.TimedOut, answer);
                }

                try
                {
                    await _delay(answer.State == ReaderScanState.Ready ? _interval : _interval * QuietPauseFactor, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }

            return new CardWaitResult(CardWaitOutcome.Cancelled, last);
        }
    }
}
