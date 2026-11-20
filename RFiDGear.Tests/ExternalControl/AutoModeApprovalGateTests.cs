#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Services.AutoMode;
using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The one client that decides about the cards of a mode: it is shown a card, and a card runs only when the connection that started
    /// the mode approves that presence once, in time. Every other end of the wait runs nothing, and says so.
    /// </summary>
    public sealed class AutoModeApprovalGateTests
    {
        private const string Session = "s-1";
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

        private sealed class Rig
        {
            private long _seq;

            public readonly List<(string Name, BridgeEvent Event)> Published = new List<(string, BridgeEvent)>();
            public readonly AutoModeApprovalGate Gate;

            // The clock of the gate: a test moves it past the limit without waiting for it.
            public DateTimeOffset Clock = Now;

            public Rig(TimeSpan? timeout = null, Func<string?>? readRevision = null)
            {
                Gate = new AutoModeApprovalGate(
                    Session,
                    timeout ?? TimeSpan.FromSeconds(30),
                    readRevision ?? (() => "rev-1"),
                    (name, _, create) =>
                    {
                        lock (Published)
                        {
                            Published.Add((name, create(++_seq, Session, Now)));
                        }
                    },
                    () => Clock);
            }

            public IReadOnlyList<T> Events<T>() where T : BridgeEvent
            {
                lock (Published)
                {
                    return Published.Select(item => item.Event).OfType<T>().ToList();
                }
            }

            public async Task WaitForAsync(Func<bool> condition)
            {
                var until = DateTime.UtcNow.AddSeconds(5);

                while (!condition())
                {
                    Assert.True(DateTime.UtcNow < until, "The condition did not become true in time.");
                    await Task.Delay(5);
                }
            }
        }

        private static ApproveCardRequest Approval(string presenceId, string? revision = null, params (string Name, string Value)[] variables) =>
            new ApproveCardRequest
            {
                PresenceId = presenceId,
                ExpectProjectRevision = revision,
                Variables = variables.Select(v => new VariablePair { Name = v.Name, Value = v.Value }).ToList(),
            };

        private static void AssertNotPending(Action answer)
        {
            var fault = Assert.Throws<BridgeFault>(answer);

            Assert.Equal(BridgeErrorKinds.ApprovalNotPending, fault.Kind);
            Assert.Equal(BridgeErrorCodes.ApprovalNotPending, fault.Code);
            Assert.True(fault.NeverStarted);
        }

        [Fact]
        public async Task ACard_IsShownWithItsPresenceUidAndRevision_AndRunsOnlyWhenTheStartingConnectionApproves()
        {
            var rig = new Rig(TimeSpan.FromSeconds(15));
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Events<CardPresentedEvent>().Count == 1);

            var shown = rig.Events<CardPresentedEvent>().Single();
            var pendingWhileWaiting = rig.Gate.PendingPresenceId;
            var response = rig.Gate.Approve(Session, Approval("p-1", "rev-1", ("$EXPECTEDUID", "04AA"), ("$JOB", "J-4")));
            var approval = await waiting;

            Assert.Equal(("p-1", "04AA", "rev-1", Now.AddSeconds(15)), (shown.PresenceId, shown.Uid, shown.ProjectRevision, shown.ExpiresAt));
            Assert.Equal("p-1", pendingWhileWaiting);
            Assert.Null(rig.Gate.PendingPresenceId);
            Assert.Equal("p-1", response.PresenceId);
            Assert.NotNull(approval);
            Assert.Equal("p-1", approval!.PresenceId);

            // The approval is for the card that was shown, and says which: the run is held to it.
            Assert.Equal("04AA", approval.Uid);
            Assert.Equal("rev-1", approval.ExpectProjectRevision);
            Assert.Equal("04AA", approval.Variables["$EXPECTEDUID"]);

            // The names of the variables are compared without regard to case, as for startRun.
            Assert.Equal("J-4", approval.Variables["$job"]);
            Assert.Empty(rig.Events<CardPresentationEndedEvent>());
        }

        [Fact]
        public async Task AnApproval_IsUsedUpByTheFirstAnswer_ASecondOneForTheSamePresenceIsNotPending()
        {
            var rig = new Rig();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            rig.Gate.Approve(Session, Approval("p-1"));
            await waiting;

            AssertNotPending(() => rig.Gate.Approve(Session, Approval("p-1")));
            AssertNotPending(() => rig.Gate.Decline(Session, new DeclineCardRequest { PresenceId = "p-1" }));
        }

        [Fact]
        public async Task AnAnswerForAnotherPresence_IsNotPending_AndTheCardKeepsWaiting()
        {
            var rig = new Rig();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            AssertNotPending(() => rig.Gate.Approve(Session, Approval("p-0")));
            AssertNotPending(() => rig.Gate.Approve(Session, Approval(string.Empty)));
            var stillWaiting = !waiting.IsCompleted;
            rig.Gate.Approve(Session, Approval("p-1"));

            Assert.True(stillWaiting);
            Assert.NotNull(await waiting);
        }

        [Fact]
        public async Task AnAnswerFromAnotherConnection_IsNotPending_AndTheCardKeepsWaiting()
        {
            var rig = new Rig();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            AssertNotPending(() => rig.Gate.Approve("s-2", Approval("p-1")));
            AssertNotPending(() => rig.Gate.Decline("s-2", new DeclineCardRequest { PresenceId = "p-1" }));
            var stillWaiting = !waiting.IsCompleted;
            rig.Gate.Decline(Session, new DeclineCardRequest { PresenceId = "p-1" });

            Assert.True(stillWaiting);
            Assert.Null(await waiting);
        }

        [Fact]
        public void WithoutAnyCardWaitedFor_AnAnswerIsNotPending()
        {
            var rig = new Rig();

            AssertNotPending(() => rig.Gate.Approve(Session, Approval("p-1")));
            AssertNotPending(() => rig.Gate.Decline(Session, new DeclineCardRequest { PresenceId = "p-1" }));
        }

        [Fact]
        public async Task ADeclinedCard_RunsNothing_AndSaysSo()
        {
            var rig = new Rig();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            var response = rig.Gate.Decline(Session, new DeclineCardRequest { PresenceId = "p-1" });

            Assert.Null(await waiting);
            Assert.Equal("p-1", response.PresenceId);
            var ended = Assert.Single(rig.Events<CardPresentationEndedEvent>());
            Assert.Equal(("p-1", PresentationEndReasons.Declined, null), (ended.PresenceId, ended.Reason, ended.RunId));
        }

        [Fact]
        public async Task ACardNobodyAnswersFor_RunsNothingWhenTheTimeIsUp_AndALateAnswerIsNotPending()
        {
            var rig = new Rig(TimeSpan.FromMilliseconds(150));

            var approval = await rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);

            Assert.Null(approval);
            Assert.Equal(PresentationEndReasons.Expired, Assert.Single(rig.Events<CardPresentationEndedEvent>()).Reason);
            AssertNotPending(() => rig.Gate.Approve(Session, Approval("p-1")));
        }

        [Theory]
        [InlineData(PresentationEndReasons.Stopped)]
        [InlineData(PresentationEndReasons.ClientLost)]
        public async Task AWaitThatIsCancelled_RunsNothing_AndSaysWhy(string reason)
        {
            var rig = new Rig();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            rig.Gate.Cancel(reason);

            Assert.Null(await waiting);
            Assert.Equal(reason, Assert.Single(rig.Events<CardPresentationEndedEvent>()).Reason);
            AssertNotPending(() => rig.Gate.Approve(Session, Approval("p-1")));
        }

        [Fact]
        public void CancellingWhenNothingIsWaitedFor_DoesNothing()
        {
            var rig = new Rig();

            rig.Gate.Cancel(PresentationEndReasons.Stopped);

            Assert.Empty(rig.Published);
        }

        [Fact]
        public async Task AnAnswerThatBreaksTheRules_IsRefusedAndTheCardKeepsWaiting_ForAnotherAnswerInTime()
        {
            var rig = new Rig();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            // The rules of the variables of startRun, and the alphabet of a revision.
            var badName = Assert.Throws<BridgeFault>(() => rig.Gate.Approve(Session, Approval("p-1", null, ("$A\tB", "x"))));
            var duplicates = Assert.Throws<BridgeFault>(() => rig.Gate.Approve(Session, Approval("p-1", null, ("$A", "1"), ("$a", "2"))));
            var badRevision = Assert.Throws<BridgeFault>(() => rig.Gate.Approve(Session, Approval("p-1", "has space!")));
            var tooMany = Assert.Throws<BridgeFault>(() => rig.Gate.Approve(
                Session,
                Approval("p-1", null, Enumerable.Range(0, BridgeProtocol.MaxVariables + 1).Select(i => ("$V" + i, "x")).ToArray())));
            var stillWaiting = !waiting.IsCompleted;
            rig.Gate.Approve(Session, Approval("p-1", null, ("$A", "1")));

            Assert.All(new[] { badName, duplicates, badRevision, tooMany }, fault => Assert.Equal(BridgeErrorKinds.InvalidRequest, fault.Kind));
            Assert.Equal("expectProjectRevision", badRevision.Details!["field"]);
            Assert.True(stillWaiting);
            Assert.Equal("1", (await waiting)!.Variables["$A"]);
        }

        [Fact]
        public async Task AnApprovalWithoutVariablesOrRevision_IsAnApprovalAllTheSame()
        {
            var rig = new Rig();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            rig.Gate.Approve(Session, new ApproveCardRequest { PresenceId = "p-1" });
            var approval = await waiting;

            Assert.NotNull(approval);
            Assert.Empty(approval!.Variables);
            Assert.Null(approval.ExpectProjectRevision);
        }

        [Fact]
        public async Task TheRunThatWasAdmitted_IsToldWithItsId_AndARefusedOneWithItsReason()
        {
            var rig = new Rig();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);
            rig.Gate.Approve(Session, Approval("p-1"));
            var approval = (await waiting)!;

            approval.NotifyRunAdmitted("run-9");
            rig.Gate.RunRefused("p-2", AutoModeRefusal.ProjectChanged);
            rig.Gate.RunRefused("p-3", AutoModeRefusal.NotAdmitted);
            rig.Gate.RunRefused("p-4", AutoModeRefusal.CardChanged);

            var ended = rig.Events<CardPresentationEndedEvent>();
            Assert.Equal(
                new[]
                {
                    ("p-1", PresentationEndReasons.Approved, "run-9"),
                    ("p-2", PresentationEndReasons.ProjectChanged, (string?)null),
                    ("p-3", PresentationEndReasons.NotAdmitted, (string?)null),
                    ("p-4", PresentationEndReasons.CardChanged, (string?)null),
                },
                ended.Select(e => (e.PresenceId, e.Reason, e.RunId)));
        }

        [Fact]
        public async Task AnAnswerAfterTheTimeLimit_IsNoAnswer_EvenWhenTheTimerHasNotNoticedYet()
        {
            var rig = new Rig(TimeSpan.FromSeconds(30));
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            // The time of the event has come; the timer of the wait (30 s of real time) has not fired.
            rig.Clock = Now.AddSeconds(30);

            AssertNotPending(() => rig.Gate.Approve(Session, Approval("p-1")));

            // The wait ends as expired, with nothing approved, and nobody can still answer it.
            Assert.Null(await waiting);
            Assert.Equal(PresentationEndReasons.Expired, Assert.Single(rig.Events<CardPresentationEndedEvent>()).Reason);
            AssertNotPending(() => rig.Gate.Decline(Session, new DeclineCardRequest { PresenceId = "p-1" }));
        }

        [Fact]
        public async Task ADeclineAfterTheTimeLimit_IsNoAnswerEither_AndTheWaitEndsAsExpired()
        {
            var rig = new Rig(TimeSpan.FromSeconds(30));
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);
            rig.Clock = Now.AddSeconds(31);

            AssertNotPending(() => rig.Gate.Decline(Session, new DeclineCardRequest { PresenceId = "p-1" }));

            Assert.Null(await waiting);
            Assert.Equal(PresentationEndReasons.Expired, Assert.Single(rig.Events<CardPresentationEndedEvent>()).Reason);
        }

        [Theory]
        [InlineData(PresentationEndReasons.Stopped)]
        [InlineData(PresentationEndReasons.ClientLost)]
        public async Task AGateThatWasCancelledBeforeACardArrived_ShowsNothingAndWaitsForNothing(string reason)
        {
            var rig = new Rig();

            // The mode was stopped while the machine was on its way to ask: the gate has been cancelled, nobody was waited for yet.
            rig.Gate.Cancel(reason);
            var approval = await rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);

            Assert.Null(approval);
            Assert.Null(rig.Gate.PendingPresenceId);
            Assert.Empty(rig.Published);
            AssertNotPending(() => rig.Gate.Approve(Session, Approval("p-1")));
        }

        [Fact]
        public async Task ACardThatTheMachineWithdraws_RunsNothing_AndTheNoticeSaysTheCardChanged()
        {
            var rig = new Rig();
            using var withdrawn = new CancellationTokenSource();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), withdrawn.Token);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            withdrawn.Cancel();

            Assert.Null(await waiting);
            Assert.Equal(PresentationEndReasons.CardChanged, Assert.Single(rig.Events<CardPresentationEndedEvent>()).Reason);
            AssertNotPending(() => rig.Gate.Approve(Session, Approval("p-1")));
        }

        [Fact]
        public async Task AnApprovalThatWinsTheRaceAgainstTheWithdrawal_IsKeptAndTheOtherSideIsNotApplied()
        {
            var rig = new Rig();
            using var withdrawn = new CancellationTokenSource();
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), withdrawn.Token);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            rig.Gate.Approve(Session, Approval("p-1"));
            withdrawn.Cancel();

            // The first answer counts: the machine, which looks at the card again before it runs anything, has the last word.
            Assert.NotNull(await waiting);
            Assert.Empty(rig.Events<CardPresentationEndedEvent>());
        }

        [Fact]
        public async Task ARevisionThatCannotBeRead_IsLeftOutOfTheEvent_AndTheCardIsShownAllTheSame()
        {
            var rig = new Rig(readRevision: () => throw new InvalidOperationException("the project cannot be read"));
            var waiting = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId != null);

            rig.Gate.Decline(Session, new DeclineCardRequest { PresenceId = "p-1" });
            await waiting;

            Assert.Null(Assert.Single(rig.Events<CardPresentedEvent>()).ProjectRevision);
        }

        [Fact]
        public async Task OneCardAtATime_ANewCardEndsTheWaitOfALeftOverOne_AsStopped()
        {
            var rig = new Rig();
            var first = rig.Gate.RequestAsync(new AutoModePresence("p-1", "04AA"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId == "p-1");

            var second = rig.Gate.RequestAsync(new AutoModePresence("p-2", "04BB"), CancellationToken.None);
            await rig.WaitForAsync(() => rig.Gate.PendingPresenceId == "p-2");

            Assert.Null(await first);
            AssertNotPending(() => rig.Gate.Approve(Session, Approval("p-1")));
            rig.Gate.Approve(Session, Approval("p-2"));
            Assert.NotNull(await second);
        }
    }
}
