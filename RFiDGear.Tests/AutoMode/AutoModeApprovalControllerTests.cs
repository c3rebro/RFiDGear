#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Services.AutoMode;

using Xunit;

namespace RFiDGear.Tests.AutoMode
{
    /// <summary>
    /// Automatic mode that waits for the decision of a program about every card: the machine shows a card to the program, runs it
    /// only when it approves, uses the approval for that one run, and does not ask again for the same presence however it ended.
    /// The reader, the program and the run are stand-ins, as in <see cref="AutoModeControllerTests"/>.
    /// </summary>
    public sealed class AutoModeApprovalControllerTests
    {
        // The program that decides: each card gets the next scripted answer; without one it approves with no variables.
        private sealed class ScriptedApproval : IAutoModeApproval
        {
            private readonly Queue<Func<AutoModePresence, AutoModeApproval?>> _answers = new Queue<Func<AutoModePresence, AutoModeApproval?>>();

            public readonly List<AutoModePresence> Asked = new List<AutoModePresence>();
            public readonly List<(string PresenceId, AutoModeRefusal Refusal)> Refusals = new List<(string, AutoModeRefusal)>();

            public Func<AutoModePresence, Task>? WhileAsked;

            public void Answer(Func<AutoModePresence, AutoModeApproval?> decide) => _answers.Enqueue(decide);

            // When set, the program does not answer until the test does (or the machine withdraws the wait, which is an answer of "no").
            public TaskCompletionSource<AutoModeApproval?>? Hold;

            public void Approve(Dictionary<string, string>? variables = null, string? expectRevision = null) =>
                Answer(presence => new AutoModeApproval(presence.PresenceId, presence.Uid, variables ?? new Dictionary<string, string>(), expectRevision));

            public void Decline() => Answer(_ => null);

            public async Task<AutoModeApproval?> RequestAsync(AutoModePresence presence, CancellationToken withdrawn)
            {
                Asked.Add(presence);

                if (WhileAsked != null)
                {
                    await WhileAsked(presence);
                }

                if (Hold != null)
                {
                    // A wait that nobody ends fails the test after a few seconds instead of hanging it.
                    using (withdrawn.Register(() => Hold.TrySetResult(null)))
                    {
                        return await Hold.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    }
                }

                return _answers.Count > 0
                    ? _answers.Dequeue()(presence)
                    : new AutoModeApproval(presence.PresenceId, presence.Uid, new Dictionary<string, string>(), null);
            }

            public void RunRefused(string presenceId, AutoModeRefusal refusal) => Refusals.Add((presenceId, refusal));
        }

        private sealed class Rig
        {
            private int _runNumber;

            public readonly ExtensionOperationCoordinator Coordinator = new ExtensionOperationCoordinator();
            public AutoModePoll? Reader = new AutoModePoll(ReaderScanState.Ready, null);
            public readonly List<string> PlainRuns = new List<string>();
            public readonly List<(string Uid, AutoModeApproval Approval)> ApprovedRuns = new List<(string, AutoModeApproval)>();

            // Whether the machine still wanted the card at the moment it handed it to the runner (the window asks the same before it admits a run).
            public readonly List<bool> StillWanted = new List<bool>();
            public readonly List<string> Events = new List<string>();
            public readonly ScriptedApproval Approval = new ScriptedApproval();

            // What a run of an approved card ends with, in place of an admitted run that succeeded.
            public Func<AutoModeApproval, AutoModeRunReport>? ApprovedBehavior;

            // Runs while the runner waits for the execution slot, before it asks whether the card is still wanted.
            public Action? WhileTheRunnerWaits;

            // The next poll looks at the reader at once and then waits for this before it answers, as a slow read does.
            public TaskCompletionSource<bool>? HoldNextPoll;

            public readonly AutoModeController Controller;

            public Rig(bool canRunAnApprovedCard = true)
            {
                Controller = new AutoModeController(
                    Coordinator,
                    PollAsync,
                    (uid, _, _) =>
                    {
                        PlainRuns.Add(uid);

                        return Task.FromResult(Admitted());
                    },
                    _ => { },
                    action => action(),
                    () => true,
                    () => true,
                    canRunAnApprovedCard
                        ? (uid, _, isStillWanted, approval) =>
                        {
                            ApprovedRuns.Add((uid, approval));
                            WhileTheRunnerWaits?.Invoke();
                            var wanted = isStillWanted();
                            StillWanted.Add(wanted);

                            // As the window does: a request that is no longer wanted is not admitted.
                            if (!wanted)
                            {
                                return Task.FromResult(AutoModeRunReport.NotAdmitted);
                            }

                            return Task.FromResult(ApprovedBehavior != null ? ApprovedBehavior(approval) : Admitted());
                        }
                        : null);
                Controller.StateChanged += (_, _) => Events.Add(Controller.IsEnabled ? "on" : "off:" + Controller.LastStopReason);
            }

            private async Task<AutoModePoll?> PollAsync()
            {
                var seen = Reader;
                var hold = HoldNextPoll;
                HoldNextPoll = null;

                if (hold != null)
                {
                    await hold.Task.ConfigureAwait(false);
                }

                return seen;
            }

            public Task Tick() => Controller.TickAsync();

            public void Present(string uid) => Reader = new AutoModePoll(ReaderScanState.Ready, uid);

            public void Remove() => Reader = new AutoModePoll(ReaderScanState.Ready, null);

            public AutoModeRunReport Admitted()
            {
                var now = DateTimeOffset.UtcNow;
                var result = new ExecResult("run-" + ++_runNumber, ExecutionOutcome.Success, 1, 0, now, now, Array.Empty<TaskOutcome>(), ExecutionFailureReason.None);

                return new AutoModeRunReport(admitted: true, result, abandoned: false);
            }

            public void StartWithApproval() =>
                Assert.Equal(AutoModeStartResult.Started, Controller.TryStart(requireReady: false, keepApplicationOpen: false, Approval, out _));
        }

        [Fact]
        public async Task ACardThatIsApproved_IsRunOnce_WithTheApprovalOfThatCard_AndNotByTheRunnerOfPlainCards()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Approval.Approve(new Dictionary<string, string> { ["$EXPECTEDUID"] = "04AA" });
            rig.Present("04AA");

            for (var tick = 0; tick < 4; tick++)
            {
                await rig.Tick();
            }

            // Shown once, run once, with what the approval said; the card that stays is not shown again.
            var presence = Assert.Single(rig.Approval.Asked);
            Assert.Equal("04AA", presence.Uid);
            Assert.False(string.IsNullOrEmpty(presence.PresenceId));
            var run = Assert.Single(rig.ApprovedRuns);
            Assert.Equal("04AA", run.Uid);
            Assert.Equal(presence.PresenceId, run.Approval.PresenceId);
            Assert.Equal("04AA", run.Approval.Variables["$EXPECTEDUID"]);
            Assert.Empty(rig.PlainRuns);
            Assert.Empty(rig.Approval.Refusals);
        }

        [Fact]
        public async Task ACardThatIsNotApproved_IsNeverRun_AndNotAskedAgainWhileItStays()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Approval.Decline();
            rig.Present("04AA");

            for (var tick = 0; tick < 4; tick++)
            {
                await rig.Tick();
            }

            Assert.Single(rig.Approval.Asked);
            Assert.Empty(rig.ApprovedRuns);
            Assert.Empty(rig.PlainRuns);
            Assert.True(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task TheSameCardPutBackAfterARemoval_IsAnotherPresence_WithAnotherIdAndAnApprovalOfItsOwn()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Approval.Approve();
            rig.Approval.Decline();
            rig.Present("04AA");

            await rig.Tick();
            rig.Remove();
            await rig.Tick();
            rig.Present("04AA");
            await rig.Tick();

            // The approval of the first presence did not carry over: the second was asked for (and declined) on its own.
            Assert.Equal(2, rig.Approval.Asked.Count);
            Assert.NotEqual(rig.Approval.Asked[0].PresenceId, rig.Approval.Asked[1].PresenceId);
            Assert.Single(rig.ApprovedRuns);
        }

        [Fact]
        public async Task AnApprovedCardThatCouldNotBeAdmitted_IsOfferedAgainWithANewPresence_AndTheProgramIsTold()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            var first = true;
            rig.ApprovedBehavior = _ =>
            {
                if (first)
                {
                    first = false;

                    return AutoModeRunReport.NotAdmitted;
                }

                return rig.Admitted();
            };
            rig.Present("04AA");

            await rig.Tick();
            await rig.Tick();

            Assert.Equal(2, rig.Approval.Asked.Count);
            Assert.NotEqual(rig.Approval.Asked[0].PresenceId, rig.Approval.Asked[1].PresenceId);
            Assert.Equal((rig.Approval.Asked[0].PresenceId, AutoModeRefusal.NotAdmitted), Assert.Single(rig.Approval.Refusals));
            Assert.Equal(2, rig.ApprovedRuns.Count);
            Assert.NotSame(rig.ApprovedRuns[0].Approval, rig.ApprovedRuns[1].Approval);
        }

        [Fact]
        public async Task AProjectThatIsNotInTheApprovedState_RunsNothing_AndTheProgramIsToldWhy()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.ApprovedBehavior = _ => AutoModeRunReport.ProjectChanged;
            rig.Present("04AA");

            await rig.Tick();

            var refusal = Assert.Single(rig.Approval.Refusals);
            Assert.Equal((rig.Approval.Asked[0].PresenceId, AutoModeRefusal.ProjectChanged), refusal);
            Assert.True(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task WithoutAWayToRunAnApprovedCard_NothingRuns_EvenWhenTheProgramApproves()
        {
            var rig = new Rig(canRunAnApprovedCard: false);
            rig.StartWithApproval();
            rig.Present("04AA");

            await rig.Tick();

            Assert.Empty(rig.ApprovedRuns);
            Assert.Empty(rig.PlainRuns);
            Assert.Equal(AutoModeRefusal.NotAdmitted, Assert.Single(rig.Approval.Refusals).Refusal);
        }

        [Fact]
        public async Task TheApprovalBelongsToTheStart_AModeStartedAfterwardsWithoutOneRunsEveryCardAsBefore()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Controller.Stop();
            Assert.Equal(AutoModeStartResult.Started, rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false));
            rig.Present("04AA");

            await rig.Tick();

            Assert.Empty(rig.Approval.Asked);
            Assert.Empty(rig.ApprovedRuns);
            Assert.Equal(new[] { "04AA" }, rig.PlainRuns);
        }

        [Fact]
        public async Task AModeWithoutApproval_NeverAsksAnyone()
        {
            var rig = new Rig();
            Assert.Equal(AutoModeStartResult.Started, rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false));
            rig.Present("04AA");

            await rig.Tick();

            Assert.Equal(new[] { "04AA" }, rig.PlainRuns);
            Assert.Empty(rig.Approval.Asked);
        }

        [Fact]
        public async Task AProgramThatFails_StopsTheMode_NothingIsRunOnTheStrengthOfIt()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Approval.WhileAsked = _ => throw new InvalidOperationException("the program broke");
            rig.Present("04AA");

            await rig.Tick();

            Assert.Empty(rig.ApprovedRuns);
            Assert.False(rig.Controller.IsEnabled);
            Assert.Equal(AutoModeStopReason.RunAborted, rig.Controller.LastStopReason);
        }

        [Fact]
        public async Task AModeThatIsStoppedWhileTheProgramDecides_TellsTheRunnerThatTheCardIsNoLongerWanted()
        {
            var rig = new Rig();
            rig.StartWithApproval();

            // The program is asked, and the mode is switched off before it answers "yes".
            rig.Approval.WhileAsked = _ =>
            {
                rig.Controller.Stop();

                return Task.CompletedTask;
            };
            rig.Present("04AA");

            await rig.Tick();

            // The window does not admit a run that is no longer wanted (that is the check it is given); here the runner is told so.
            Assert.False(rig.Controller.IsEnabled);
            Assert.Equal("04AA", Assert.Single(rig.ApprovedRuns).Uid);
            Assert.False(Assert.Single(rig.StillWanted));
        }

        // ── the card while the program decides ──

        private static AutoModeApproval ApprovalFor(AutoModePresence presence) =>
            new AutoModeApproval(presence.PresenceId, presence.Uid, new Dictionary<string, string>(), null);

        [Fact]
        public async Task ACardThatIsTakenAwayWhileTheProgramDecides_IsNeverRun_AndWhenPutBackItIsANewPresence()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Approval.Hold = new TaskCompletionSource<AutoModeApproval?>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Present("04AA");
            var waiting = rig.Tick();

            // The tick that waits holds the machine; the next ticks look at the reader and see that the card is gone.
            rig.Remove();
            await rig.Tick();
            await waiting;
            rig.Approval.Hold = null;
            rig.Present("04AA");
            await rig.Tick();

            Assert.Equal(2, rig.Approval.Asked.Count);
            Assert.NotEqual(rig.Approval.Asked[0].PresenceId, rig.Approval.Asked[1].PresenceId);
            Assert.Equal(rig.Approval.Asked[1].PresenceId, Assert.Single(rig.ApprovedRuns).Approval.PresenceId);
            Assert.True(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task ACardThatIsReplacedWhileTheProgramDecides_IsNeverRunOnTheOldApproval_AndTheOtherCardIsShownNext()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Approval.Hold = new TaskCompletionSource<AutoModeApproval?>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Present("04AA");
            var waiting = rig.Tick();

            rig.Present("04BB");
            await rig.Tick();
            await waiting;
            rig.Approval.Hold = null;
            await rig.Tick();

            Assert.Equal(new[] { "04AA", "04BB" }, rig.Approval.Asked.Select(p => p.Uid));
            Assert.Equal("04BB", Assert.Single(rig.ApprovedRuns).Uid);
        }

        [Fact]
        public async Task ACardThatStays_KeepsWaiting_WhateverTheReaderSaysThatSaysNothingAboutIt()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Approval.Hold = new TaskCompletionSource<AutoModeApproval?>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Present("04AA");
            var waiting = rig.Tick();

            // A reader that cannot tell, a reader that is busy and the same card again: none of them withdraws the wait.
            rig.Reader = new AutoModePoll(ReaderScanState.ReadFailed, null);
            await rig.Tick();
            rig.Reader = null;
            await rig.Tick();
            rig.Reader = new AutoModePoll(ReaderScanState.Busy, null);
            await rig.Tick();
            rig.Present("04AA");
            await rig.Tick();
            var stillWaiting = !waiting.IsCompleted;
            rig.Approval.Hold.SetResult(ApprovalFor(rig.Approval.Asked[0]));
            await waiting;

            Assert.True(stillWaiting);
            Assert.Equal("04AA", Assert.Single(rig.ApprovedRuns).Uid);
        }

        [Fact]
        public async Task ACardThatAnotherRunProcessedWhileTheProgramDecides_IsNotRunAgainOnTheApproval()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Approval.Hold = new TaskCompletionSource<AutoModeApproval?>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Present("04AA");
            var waiting = rig.Tick();

            // A run by hand (or of an extension) is made on the card that is waited for, and says so.
            rig.Controller.RememberCard("04AA");
            rig.Approval.Hold.SetResult(ApprovalFor(rig.Approval.Asked[0]));
            await waiting;
            await rig.Tick();

            // Nothing was run for the approval, the program was told, and the card is not shown again: it was processed.
            Assert.Empty(rig.ApprovedRuns);
            Assert.Equal((rig.Approval.Asked[0].PresenceId, AutoModeRefusal.CardChanged), Assert.Single(rig.Approval.Refusals));
            Assert.Single(rig.Approval.Asked);
        }

        [Fact]
        public async Task ALookAtTheReaderThatEndsAfterAnotherRunWroteTheCardDown_DoesNotEraseWhatThatRunWrote()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.Approval.Hold = new TaskCompletionSource<AutoModeApproval?>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Present("04AA");
            var waiting = rig.Tick();

            // The watcher looks (it sees no card) and the read is slow; meanwhile a run by hand is made on the card and says so.
            var slowRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.HoldNextPoll = slowRead;
            rig.Remove();
            var watching = rig.Tick();
            rig.Controller.RememberCard("04AA");
            slowRead.SetResult(true);
            await watching;
            await waiting;

            // The wait is withdrawn, but what the other run wrote down is the truth: the card was processed, and is not shown again.
            rig.Present("04AA");
            await rig.Tick();

            Assert.Single(rig.Approval.Asked);
            Assert.Empty(rig.ApprovedRuns);
        }

        [Fact]
        public async Task ACardThatAnotherRunProcessesWhileTheRunnerWaitsForTheSlot_IsNotRunOnTheApproval_AndTheProgramIsToldTheCardChanged()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            rig.WhileTheRunnerWaits = () => rig.Controller.RememberCard("04AA");
            rig.Present("04AA");

            await rig.Tick();
            await rig.Tick();

            // The runner was asked, found the card taken (the question it is given covers that), and did not admit anything.
            Assert.False(Assert.Single(rig.StillWanted));
            Assert.Equal((rig.Approval.Asked[0].PresenceId, AutoModeRefusal.CardChanged), Assert.Single(rig.Approval.Refusals));
            Assert.Single(rig.Approval.Asked);
            Assert.True(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task ACardThatTheRunFindsReplacedAtTheStart_IsOfferedAgainAsANewPresence()
        {
            var rig = new Rig();
            rig.StartWithApproval();
            var first = true;
            rig.ApprovedBehavior = _ =>
            {
                if (first)
                {
                    first = false;

                    return AutoModeRunReport.CardChanged;
                }

                return rig.Admitted();
            };
            rig.Present("04AA");

            await rig.Tick();
            await rig.Tick();

            Assert.Equal((rig.Approval.Asked[0].PresenceId, AutoModeRefusal.CardChanged), Assert.Single(rig.Approval.Refusals));
            Assert.Equal(2, rig.Approval.Asked.Count);
            Assert.NotEqual(rig.Approval.Asked[0].PresenceId, rig.Approval.Asked[1].PresenceId);
            Assert.Equal(2, rig.ApprovedRuns.Count);
        }
    }
}
