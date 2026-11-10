using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    public sealed class CardWaiterTests
    {
        private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);


        // A waiter on a scripted reader whose clock is moved only by the waits between two looks, so a test needs no
        // real time: one look at a time, one second after the other.
        private sealed class Rig
        {
            public Rig()
            {
                Waiter = new CardWaiter(
                    Host,
                    Second,
                    (span, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        Now += span;
                        Delays++;

                        // A wait that should have ended must fail a test, not loop for ever.
                        if (Delays > 1000)
                        {
                            // A failure that no test expects by accident, so a wait that should have ended fails its test.
                            throw new TimeoutException("The wait did not end.");
                        }

                        OnDelay?.Invoke();

                        return Task.CompletedTask;
                    },
                    () => Now);
            }

            public FakeReaderHost Host { get; } = new FakeReaderHost();

            public CardWaiter Waiter { get; }

            public TimeSpan Now { get; set; }

            public int Delays { get; private set; }

            public Action? OnDelay { get; set; }

            public void Script(params ReaderSnapshot[] answers)
            {
                foreach (var answer in answers)
                {
                    Host.ScriptScan(answer);
                }
            }
        }

        private static ReaderSnapshot CardA() => Snapshots.Card("04AABBCCDD");

        private static ReaderSnapshot CardB() => Snapshots.Card("04112233EE");

        // An answer in a state this code does not know (a later version of the host may add one).
        private static ReaderSnapshot Unknown() =>
            new ReaderSnapshot(Snapshots.Reader, (ReaderScanState)99, Array.Empty<CredentialInfo>());

        [Fact]
        public async Task WaitForCard_StopsAtTheFirstLookThatShowsACard()
        {
            var rig = new Rig();
            rig.Script(Snapshots.NoCard(), Snapshots.NoCard(), CardA(), Snapshots.NoCard());

            var result = await rig.Waiter.WaitForCardAsync(null, CancellationToken.None);

            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal(CardA(), result.Last);
            Assert.Equal(3, rig.Host.ScanCount);
            Assert.Equal(2, rig.Delays);
        }

        [Fact]
        public async Task WaitForCard_AnswersThatSayNothing_AreLookedPast()
        {
            var rig = new Rig();
            rig.Script(Snapshots.NoReader(), Snapshots.Busy(), Snapshots.ReadFailed(), Unknown(), CardA());

            var result = await rig.Waiter.WaitForCardAsync(null, CancellationToken.None);

            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal(5, rig.Host.ScanCount);
        }

        [Fact]
        public async Task AnAnswerThatSaysNothing_IsFollowedByALongerPause_SoAnUnpluggedReaderIsNotAskedTwiceASecond()
        {
            var rig = new Rig();
            rig.Script(Snapshots.Busy(), Snapshots.NoCard(), CardA());

            await rig.Waiter.WaitForCardAsync(null, CancellationToken.None);

            // Four intervals after the busy answer, one after the empty reader.
            Assert.Equal(TimeSpan.FromSeconds(5), rig.Now);
            Assert.Equal(2, rig.Delays);
        }

        [Fact]
        public async Task WaitForEmptyReader_OnlyAReaderThatAnswersReadyAndEmpty_CountsAsRemoval()
        {
            var rig = new Rig();
            rig.Script(CardA(), Snapshots.NoReader(), Snapshots.Busy(), Snapshots.ReadFailed(), Unknown(), Snapshots.NoCard());

            var result = await rig.Waiter.WaitForEmptyReaderAsync(null, CancellationToken.None);

            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal(ReaderScanState.Ready, result.Last!.State);
            Assert.Equal(6, rig.Host.ScanCount);
        }

        [Fact]
        public async Task WaitForEmptyReader_AReaderThatIsAlreadyEmpty_IsDoneAtTheFirstLook()
        {
            var rig = new Rig();
            rig.Script(Snapshots.NoCard());

            var result = await rig.Waiter.WaitForEmptyReaderAsync(null, CancellationToken.None);

            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal(1, rig.Host.ScanCount);
            Assert.Equal(0, rig.Delays);
        }

        [Fact]
        public async Task ATimeout_EndsTheWait_AndGivesTheLastAnswer()
        {
            var rig = new Rig();
            rig.Host.Show(CardA(), raise: false);

            var result = await rig.Waiter.WaitForEmptyReaderAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

            Assert.Equal(CardWaitOutcome.TimedOut, result.Outcome);
            Assert.Equal(CardA(), result.Last);
            Assert.Equal(6, rig.Host.ScanCount);
            Assert.Equal(5, rig.Delays);
        }

        [Fact]
        public async Task AnAnswerAtTheDeadline_StillCounts()
        {
            var rig = new Rig();
            rig.Script(Snapshots.NoCard(), Snapshots.NoCard(), CardA());

            var result = await rig.Waiter.WaitForCardAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal(TimeSpan.FromSeconds(2), rig.Now);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task ATimeoutOfZeroOrLess_LooksOnce(int seconds)
        {
            var rig = new Rig();
            rig.Host.Show(CardA(), raise: false);

            var result = await rig.Waiter.WaitForEmptyReaderAsync(TimeSpan.FromSeconds(seconds), CancellationToken.None);

            Assert.Equal(CardWaitOutcome.TimedOut, result.Outcome);
            Assert.Equal(1, rig.Host.ScanCount);
            Assert.Equal(0, rig.Delays);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ATimeoutThatIsNoTime_OrTooLongForTheClock_WaitsUntilItIsCancelled(bool infinite)
        {
            // Timeout.InfiniteTimeSpan is -1 millisecond; the longest time overflows the clock when added to it.
            var timeout = infinite ? Timeout.InfiniteTimeSpan : TimeSpan.MaxValue;
            var rig = new Rig { Now = TimeSpan.FromSeconds(10) };
            rig.Host.Show(CardA(), raise: false);
            using var cts = new CancellationTokenSource();
            rig.OnDelay = () =>
            {
                if (rig.Delays == 5)
                {
                    cts.Cancel();
                }
            };

            var result = await rig.Waiter.WaitForEmptyReaderAsync(timeout, cts.Token);

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Equal(5, rig.Host.ScanCount);
        }

        [Fact]
        public async Task WithoutATimeout_TheWaiterLooksUntilItIsCancelled()
        {
            var rig = new Rig();
            rig.Host.Show(CardA(), raise: false);
            using var cts = new CancellationTokenSource();
            rig.OnDelay = () =>
            {
                if (rig.Delays == 20)
                {
                    cts.Cancel();
                }
            };

            var result = await rig.Waiter.WaitForEmptyReaderAsync(null, cts.Token);

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Equal(20, rig.Host.ScanCount);
        }

        [Fact]
        public async Task ACancelBeforeTheStart_LooksNever()
        {
            var rig = new Rig();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await rig.Waiter.WaitForCardAsync(null, cts.Token);

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Null(result.Last);
            Assert.Equal(0, rig.Host.ScanCount);
        }

        [Fact]
        public async Task ACancelWhileLooking_EndsTheWaitOnlyAfterTheLookHasEnded_AndItsAnswerDoesNotCount()
        {
            var rig = new Rig();
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Host.ScriptScan(CardA(), gate);
            using var cts = new CancellationTokenSource();

            var wait = rig.Waiter.WaitForCardAsync(null, cts.Token);
            Assert.Equal(1, rig.Host.ScanCount);
            cts.Cancel();
            for (var i = 0; i < 20; i++)
            {
                await Task.Yield();
            }

            // The host finishes a read it has begun; until it has, the waiter must not report that it stopped.
            Assert.False(wait.IsCompleted);

            gate.SetResult(true);
            var result = await wait.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Null(result.Last);
        }

        [Fact]
        public async Task ACancelWhileWaitingBetweenTwoLooks_EndsTheWait()
        {
            var host = new FakeReaderHost();
            host.ScriptScan(Snapshots.NoCard());
            var waiter = new CardWaiter(host, Second, (_, ct) => Task.Delay(Timeout.Infinite, ct));
            using var cts = new CancellationTokenSource();

            var wait = waiter.WaitForCardAsync(null, cts.Token);
            Assert.False(wait.IsCompleted);
            cts.Cancel();
            var result = await wait.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Equal(Snapshots.NoCard(), result.Last);
            Assert.Equal(1, host.ScanCount);
        }

        [Fact]
        public async Task ACancelThatTheHostReportsAsACancellation_IsACancelledWait_NotAnException()
        {
            var rig = new Rig();
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            rig.Host.ScriptScanFailure(new OperationCanceledException(cts.Token), gate);

            var wait = rig.Waiter.WaitForCardAsync(null, cts.Token);
            cts.Cancel();
            gate.SetResult(true);
            var result = await wait.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Null(result.Last);
        }

        [Fact]
        public async Task ACancellationOfTheHostThatIsNotOurs_IsNotTakenForACancel()
        {
            var rig = new Rig();
            rig.Host.ScriptScanFailure(new OperationCanceledException("The host gave up."));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Waiter.WaitForCardAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task AnExceptionOfTheHost_IsNotSwallowed()
        {
            var rig = new Rig();
            rig.Host.ScriptScanFailure(new InvalidOperationException("The reader provider failed."));

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Waiter.WaitForCardAsync(null, CancellationToken.None));

            Assert.Equal("The reader provider failed.", failure.Message);
            Assert.Equal(1, rig.Host.ScanCount);
        }

        [Fact]
        public async Task RemovalAndReinsertion_OfTheSameCard_NamesBothStagesAndBothUids()
        {
            var rig = new Rig();
            var stages = new List<CardMoveStage>();
            rig.Script(CardA(), CardA(), Snapshots.NoCard(), Snapshots.NoCard(), CardA());

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(null, null, stages.Add, CancellationToken.None);

            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal(CardMoveStage.WaitingForReinsertion, result.Stage);
            Assert.Equal("04AABBCCDD", result.UidBefore);
            Assert.Equal("04AABBCCDD", result.UidAfter);
            Assert.True(result.SameCard);
            Assert.Equal(new[] { CardMoveStage.WaitingForRemoval, CardMoveStage.WaitingForReinsertion }, stages);
            Assert.Equal(5, rig.Host.ScanCount);
        }

        [Fact]
        public async Task RemovalAndReinsertion_OfAnotherCard_IsNotTheSameCard()
        {
            var rig = new Rig();
            rig.Script(CardA(), Snapshots.NoCard(), CardB());

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(null, null, null, CancellationToken.None);

            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal("04AABBCCDD", result.UidBefore);
            Assert.Equal("04112233EE", result.UidAfter);
            Assert.False(result.SameCard);
        }

        [Fact]
        public async Task RemovalAndReinsertion_AReaderThatIsAlreadyEmpty_StartsWithTheReinsertion()
        {
            var rig = new Rig();
            var stages = new List<CardMoveStage>();
            rig.Script(Snapshots.NoCard(), Snapshots.NoCard(), CardB());

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(null, "04AABBCCDD", stages.Add, CancellationToken.None);

            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal(new[] { CardMoveStage.WaitingForReinsertion }, stages);
            Assert.Equal("04AABBCCDD", result.UidBefore);
            Assert.False(result.SameCard);
        }

        [Fact]
        public async Task RemovalAndReinsertion_WithoutAKnownCardBefore_CannotSayWhetherItIsTheSame()
        {
            var rig = new Rig();
            rig.Script(Snapshots.NoCard(), CardA());

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(null, null, null, CancellationToken.None);

            Assert.Null(result.UidBefore);
            Assert.Equal("04AABBCCDD", result.UidAfter);
            Assert.Null(result.SameCard);
        }

        [Fact]
        public async Task RemovalAndReinsertion_AnswersThatSayNothing_AreNotARemoval()
        {
            var rig = new Rig();
            var stages = new List<CardMoveStage>();
            rig.Script(CardA(), Snapshots.Busy(), Snapshots.ReadFailed(), Snapshots.NoReader(), CardA(), Snapshots.NoCard(), CardB());

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(null, null, stages.Add, CancellationToken.None);

            // Nothing before the empty reader is a removal: a wait that took one of them for it would end at the card
            // of the fifth look, which is the old one.
            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal(7, rig.Host.ScanCount);
            Assert.Equal("04112233EE", result.UidAfter);
            Assert.Equal(new[] { CardMoveStage.WaitingForRemoval, CardMoveStage.WaitingForReinsertion }, stages);
        }

        [Fact]
        public async Task RemovalAndReinsertion_ACardThatStays_TimesOutWhileWaitingForTheRemoval()
        {
            var rig = new Rig();
            rig.Host.Show(CardA(), raise: false);

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(TimeSpan.FromSeconds(3), null, null, CancellationToken.None);

            Assert.Equal(CardWaitOutcome.TimedOut, result.Outcome);
            Assert.Equal(CardMoveStage.WaitingForRemoval, result.Stage);
            Assert.Equal("04AABBCCDD", result.UidBefore);
            Assert.Null(result.UidAfter);
        }

        [Fact]
        public async Task RemovalAndReinsertion_TheTimeIsForBothStagesTogether()
        {
            var rig = new Rig();
            rig.Script(CardA(), Snapshots.NoCard());

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(TimeSpan.FromSeconds(4), null, null, CancellationToken.None);

            // One second went on the removal, so the reinsertion gets three, and the whole wait ends after four.
            Assert.Equal(CardWaitOutcome.TimedOut, result.Outcome);
            Assert.Equal(CardMoveStage.WaitingForReinsertion, result.Stage);
            Assert.Equal(TimeSpan.FromSeconds(4), rig.Now);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1000, 1)]
        [InlineData(999, 1)]
        public async Task RemovalAndReinsertion_WhenTheRemovalHasUsedUpTheTime_NoFurtherLookIsMade(int milliseconds, int pauses)
        {
            // After one pause of a second the time is used up exactly (1000), or by 1 ms (999: the time left is -1 ms,
            // which is not the infinite timeout). The reader showed the empty reader at that look.
            var rig = new Rig();
            rig.Script(pauses == 0 ? Snapshots.NoCard() : CardA(), Snapshots.NoCard());

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(TimeSpan.FromMilliseconds(milliseconds), null, null, CancellationToken.None);

            Assert.Equal(CardWaitOutcome.TimedOut, result.Outcome);
            Assert.Equal(CardMoveStage.WaitingForReinsertion, result.Stage);
            Assert.Null(result.UidAfter);
            Assert.Equal(Snapshots.NoCard(), result.Last);
            Assert.Equal(pauses + 1, rig.Host.ScanCount);
        }

        [Fact]
        public async Task RemovalAndReinsertion_ACancelWithTheTimeUsedUp_WinsOverTheTimeout()
        {
            var rig = new Rig();
            rig.Script(Snapshots.NoCard());
            using var cts = new CancellationTokenSource();

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(
                TimeSpan.Zero,
                null,
                stage =>
                {
                    if (stage == CardMoveStage.WaitingForReinsertion)
                    {
                        cts.Cancel();
                    }
                },
                cts.Token);

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Equal(CardMoveStage.WaitingForReinsertion, result.Stage);
            Assert.Equal(1, rig.Host.ScanCount);
        }

        [Fact]
        public async Task RemovalAndReinsertion_ACancelWhileWaitingForTheRemoval_NamesTheStage()
        {
            var rig = new Rig();
            rig.Host.Show(CardA(), raise: false);
            using var cts = new CancellationTokenSource();
            rig.OnDelay = cts.Cancel;

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(null, null, null, cts.Token);

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Equal(CardMoveStage.WaitingForRemoval, result.Stage);
            Assert.Equal("04AABBCCDD", result.UidBefore);
        }

        [Fact]
        public async Task RemovalAndReinsertion_ACancelWhileWaitingForTheCard_NamesTheStage_AndKeepsTheLastAnswer()
        {
            var rig = new Rig();
            rig.Script(CardA(), Snapshots.NoCard());
            using var cts = new CancellationTokenSource();
            rig.OnDelay = () =>
            {
                if (rig.Delays == 3)
                {
                    cts.Cancel();
                }
            };

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(null, null, null, cts.Token);

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Equal(CardMoveStage.WaitingForReinsertion, result.Stage);
            Assert.Equal(ReaderScanState.Ready, result.Last!.State);
            Assert.False(result.Last.HasCard);
        }

        [Fact]
        public async Task RemovalAndReinsertion_ACancelBetweenTheStages_KeepsTheAnswerThatShowedTheEmptyReader()
        {
            var rig = new Rig();
            rig.Script(CardA(), Snapshots.NoCard());
            using var cts = new CancellationTokenSource();

            var result = await rig.Waiter.WaitForRemovalAndReinsertionAsync(
                null,
                null,
                stage =>
                {
                    if (stage == CardMoveStage.WaitingForReinsertion)
                    {
                        cts.Cancel();
                    }
                },
                cts.Token);

            Assert.Equal(CardWaitOutcome.Cancelled, result.Outcome);
            Assert.Equal(CardMoveStage.WaitingForReinsertion, result.Stage);
            Assert.Equal(Snapshots.NoCard(), result.Last);
            Assert.Equal(2, rig.Host.ScanCount);
        }

        [Fact]
        public void SameCard_IgnoresTheCaseOfTheUids()
        {
            Assert.True(new CardMoveResult(CardWaitOutcome.Done, CardMoveStage.WaitingForReinsertion, "04aabbccdd", "04AABBCCDD", null).SameCard);
        }

        [Fact]
        public void PrimaryUid_IsThatOfThePrimaryCredential_OrOfTheFirstWhenNoneIsMarked_OrNothing()
        {
            var lf = new CredentialInfo("LF0102", 1, "EM4100", false);
            var hf = new CredentialInfo("04AABBCCDD", 2, "DESFireEV1", true);

            Assert.Equal("04AABBCCDD", CardWaiter.PrimaryUid(new ReaderSnapshot("r", ReaderScanState.Ready, new[] { lf, hf })));
            Assert.Equal("LF0102", CardWaiter.PrimaryUid(new ReaderSnapshot("r", ReaderScanState.Ready, new[] { lf })));
            Assert.Null(CardWaiter.PrimaryUid(Snapshots.NoCard()));
            Assert.Throws<ArgumentNullException>(() => CardWaiter.PrimaryUid(null!));
        }

        [Fact]
        public async Task WithoutTestDoubles_TheWaiterPacesItselfWithRealTime()
        {
            var host = new FakeReaderHost();
            host.ScriptScan(Snapshots.NoCard());
            host.ScriptScan(CardA());
            var waiter = new CardWaiter(host, TimeSpan.FromMilliseconds(1));

            var result = await waiter.WaitForCardAsync(TimeSpan.FromSeconds(30), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(CardWaitOutcome.Done, result.Outcome);
            Assert.Equal(2, host.ScanCount);
        }

        [Fact]
        public async Task WithoutTestDoubles_ATimeoutIsMeasuredOnTheRealClock()
        {
            var host = new FakeReaderHost();
            host.Show(CardA(), raise: false);
            var waiter = new CardWaiter(host, TimeSpan.FromMilliseconds(1));

            var result = await waiter.WaitForEmptyReaderAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(CardWaitOutcome.TimedOut, result.Outcome);
            Assert.InRange(host.ScanCount, 2, 5000);
        }

        [Fact]
        public void TheDefaultInterval_IsHalfASecond()
        {
            Assert.Equal(TimeSpan.FromMilliseconds(500), CardWaiter.DefaultInterval);
        }

        [Fact]
        public async Task TheWaiterContinuesWhereTheCallerAwaits_SoACallbackNeedsNoMarshalling()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeReaderHost();
                host.ScriptScan(CardA());
                host.ScriptScan(Snapshots.NoCard());
                host.ScriptScan(CardA());
                var waiter = new CardWaiter(host, TimeSpan.FromMilliseconds(1));
                var caller = Environment.CurrentManagedThreadId;
                var seenOn = new List<int>();

                // The fake host raises ReaderStateChanged on the thread that calls ScanAsync, which shows where the
                // waiter's loop runs after each pause; the callback shows where the stages are announced.
                host.ReaderStateChanged += (_, _) => seenOn.Add(Environment.CurrentManagedThreadId);

                var result = await waiter.WaitForRemovalAndReinsertionAsync(
                    TimeSpan.FromSeconds(30),
                    null,
                    _ => seenOn.Add(Environment.CurrentManagedThreadId),
                    CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

                Assert.Equal(CardWaitOutcome.Done, result.Outcome);
                Assert.Equal(5, seenOn.Count);
                Assert.All(seenOn, id => Assert.Equal(caller, id));
            });
        }

        [Fact]
        public void Arguments_AreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => new CardWaiter(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CardWaiter(new FakeReaderHost(), TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CardWaiter(new FakeReaderHost(), TimeSpan.FromSeconds(-1)));
        }
    }
}
