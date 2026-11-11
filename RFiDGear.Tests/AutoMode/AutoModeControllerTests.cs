using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Services.AutoMode;

using Xunit;

namespace RFiDGear.Tests.AutoMode
{
    /// <summary>
    /// The state machine of automatic mode, without a window: the reader, the run and the clock are stand-ins, so each
    /// case says what the reader shows at a tick and what the run ends with. The cases follow the acceptance cases of
    /// issue #233.
    /// </summary>
    public class AutoModeControllerTests
    {
        private sealed class Rig
        {
            private int _runNumber;

            public readonly ExtensionOperationCoordinator Coordinator = new ExtensionOperationCoordinator();

            /// <summary>What the reader shows at the next tick; <c>null</c> means somebody else holds it.</summary>
            public AutoModePoll? Reader = new AutoModePoll(ReaderScanState.Ready, null);

            public readonly List<string> Runs = new List<string>();
            public readonly List<bool> KeepOpen = new List<bool>();
            public readonly List<bool> Clock = new List<bool>();
            public readonly List<string> Events = new List<string>();

            /// <summary>What a run does, in place of ending with success at once.</summary>
            public Func<string, Task<AutoModeRunReport>>? RunBehavior;

            public bool ReaderSelected = true;
            public bool HasTasks = true;
            public readonly AutoModeController Controller;

            public Rig()
            {
                Controller = new AutoModeController(
                    Coordinator,
                    () => Task.FromResult(Reader),
                    RunAsync,
                    Clock.Add,
                    action => action(),
                    () => ReaderSelected,
                    () => HasTasks);
                Controller.StateChanged += (_, _) => Events.Add(Controller.IsEnabled ? "on" : "off:" + Controller.LastStopReason);
                Controller.RunCompleted += (_, result) => Events.Add("run:" + result.RunId);
            }

            public Task Tick() => Controller.TickAsync();

            public void Present(string uid) => Reader = new AutoModePoll(ReaderScanState.Ready, uid);

            public void Remove() => Reader = new AutoModePoll(ReaderScanState.Ready, null);

            public void Show(ReaderScanState state) => Reader = new AutoModePoll(state, null);

            public AutoModeRunReport Admitted(
                ExecutionOutcome outcome = ExecutionOutcome.Success,
                ExecutionFailureReason reason = ExecutionFailureReason.None,
                bool abandoned = false)
            {
                var now = DateTimeOffset.UtcNow;
                var result = new ExecResult("run-" + ++_runNumber, outcome, 1, 0, now, now, Array.Empty<TaskOutcome>(), reason);

                return new AutoModeRunReport(admitted: true, result, abandoned);
            }

            /// <summary>Stands for the wait for a reader scan that a run may have to make before it is admitted.</summary>
            public Func<Task>? WaitForAdmission;

            private async Task<AutoModeRunReport> RunAsync(string uid, bool keepOpen, Func<bool> isStillWanted)
            {
                if (WaitForAdmission != null)
                {
                    await WaitForAdmission();

                    // As the main window does: a request that was withdrawn while it waited is not admitted.
                    if (!isStillWanted())
                    {
                        return AutoModeRunReport.NotAdmitted;
                    }
                }

                Runs.Add(uid);
                KeepOpen.Add(keepOpen);

                return RunBehavior != null ? await RunBehavior(uid) : Admitted();
            }
        }

        private static Rig StartedRig(bool keepApplicationOpen = false)
        {
            var rig = new Rig();
            Assert.Equal(AutoModeStartResult.Started, rig.Controller.TryStart(requireReady: false, keepApplicationOpen));

            return rig;
        }

        // ── 1. one run for a new card, none while it stays ──

        [Fact]
        public async Task NothingRuns_BeforeAutomaticModeIsStarted()
        {
            var rig = new Rig();
            rig.Present("A");

            await rig.Tick();

            Assert.Empty(rig.Runs);
            Assert.False(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task ANewCard_RunsOnce_AndNotAgainWhileItStaysOnTheReader()
        {
            var rig = StartedRig();
            rig.Present("A");

            for (var tick = 0; tick < 5; tick++)
            {
                await rig.Tick();
            }

            Assert.Equal(new[] { "A" }, rig.Runs);
        }

        [Fact]
        public async Task ACardThatIsAlreadyOnTheReaderAtStart_CountsAsNewlyPresented()
        {
            var rig = new Rig();
            rig.Present("A");
            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);

            await rig.Tick();

            Assert.Equal(new[] { "A" }, rig.Runs);
        }

        // ── 2. taking the card away and putting it back, or another card ──

        [Fact]
        public async Task TheSameCard_PutBackAfterARemoval_RunsAgain_WithADifferentRunId()
        {
            var rig = StartedRig();
            var ids = new List<string>();
            rig.Controller.RunCompleted += (_, result) => ids.Add(result.RunId);

            rig.Present("A");
            await rig.Tick();
            rig.Remove();
            await rig.Tick();
            rig.Present("A");
            await rig.Tick();

            Assert.Equal(new[] { "A", "A" }, rig.Runs);
            Assert.Equal(2, ids.Distinct().Count());
        }

        [Fact]
        public async Task AnotherCard_RunsAtOnce_WithoutARemovalBetween()
        {
            var rig = StartedRig();

            rig.Present("A");
            await rig.Tick();
            rig.Present("B");
            await rig.Tick();
            await rig.Tick();

            Assert.Equal(new[] { "A", "B" }, rig.Runs);
        }

        [Fact]
        public async Task AReadThatFails_DoesNotEndThePresence_SoTheCardThatStaysIsNotRunAgain()
        {
            var rig = StartedRig();
            rig.Present("A");
            await rig.Tick();

            rig.Show(ReaderScanState.ReadFailed);
            await rig.Tick();
            rig.Present("A");
            await rig.Tick();

            Assert.Equal(new[] { "A" }, rig.Runs);
            Assert.True(rig.Controller.IsEnabled);
        }

        // ── 3. stop, reader loss, failures ──

        [Fact]
        public async Task StopBeforeACard_MeansNoRun_AndTheClockEndsOff()
        {
            var rig = StartedRig();
            rig.Controller.Stop();
            rig.Present("A");

            await rig.Tick();

            Assert.Empty(rig.Runs);
            Assert.False(rig.Clock.Last());
        }

        [Fact]
        public async Task StopDuringARun_LetsTheRunEnd_ReportsItAndStartsNothingAfterIt()
        {
            var rig = StartedRig();
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.RunBehavior = async _ =>
            {
                Assert.True(rig.Coordinator.TryBeginExecution());
                await release.Task;
                rig.Coordinator.EndExecution();

                return rig.Admitted();
            };
            rig.Present("A");

            var tick = rig.Tick();
            rig.Controller.Stop();
            Assert.False(tick.IsCompleted, "the run is not interrupted");
            release.SetResult(true);
            await tick;

            Assert.Single(rig.Events, e => e.StartsWith("run:"));
            Assert.False(rig.Controller.IsEnabled);

            rig.Remove();
            await rig.Tick();
            rig.Present("A");
            await rig.Tick();
            Assert.Single(rig.Runs);
            Assert.False(rig.Clock.Last());
        }

        [Fact]
        public async Task AStopThatArrivesBeforeTheRunPutsTheClockBack_StillLeavesTheClockOff()
        {
            var rig = StartedRig();

            // What a run does to the clock: it switches it off for its length and puts back what it found.
            rig.RunBehavior = _ =>
            {
                Assert.True(rig.Coordinator.TryBeginExecution());
                rig.Controller.Stop();
                rig.Clock.Add(true);
                rig.Coordinator.EndExecution();

                return Task.FromResult(rig.Admitted());
            };
            rig.Present("A");

            await rig.Tick();

            Assert.False(rig.Clock.Last());
        }

        [Fact]
        public async Task AReaderThatHadAnswered_AndThenFailsThreeTimes_IsLost_NoRun_AndAutomaticModeStops()
        {
            var rig = StartedRig();
            await rig.Tick();   // an answer, without a card

            rig.Show(ReaderScanState.NoReader);
            await rig.Tick();
            await rig.Tick();
            Assert.True(rig.Controller.IsEnabled);
            await rig.Tick();

            Assert.False(rig.Controller.IsEnabled);
            Assert.Equal(AutoModeStopReason.ReaderLost, rig.Controller.LastStopReason);
            Assert.Empty(rig.Runs);
        }

        [Fact]
        public async Task AReaderThatNeverAnswered_IsWaitedFor_NotGivenUp()
        {
            var rig = StartedRig();
            rig.Show(ReaderScanState.NoReader);

            for (var tick = 0; tick < 10; tick++)
            {
                await rig.Tick();
            }

            Assert.True(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task AnAnswerBetweenTheFailures_StartsTheCountingOver()
        {
            var rig = StartedRig();
            await rig.Tick();

            for (var round = 0; round < 3; round++)
            {
                rig.Show(ReaderScanState.ReadFailed);
                await rig.Tick();
                await rig.Tick();
                rig.Remove();
                await rig.Tick();
            }

            Assert.True(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task ATaskThatFailed_DoesNotStopAutomaticMode_AndTheCardIsNotRetriedWhileItStays()
        {
            var rig = StartedRig();
            rig.RunBehavior = _ => Task.FromResult(rig.Admitted(ExecutionOutcome.Failure));
            rig.Present("A");

            await rig.Tick();
            await rig.Tick();
            await rig.Tick();

            Assert.True(rig.Controller.IsEnabled);
            Assert.Single(rig.Runs);

            // Retrying is for the operator: take the card away and put it back.
            rig.Remove();
            await rig.Tick();
            rig.Present("A");
            await rig.Tick();
            Assert.Equal(2, rig.Runs.Count);
        }

        [Theory]
        [InlineData(ExecutionFailureReason.Faulted, "RunAborted")]
        [InlineData(ExecutionFailureReason.Cancelled, "RunAborted")]
        [InlineData(ExecutionFailureReason.TimedOut, "RunTimedOut")]
        public async Task ARunThatFaultedWasCancelledOrTimedOut_StopsAutomaticMode_AndTheCardIsNotRunAgain(
            ExecutionFailureReason reason, string expectedStopReason)
        {
            var rig = StartedRig();
            rig.RunBehavior = _ => Task.FromResult(rig.Admitted(ExecutionOutcome.Failure, reason));
            rig.Present("A");

            await rig.Tick();

            Assert.False(rig.Controller.IsEnabled);
            Assert.Equal(expectedStopReason, rig.Controller.LastStopReason.ToString());

            // Started again, the card that is still there is the same presence: nothing repeats it.
            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            await rig.Tick();
            Assert.Single(rig.Runs);
        }

        [Fact]
        public async Task ARunTheWatchdogGaveUp_StopsAutomaticMode_EvenThoughItReadsLikeAnOrdinaryFailure()
        {
            var rig = StartedRig();
            rig.RunBehavior = _ => Task.FromResult(rig.Admitted(ExecutionOutcome.Failure, ExecutionFailureReason.None, abandoned: true));
            rig.Present("A");

            await rig.Tick();

            Assert.False(rig.Controller.IsEnabled);
            Assert.Equal(AutoModeStopReason.RunTimedOut, rig.Controller.LastStopReason);
        }

        [Fact]
        public async Task ARunThatThrows_StopsAutomaticMode_AndTheCardCountsAsProcessed()
        {
            var rig = StartedRig();
            rig.RunBehavior = _ => throw new InvalidOperationException("The run blew up.");
            rig.Present("A");

            await rig.Tick();

            Assert.False(rig.Controller.IsEnabled);
            Assert.Equal(AutoModeStopReason.RunAborted, rig.Controller.LastStopReason);

            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            await rig.Tick();
            Assert.Single(rig.Runs);
        }

        // ── 4. busy, repeated start and stop, readiness ──

        [Fact]
        public void StartWhileSomethingIsInProgress_IsRefused_AndChangesNothing()
        {
            foreach (var begin in new Func<ExtensionOperationCoordinator, bool>[]
            {
                c => c.TryBeginLoad(), c => c.TryBeginExecution(), c => c.TryBeginProjectOperation(), c => c.TryBeginScan()
            })
            {
                var rig = new Rig();
                Assert.True(begin(rig.Coordinator));

                Assert.Equal(AutoModeStartResult.Busy, rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false));

                Assert.False(rig.Controller.IsEnabled);
                Assert.Empty(rig.Events);
            }
        }

        [Fact]
        public void StartAndStop_AreIdempotent_AndEachRealChangeRaisesOneEvent()
        {
            var rig = new Rig();

            Assert.Equal(AutoModeStartResult.Started, rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false));
            Assert.Equal(AutoModeStartResult.AlreadyEnabled, rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false));
            rig.Controller.Stop();
            rig.Controller.Stop();

            Assert.Equal(new[] { "on", "off:" + AutoModeStopReason.StopRequested }, rig.Events);
        }

        [Fact]
        public void WhenReadinessIsRequired_ARunningWithoutReaderOrTasks_IsRefused()
        {
            var rig = new Rig { ReaderSelected = false };
            Assert.Equal(AutoModeStartResult.NoReader, rig.Controller.TryStart(requireReady: true, keepApplicationOpen: true));

            rig.ReaderSelected = true;
            rig.HasTasks = false;
            Assert.Equal(AutoModeStartResult.NoTasks, rig.Controller.TryStart(requireReady: true, keepApplicationOpen: true));
            Assert.False(rig.Controller.IsEnabled);

            // The menu does not insist.
            Assert.Equal(AutoModeStartResult.Started, rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false));
        }

        [Fact]
        public async Task ATickThatMeetsARunOrAProjectEdit_DoesNothing_AndTheCardIsTakenAtALaterTick()
        {
            var rig = StartedRig();
            rig.Present("A");
            Assert.True(rig.Coordinator.TryBeginProjectOperation());

            await rig.Tick();
            Assert.Empty(rig.Runs);

            rig.Coordinator.EndProjectOperation();
            await rig.Tick();
            Assert.Equal(new[] { "A" }, rig.Runs);
        }

        [Fact]
        public async Task ARunThatWasNotAdmitted_LeavesTheCardPending_SoTheNextTickTriesAgain()
        {
            var rig = StartedRig();
            var refusals = 2;
            rig.RunBehavior = _ => Task.FromResult(refusals-- > 0 ? AutoModeRunReport.NotAdmitted : rig.Admitted());
            rig.Present("A");

            await rig.Tick();
            await rig.Tick();
            Assert.Empty(rig.Events.Where(e => e.StartsWith("run:")));

            await rig.Tick();
            await rig.Tick();

            Assert.Equal(3, rig.Runs.Count);
            Assert.Single(rig.Events, e => e.StartsWith("run:"));
        }

        [Fact]
        public async Task ATickThatMeetsAHeldReader_DoesNothing()
        {
            var rig = StartedRig();
            rig.Reader = null;

            await rig.Tick();

            Assert.Empty(rig.Runs);
            Assert.True(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task TicksDoNotOverlap_ASecondOneDuringARunIsSkipped()
        {
            var rig = StartedRig();
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.RunBehavior = async _ =>
            {
                await release.Task;

                return rig.Admitted();
            };
            rig.Present("A");

            var first = rig.Tick();
            await rig.Tick();
            release.SetResult(true);
            await first;

            Assert.Single(rig.Runs);
        }

        // ── 5. what an observer sees ──

        [Fact]
        public async Task TheResultOfARun_IsReportedBeforeTheStopItCauses()
        {
            var rig = StartedRig();
            rig.RunBehavior = _ => Task.FromResult(rig.Admitted(ExecutionOutcome.Failure, ExecutionFailureReason.Faulted));
            rig.Present("A");

            await rig.Tick();

            Assert.Equal(new[] { "on", "run:run-1", "off:" + AutoModeStopReason.RunAborted }, rig.Events);
        }

        [Fact]
        public async Task ASubscriberThatThrows_DoesNotStopTheMachine_NorStarveTheOnesAfterIt()
        {
            var rig = StartedRig();
            var later = 0;
            rig.Controller.RunCompleted += (_, _) => throw new InvalidOperationException("Key=0011223344556677");
            rig.Controller.RunCompleted += (_, _) => later++;
            rig.Controller.StateChanged += (_, _) => throw new InvalidOperationException("nope");
            rig.Present("A");

            await rig.Tick();
            rig.Controller.Stop();

            Assert.Equal(1, later);
            Assert.False(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task TheKeepOpenFlag_IsTheOneTheLastStartAskedFor()
        {
            var rig = StartedRig(keepApplicationOpen: true);
            rig.Present("A");
            await rig.Tick();

            rig.Controller.Stop();
            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            rig.Remove();
            await rig.Tick();
            rig.Present("A");
            await rig.Tick();

            Assert.Equal(new[] { true, false }, rig.KeepOpen);
        }

        // ── the card is remembered across stop and start, and by runs made by hand ──

        [Fact]
        public async Task ACardThatWasRunByHand_IsNotRunAgainByAutomaticMode()
        {
            var rig = new Rig();
            rig.Controller.RememberCard("A");
            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            rig.Present("A");

            await rig.Tick();
            Assert.Empty(rig.Runs);

            rig.Remove();
            await rig.Tick();
            rig.Present("A");
            await rig.Tick();
            Assert.Equal(new[] { "A" }, rig.Runs);
        }

        [Fact]
        public async Task StopAndStartWithTheCardStillThere_DoesNotRunItAgain()
        {
            var rig = StartedRig();
            rig.Present("A");
            await rig.Tick();

            rig.Controller.Stop();
            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            await rig.Tick();

            Assert.Single(rig.Runs);
        }

        [Fact]
        public async Task TheCardTheRunActuallyRead_IsTheOneThatIsRemembered_NotTheOneThePollSaw()
        {
            var rig = StartedRig();
            rig.RunBehavior = _ =>
            {
                // The card was swapped between the poll and the run's own read.
                rig.Controller.RememberCard("B");

                return Task.FromResult(rig.Admitted());
            };
            rig.Present("A");
            await rig.Tick();

            rig.Present("B");
            await rig.Tick();

            Assert.Equal(new[] { "A" }, rig.Runs);
        }

        [Fact]
        public async Task AChangeOfTheReader_StopsAutomaticMode_AndForgetsTheCard()
        {
            var rig = StartedRig();
            rig.Present("A");
            await rig.Tick();

            rig.Controller.OnReaderChanged();

            Assert.False(rig.Controller.IsEnabled);
            Assert.Equal(AutoModeStopReason.ReaderChanged, rig.Controller.LastStopReason);

            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            await rig.Tick();
            Assert.Equal(2, rig.Runs.Count);
        }

        // ── a request that waits for admission must not outlive the session that made it ──

        [Fact]
        public async Task AStopWhileTheRunWaitsForAdmission_WithdrawsTheRequest_SoNothingRunsAfterIt()
        {
            var rig = StartedRig();
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.WaitForAdmission = () => release.Task;
            rig.Present("A");

            var tick = rig.Tick();
            rig.Controller.Stop();
            release.SetResult(true);
            await tick;

            Assert.Empty(rig.Runs);
            Assert.DoesNotContain(rig.Events, e => e.StartsWith("run:"));

            // Nothing happened to the card, so it is not remembered: a later start takes it.
            rig.WaitForAdmission = null;
            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            await rig.Tick();
            Assert.Equal(new[] { "A" }, rig.Runs);
        }

        [Fact]
        public async Task AStopAndANewStartWhileTheRunWaits_AlsoWithdrawTheRequestOfTheOldSession()
        {
            var rig = StartedRig();
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.WaitForAdmission = () => release.Task;
            rig.Present("A");

            var tick = rig.Tick();
            rig.Controller.Stop();
            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            release.SetResult(true);
            await tick;
            Assert.Empty(rig.Runs);

            // The new session takes the card, once.
            rig.WaitForAdmission = null;
            await rig.Tick();
            await rig.Tick();
            Assert.Equal(new[] { "A" }, rig.Runs);
        }

        // ── a run made by hand is judged by the same safety rules ──

        [Theory]
        [InlineData(ExecutionFailureReason.Faulted)]
        [InlineData(ExecutionFailureReason.TimedOut)]
        [InlineData(ExecutionFailureReason.Cancelled)]
        public void ARunByHandThatEndedBadly_StopsAModeThatIsOn_WithoutReportingItAsARunOfTheMode(ExecutionFailureReason reason)
        {
            var rig = StartedRig();

            rig.Controller.OnRunByHandEnded(rig.Admitted(ExecutionOutcome.Failure, reason));

            Assert.False(rig.Controller.IsEnabled);
            Assert.DoesNotContain(rig.Events, e => e.StartsWith("run:"));
        }

        [Fact]
        public void ARunByHandThatTheWatchdogGaveUp_StopsAModeThatIsOn_ButAnOrdinaryFailureDoesNot()
        {
            var rig = StartedRig();

            rig.Controller.OnRunByHandEnded(rig.Admitted(ExecutionOutcome.Failure));
            rig.Controller.OnRunByHandEnded(AutoModeRunReport.NotAdmitted);
            Assert.True(rig.Controller.IsEnabled);

            rig.Controller.OnRunByHandEnded(rig.Admitted(ExecutionOutcome.Failure, abandoned: true));
            Assert.False(rig.Controller.IsEnabled);
            Assert.Equal(AutoModeStopReason.RunTimedOut, rig.Controller.LastStopReason);
        }

        [Fact]
        public void ARunByHand_ChangesNothing_WhenTheModeIsOff()
        {
            var rig = new Rig();

            rig.Controller.OnRunByHandEnded(rig.Admitted(ExecutionOutcome.Failure, ExecutionFailureReason.Faulted));

            Assert.Empty(rig.Events);
            Assert.Equal(AutoModeStopReason.None, rig.Controller.LastStopReason);
        }

        // ── the clock follows the state, whatever else touches it ──

        [Fact]
        public void TheClock_FollowsStartAndStop()
        {
            var rig = new Rig();

            rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            rig.Controller.Stop();

            Assert.Equal(new[] { true, false }, rig.Clock);
        }

        [Fact]
        public void WhenARunEnds_TheClockIsSetRight_NoMatterWhatTheRunPutBack()
        {
            var rig = StartedRig();
            rig.Controller.Stop();
            rig.Clock.Clear();

            Assert.True(rig.Coordinator.TryBeginExecution());
            rig.Coordinator.EndExecution();

            Assert.Equal(new[] { false }, rig.Clock);
        }

        [Fact]
        public async Task ATickOfAClockThatShouldBeOff_SwitchesItOff_AndDoesNothingElse()
        {
            var rig = new Rig();
            rig.Present("A");

            await rig.Tick();

            Assert.Empty(rig.Runs);
            Assert.Equal(new[] { false }, rig.Clock);
        }

        // ── holding the tick back ──

        [Fact]
        public async Task WhilePolling_IsPaused_NothingIsRead_AndAStopOrStartKeepsItsEffect()
        {
            var rig = StartedRig();
            rig.Present("A");

            using (rig.Controller.PausePolling())
            {
                await rig.Tick();
                Assert.Empty(rig.Runs);

                rig.Controller.Stop();
                rig.Controller.TryStart(requireReady: false, keepApplicationOpen: false);
            }

            await rig.Tick();

            Assert.Equal(new[] { "A" }, rig.Runs);
            Assert.True(rig.Controller.IsEnabled);
        }

        [Fact]
        public async Task APauseScope_EndsOnce_EvenIfDisposedTwice()
        {
            var rig = StartedRig();
            rig.Present("A");

            var first = rig.Controller.PausePolling();
            var second = rig.Controller.PausePolling();
            first.Dispose();
            first.Dispose();
            await rig.Tick();
            Assert.Empty(rig.Runs);

            second.Dispose();
            await rig.Tick();
            Assert.Single(rig.Runs);
        }
    }
}
