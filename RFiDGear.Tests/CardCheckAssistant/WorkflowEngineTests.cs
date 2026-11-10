using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    public sealed class WorkflowEngineTests
    {
        // ── the tables ──────────────────────────────────────────────────────

        [Fact]
        public void EveryState_HasATransitionEntry_ASetOfControls_AndACloseAdvice()
        {
            foreach (var state in WorkflowTables.States)
            {
                Assert.NotNull(WorkflowTables.Next(state));
                _ = WorkflowTables.ControlsOf(state);
                _ = WorkflowTables.CloseAdviceOf(state);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => WorkflowTables.Next((WorkflowState)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorkflowTables.ControlsOf((WorkflowState)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorkflowTables.CloseAdviceOf((WorkflowState)99));
        }

        [Fact]
        public void EveryState_IsReachableFromIdle_AndEveryStateButTheEndsHasAWayOnAndCanFault()
        {
            var reached = new HashSet<WorkflowState> { WorkflowState.Idle };
            var open = new Queue<WorkflowState>(new[] { WorkflowState.Idle });
            while (open.Count > 0)
            {
                foreach (var next in WorkflowTables.Next(open.Dequeue()))
                {
                    if (reached.Add(next))
                    {
                        open.Enqueue(next);
                    }
                }
            }

            Assert.Equal(WorkflowTables.States.OrderBy(s => s), reached.OrderBy(s => s));
            foreach (var state in WorkflowTables.States)
            {
                Assert.NotEmpty(WorkflowTables.Next(state));
                if (!WorkflowTables.IsEnd(state))
                {
                    Assert.True(WorkflowTables.IsAllowed(state, WorkflowState.Faulted), state + " cannot fault");
                }
            }
        }

        [Fact]
        public void TheWayBackToIdle_IsOpenOnlyFromTheEndsAndFromTheStepsThatMaybeCancelledOrDeclined()
        {
            var leadingToIdle = WorkflowTables.States.Where(s => WorkflowTables.IsAllowed(s, WorkflowState.Idle)).OrderBy(s => s);

            Assert.Equal(
                new[]
                {
                    WorkflowState.LoadingOrders,
                    WorkflowState.Starting,
                    WorkflowState.ConfirmingReplace,
                    WorkflowState.Completed,
                    WorkflowState.Cancelled,
                    WorkflowState.Faulted,
                }.OrderBy(s => s),
                leadingToIdle);
            Assert.All(
                WorkflowTables.States.Where(WorkflowTables.IsEnd),
                state => Assert.Equal(new[] { WorkflowState.Idle }, WorkflowTables.Next(state)));
        }

        [Fact]
        public void TheControls_OfEachState_AreTheOnesThePlanGives()
        {
            Assert.Equal(WorkflowControls.Refresh | WorkflowControls.Start, WorkflowTables.ControlsOf(WorkflowState.Idle));
            Assert.Equal(WorkflowControls.None, WorkflowTables.ControlsOf(WorkflowState.Completing));
            Assert.Equal(WorkflowControls.ConfirmReplace | WorkflowControls.DeclineReplace, WorkflowTables.ControlsOf(WorkflowState.ConfirmingReplace));
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, WorkflowTables.ControlsOf(WorkflowState.RunFailed));
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, WorkflowTables.ControlsOf(WorkflowState.CompletionFailed));
            Assert.All(
                WorkflowTables.States.Where(WorkflowTables.IsEnd),
                state => Assert.Equal(WorkflowControls.Back, WorkflowTables.ControlsOf(state)));
            Assert.Equal(CloseAdvice.Refuse, WorkflowTables.CloseAdviceOf(WorkflowState.Checking));
            Assert.Equal(CloseAdvice.Refuse, WorkflowTables.CloseAdviceOf(WorkflowState.Completing));
            Assert.Equal(CloseAdvice.Confirm, WorkflowTables.CloseAdviceOf(WorkflowState.WaitingForCard));
            Assert.Equal(CloseAdvice.Allow, WorkflowTables.CloseAdviceOf(WorkflowState.Idle));
        }

        // ── the list ────────────────────────────────────────────────────────

        [Fact]
        public Task RefreshOrders_ReadsTheList_AndAFailedReadKeepsTheLastGoodOne() => Sta(async rig =>
        {
            Assert.True(rig.Engine.RefreshOrders());
            Assert.Equal(WorkflowState.LoadingOrders, rig.Now.State);
            Assert.True(rig.Now.IsBusy);
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.Equal(5, rig.Now.Orders.Count);
            Assert.True(rig.Now.IsTestMode);
            Assert.Equal(ReportLanguage.German, rig.Now.DefaultLanguage);
            Assert.Null(rig.Now.OrdersFailure);

            rig.Store.ListAnswer = Result<IReadOnlyList<OrderRecord>>.Fail(new Failure(FailureKind.Unreachable, "orders.unreachable", "No server."));
            Assert.True(rig.Engine.RefreshOrders());
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Equal("orders.unreachable", rig.Now.OrdersFailure!.Code);
            Assert.Equal(5, rig.Now.Orders.Count);
        });

        [Fact]
        public Task RefreshOrders_WhenTheSettingsCannotBeRead_SaysSo() => Sta(async rig =>
        {
            rig.Context.Answer = Result<WorkflowContext>.Fail(new Failure(FailureKind.NotConfigured, "report.root.notSet", "No folder."));

            rig.Engine.RefreshOrders();
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Equal("report.root.notSet", rig.Now.OrdersFailure!.Code);
            Assert.Empty(rig.Now.Orders);
        });

        [Fact]
        public Task ACancelWhileTheListIsRead_GoesBackToIdle_AndThrowsAwayTheAnswer() => Sta(async rig =>
        {
            rig.Store.ListGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Engine.RefreshOrders();
            await rig.UntilAsync(() => rig.Store.ListCalls == 1);   // the read is under way, and held

            Assert.True(rig.Engine.Cancel());
            Assert.True(rig.Now.IsCancelling);
            Assert.Equal(WorkflowControls.None, rig.Now.Controls);
            Assert.False(rig.Engine.Cancel());   // a second cancel is not accepted

            rig.Store.ListGate.SetResult(true);
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.False(rig.Now.IsCancelling);
            Assert.Empty(rig.Now.Orders);
            Assert.Null(rig.Now.OrdersFailure);   // the "cancelled" answer of the store is not shown as a failure
        });

        [Fact]
        public Task ACancelWhileAnOrderIsPrepared_GoesBackToTheList_WithNoOrderAndNoFailure() => Sta(async rig =>
        {
            rig.Store.ListGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.UntilAsync(() => rig.Store.ListCalls == 1);
            Assert.Equal(WorkflowState.Starting, rig.Now.State);

            Assert.True(rig.Engine.Cancel());
            rig.Store.ListGate.SetResult(true);
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Null(rig.Now.Failure);
            Assert.Null(rig.Now.Order);
            Assert.Null(rig.Session.Current.Order);
            Assert.Empty(rig.Waiter.Timeouts);
        });

        [Fact]
        public Task ACancelFromAHandlerOfTheEventThatSaysTheOrderIsPrepared_IsNotSwallowedByWhatFollows() => Sta(async rig =>
        {
            rig.WriteReport(ReportFile.Step1);   // so that the question about earlier reports would be asked next
            var cancelled = false;
            rig.Engine.Changed += (_, s) =>
            {
                // Once: a workflow that forgot the cancel would otherwise be asked again by every event it raises.
                if (!cancelled && s.State == WorkflowState.Starting && s.Order != null)
                {
                    cancelled = true;
                    rig.Engine.Cancel();
                }
            };

            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Null(rig.Session.Current.Order);
            Assert.Empty(rig.Waiter.Timeouts);
        });

        [Fact]
        public Task AFailedListAtTheStart_AlsoDropsAListFromTheOtherMode_AndShowsTheModeItFailedIn() => Sta(async rig =>
        {
            rig.Engine.RefreshOrders();
            await rig.Settled();
            Assert.Equal(5, rig.Now.Orders.Count);

            rig.UseSettings(s => s with { IsTestMode = false });
            rig.Store.ListAnswer = Result<IReadOnlyList<OrderRecord>>.Fail(new Failure(FailureKind.Unreachable, "orders.unreachable", "No server."));
            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Equal("orders.unreachable", rig.Now.Failure!.Code);
            Assert.Empty(rig.Now.Orders);
            Assert.False(rig.Now.IsTestMode);
        });

        [Fact]
        public Task ACancelFromAHandlerOfTheEventThatShowsTheFreshList_WinsOverARefusal() => Sta(async rig =>
        {
            var cancelled = false;
            rig.Engine.Changed += (_, s) =>
            {
                if (!cancelled && s.State == WorkflowState.Starting && s.Orders.Count > 0 && s.Order == null)
                {
                    cancelled = true;
                    rig.Engine.Cancel();
                }
            };

            rig.Engine.Start("STUB-1", ReportLanguage.German);   // not in progress: it would be refused
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Null(rig.Now.Failure);
        });

        [Fact]
        public Task TheOperation_IsThereBeforeTheFirstEventOfItIsRaised() => Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.NotSupported);
            Task? seen = null;
            rig.Engine.Changed += (_, s) =>
            {
                if (s.State == WorkflowState.Completing && seen == null)
                {
                    seen = rig.Engine.CurrentOperation;
                }
            };

            rig.Engine.Finish();

            // A handler that waited for the operation at the first event waits for the upload, not for the step before.
            Assert.NotNull(seen);
            Assert.False(seen!.IsCompleted);
            await rig.Settled();
            await seen.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(WorkflowState.Completed, rig.Now.State);
        });

        [Fact]
        public Task AnOrderThatOneWindowChecks_CannotBeStartedInAnotherWindow_UntilTheFirstLetsGo() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();   // the first window holds the order
            using var other = rig.SecondWindow();

            other.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await other.Settled();

            Assert.Equal(WorkflowState.Idle, other.Now.State);
            Assert.Equal(WorkflowFailureCodes.OrderInUse, other.Now.Failure!.Code);
            Assert.Null(other.Session.Current.Order);
            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.State);

            rig.Engine.Cancel();
            rig.Engine.Back();   // the first window goes back to the list and lets go
            other.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await other.Settled();

            Assert.Equal(WorkflowState.WaitingForCard, other.Now.State);
        });

        [Fact]
        public Task AWindowThatIsClosed_LetsGoOfItsOrder() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            using var other = rig.SecondWindow();

            rig.Engine.Dispose();
            other.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await other.Settled();

            Assert.Equal(WorkflowState.WaitingForCard, other.Now.State);
        });

        [Fact]
        public Task AStartThatIsCancelledAfterTheOrderWasReserved_LetsGoOfIt() => Sta(async rig =>
        {
            rig.WriteReport(ReportFile.Step1);
            var cancelled = false;
            rig.Engine.Changed += (_, s) =>
            {
                if (!cancelled && s.State == WorkflowState.Starting && s.Order != null)
                {
                    cancelled = true;
                    rig.Engine.Cancel();
                }
            };
            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();
            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            using var other = rig.SecondWindow();

            other.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await other.Settled();

            Assert.Equal(WorkflowState.ConfirmingReplace, other.Now.State);   // not "in use"
        });

        [Fact]
        public Task AStartThatIsRefusedAfterTheOrderWasReserved_LetsGoOfIt() => Sta(async rig =>
        {
            // The session refuses a check without an identifier; the order is reserved before the session is asked.
            rig.Store.ListAnswer = Result<IReadOnlyList<OrderRecord>>.Ok(new[]
            {
                new OrderRecord { Id = " ", JobNumber = Rig.Job, ChipNumber = Rig.Chip, StatusText = nameof(OrderStatus.InProgress) },
            });
            rig.Engine.Start(" ", ReportLanguage.German);
            await rig.Settled();
            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.NotNull(rig.Now.Failure);
            using var other = rig.SecondWindow();
            rig.Store.ListAnswer = null;

            other.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await other.Settled();

            Assert.Equal(WorkflowState.WaitingForCard, other.Now.State);
        });

        [Fact]
        public void TheReservations_AreExclusive_ReleasedOnce_AndIgnoreTheCaseOfAPath()
        {
            var reservations = new OrderReservations();

            var first = reservations.TryReserve("C:\\Reports\\KP-1\\KP-1-5_final.pdf");
            Assert.NotNull(first);
            Assert.Null(reservations.TryReserve("c:\\reports\\kp-1\\kp-1-5_final.pdf"));
            Assert.NotNull(reservations.TryReserve("C:\\Reports\\KP-2\\KP-2-6_final.pdf"));

            first!.Dispose();
            var second = reservations.TryReserve("C:\\Reports\\KP-1\\KP-1-5_final.pdf");
            Assert.NotNull(second);
            first.Dispose();   // letting go twice must not release what the second holds
            Assert.Null(reservations.TryReserve("C:\\Reports\\KP-1\\KP-1-5_final.pdf"));
            Assert.Throws<ArgumentNullException>(() => reservations.TryReserve(null!));
        }

        [Fact]
        public Task AFailedRead_KeepsTheLastGoodList_ButNotOneThatCameFromTheOtherMode() => Sta(async rig =>
        {
            rig.Engine.RefreshOrders();
            await rig.Settled();
            Assert.Equal(5, rig.Now.Orders.Count);
            Assert.True(rig.Now.IsTestMode);

            // Test mode is switched off and the real store cannot be reached: the made-up orders must not stay on show.
            rig.UseSettings(s => s with { IsTestMode = false });
            rig.Store.ListAnswer = Result<IReadOnlyList<OrderRecord>>.Fail(new Failure(FailureKind.Unreachable, "orders.unreachable", "No server."));
            rig.Engine.RefreshOrders();
            await rig.Settled();

            Assert.False(rig.Now.IsTestMode);
            Assert.Empty(rig.Now.Orders);
            Assert.Equal("orders.unreachable", rig.Now.OrdersFailure!.Code);
        });

        // ── the start ───────────────────────────────────────────────────────

        [Theory]
        [InlineData("STUB-9", WorkflowFailureCodes.OrderUnknown)]
        [InlineData("STUB-1", WorkflowFailureCodes.OrderNotStartable)]
        [InlineData("STUB-4", WorkflowFailureCodes.OrderNotStartable)]
        public Task Start_RefusesAnOrderThatDoesNotExistOrIsNotInProgress(string orderId, string code) => Sta(async rig =>
        {
            Assert.True(rig.Engine.Start(orderId, ReportLanguage.German));
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Equal(code, rig.Now.Failure!.Code);
            Assert.Null(rig.Session.Current.Order);
        });

        [Fact]
        public Task Start_RefusesWhenNoCheckProjectIsSet_OrTheSettingsCannotBeRead_OrTheNumbersCannotNameAFile() => Sta(async rig =>
        {
            rig.UseSettings(s => s with { DefaultProjectPath = "  " });
            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();
            Assert.Equal(WorkflowFailureCodes.ProjectNotConfigured, rig.Now.Failure!.Code);

            rig.Context.Answer = Result<WorkflowContext>.Fail(new Failure(FailureKind.NotConfigured, "report.root.notSet", "No folder."));
            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();
            Assert.Equal("report.root.notSet", rig.Now.Failure!.Code);

            rig.UseSettings(s => s with { DefaultProjectPath = rig.ProjectPath });
            rig.Store.ListAnswer = Result<IReadOnlyList<OrderRecord>>.Ok(new[]
            {
                new OrderRecord { Id = "X-1", JobNumber = "  ", ChipNumber = "1", StatusText = nameof(OrderStatus.InProgress) },
            });
            rig.Engine.Start("X-1", ReportLanguage.German);
            await rig.Settled();
            Assert.StartsWith("report.", rig.Now.Failure!.Code, StringComparison.Ordinal);
            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Null(rig.Session.Current.Order);
        });

        [Fact]
        public Task Start_WithoutEarlierReports_BeginsTheCheck_AndWaitsForACard() => Sta(async rig =>
        {
            Assert.True(rig.Engine.Start(Rig.OrderId, ReportLanguage.English));

            // The first event already says that the order is being prepared and that something runs.
            Assert.Equal(WorkflowState.Starting, rig.Events[0].State);
            Assert.True(rig.Events[0].IsBusy);
            Assert.Equal(WorkflowControls.Cancel, rig.Events[0].Controls);

            await rig.Settled();

            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.Equal("04AABBCCDD", CardWaiter.PrimaryUid(rig.Now.Card!));
            Assert.Equal(WorkflowControls.StartCheck | WorkflowControls.Cancel, rig.Now.Controls);
            Assert.Equal(new WorkflowOrder(Rig.OrderId, Rig.Job, Rig.Chip), rig.Now.Order);
            Assert.Equal(ReportLanguage.English, rig.Now.Language);
            Assert.Equal(new WorkflowOrder(Rig.OrderId, Rig.Job, Rig.Chip), rig.Session.Current.Order);
            Assert.Equal(ReportLanguage.English, rig.Session.Current.Language);
            Assert.Equal(new TimeSpan?[] { CardCheckWorkflow.WaitCeiling }, rig.Waiter.Timeouts);
        });

        [Fact]
        public Task Start_WithEarlierReports_AsksFirst_DecliningGoesBack_ReplacingGoesOn_AndNothingIsDeletedYet() => Sta(async rig =>
        {
            rig.WriteReport(ReportFile.Step1);
            rig.WriteReport(ReportFile.Final);

            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();

            Assert.Equal(WorkflowState.ConfirmingReplace, rig.Now.State);
            Assert.False(rig.Now.IsBusy);
            Assert.Equal(new[] { ReportFile.Step1, ReportFile.Final }, rig.Now.ExistingReports);
            Assert.Equal(WorkflowControls.ConfirmReplace | WorkflowControls.DeclineReplace, rig.Now.Controls);
            Assert.Empty(rig.Waiter.Timeouts);

            Assert.True(rig.Engine.ConfirmReplace(false));
            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Null(rig.Now.Order);
            Assert.Null(rig.Session.Current.Order);

            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();
            Assert.True(rig.Engine.ConfirmReplace(true));
            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.State);
            Assert.Empty(rig.Now.ExistingReports);
            await rig.Settled();

            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.State);
            Assert.NotNull(rig.Now.Card);
            Assert.True(File.Exists(rig.Paths.Step1));
            Assert.True(File.Exists(rig.Paths.Final));
        });

        [Fact]
        public Task Start_ReadsTheListAgain_SoAStaleListCannotStartAnOrderThatIsFinished() => Sta(async rig =>
        {
            rig.Engine.RefreshOrders();
            await rig.Settled();
            Assert.True(Rig.IsStartableIn(rig.Now.Orders, Rig.OrderId));

            await rig.Stub.FinalizeAsync(Rig.OrderId, new byte[] { 1 }, CancellationToken.None);   // someone else finishes it
            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();

            Assert.Equal(WorkflowFailureCodes.OrderNotStartable, rig.Now.Failure!.Code);
            Assert.False(Rig.IsStartableIn(rig.Now.Orders, Rig.OrderId));
        });

        // ── the wait for a card ─────────────────────────────────────────────

        [Fact]
        public Task TheWait_EndsAfterTheCeiling_AsACancelledCheckWithAReason() => Sta(async rig =>
        {
            rig.Waiter.ThenFindsTheReaderEmpty();

            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.CancelledAt);
            Assert.Equal(WorkflowFailureCodes.WaitTimedOut, rig.Now.Failure!.Code);
            Assert.Equal(WorkflowControls.Back, rig.Now.Controls);
        });

        [Fact]
        public Task ACancelWhileWaitingForACard_EndsTheWait() => Sta(async rig =>
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Waiter.ThenHoldsUntil(gate, Snapshots.Card("04AABBCCDD"));
            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.UntilAsync(() => rig.Now.State == WorkflowState.WaitingForCard);
            Assert.Equal(WorkflowControls.Cancel, rig.Now.Controls);

            Assert.True(rig.Engine.Cancel());
            gate.SetResult(true);   // the look ends and sees a card whatever the token says; that answer does not count
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.CancelledAt);
            Assert.Null(rig.Now.Card);
        });

        // ── the check ───────────────────────────────────────────────────────

        [Fact]
        public Task StartCheck_LooksOnceMore_AndRunsTheCheckProjectInTheLanguageOfTheOrder() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync(ReportLanguage.English);

            Assert.True(rig.Engine.StartCheck());
            await rig.Settled();

            // While it ran, only a cancel was offered.
            Assert.Contains(rig.Events, e => e.State == WorkflowState.Checking && e.IsBusy && e.Controls == WorkflowControls.Cancel);

            var request = Assert.Single(rig.Runner.Requests);
            Assert.Equal(rig.Location, request.Location);
            Assert.Equal(Rig.Job, request.JobNumber);
            Assert.Equal(Rig.Chip, request.ChipNumber);
            Assert.Equal(rig.Settings.DefaultProjectPath, request.ProjectPath);
            Assert.Equal(ReportLanguage.English, request.ProjectLanguage);
            Assert.Equal(ReportFile.Step1, request.Report);
            Assert.Null(request.TemplateReport);
            Assert.Equal(new TimeSpan?[] { TimeSpan.Zero }, rig.Waiter.Timeouts);   // the look before the check
            Assert.Contains(rig.Events, e => e.State == WorkflowState.Checking && e.Phase == CheckPhase.Running);
            Assert.Equal(WorkflowState.Assessing, rig.Now.State);
            Assert.Null(rig.Now.Phase);
        });

        [Fact]
        public Task StartCheck_WhenTheCardWasTakenOffSince_WaitsForACardAgain_AndRunsNothing() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            rig.Waiter.ThenFindsTheReaderEmpty();
            rig.Waiter.ThenSees(Snapshots.Card("04AABBCCDD"));

            rig.Engine.StartCheck();
            await rig.Settled();

            Assert.Empty(rig.Runner.Requests);
            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.State);
            Assert.NotNull(rig.Now.Card);   // the second wait saw a card again
            Assert.Equal(new TimeSpan?[] { TimeSpan.Zero, CardCheckWorkflow.WaitCeiling }, rig.Waiter.Timeouts);
        });

        [Fact]
        public Task StartCheck_WhenAnotherCardLiesThereNow_ShowsItAndRunsNothing() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            rig.Waiter.ThenSees(Snapshots.Card("04112233EE"));

            rig.Engine.StartCheck();
            await rig.Settled();

            Assert.Empty(rig.Runner.Requests);
            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.State);
            Assert.Equal("04112233EE", CardWaiter.PrimaryUid(rig.Now.Card!));
            Assert.Equal(WorkflowFailureCodes.CardChanged, rig.Now.Failure!.Code);
            Assert.Equal(WorkflowControls.StartCheck | WorkflowControls.Cancel, rig.Now.Controls);
        });

        [Fact]
        public Task StartCheck_WhenTheReaderGivesNoClearAnswer_RunsNothing_AndKeepsTheCard() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            rig.Waiter.ThenGetsNoClearAnswer();

            rig.Engine.StartCheck();
            await rig.Settled();

            Assert.Empty(rig.Runner.Requests);
            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.State);
            Assert.Equal(WorkflowFailureCodes.CardUnclear, rig.Now.Failure!.Code);
            Assert.NotNull(rig.Now.Card);
        });

        [Theory]
        [InlineData(ChipVerdict.NotSupported, true)]
        [InlineData(ChipVerdict.SupportedOnly, true)]
        [InlineData(ChipVerdict.Indeterminate, true)]
        [InlineData(ChipVerdict.Programmable, false)]
        public Task TheVerdict_IsRecordedAndShown_AndOnlyAChipThatIsNotProgrammedCanBeFinished(ChipVerdict verdict, bool canFinish) => Sta(async rig =>
        {
            await rig.AssessedAsync(verdict);

            Assert.Equal(WorkflowState.Assessing, rig.Now.State);
            Assert.Equal(verdict, rig.Now.Selection!.Verdict);
            Assert.Equal(verdict, rig.Session.Current.Selection!.Verdict);
            Assert.NotNull(rig.Now.Findings);
            Assert.NotNull(rig.Now.Check!.Execution);
            Assert.Equal(canFinish, (rig.Now.Controls & WorkflowControls.Finish) != 0);
            Assert.True((rig.Now.Controls & WorkflowControls.Cancel) != 0);

            var accepted = rig.Engine.Finish();
            Assert.Equal(canFinish, accepted);
            if (accepted)
            {
                await rig.Settled();
                Assert.Equal(WorkflowState.Completed, rig.Now.State);
            }
        });

        [Fact]
        public Task ARunThatFails_IsRunFailed_WithTheTaskOutcomes_AndRetryWaitsAgainWithTheCardThatWasSeen() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            rig.Runner.Answer = _ => new CheckRunResult(
                CheckRunStatus.Failed,
                new Failure(FailureKind.Unexpected, CheckFailureCodes.RunFailed, "The run failed."),
                FakeResults.WithOutcomes(ExecutionOutcome.Failure, ExecutionFailureReason.Faulted, new TaskOutcome(0, "1", "CommonTaskViewModel", false, "Unknown")),
                null,
                null);

            rig.Engine.StartCheck();
            await rig.Settled();

            Assert.Equal(WorkflowState.RunFailed, rig.Now.State);
            Assert.Equal(CheckFailureCodes.RunFailed, rig.Now.Failure!.Code);
            Assert.Single(rig.Now.Check!.Execution!.TaskOutcomes);
            Assert.Null(rig.Session.Current.Findings);
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, rig.Now.Controls);

            Assert.True(rig.Engine.Retry());
            Assert.Equal(WorkflowState.WaitingForCard, rig.Now.State);
            Assert.NotNull(rig.Now.Card);
            Assert.Null(rig.Now.Failure);
            Assert.Null(rig.Now.Check);
            Assert.Single(rig.Waiter.Timeouts);   // the look before the first check only: the retry does not wait again
            Assert.Equal(WorkflowControls.StartCheck | WorkflowControls.Cancel, rig.Now.Controls);

            rig.RunnerWritesFirstReport(true, true, "1760");
            rig.Engine.StartCheck();
            await rig.Settled();
            Assert.Equal(WorkflowState.Assessing, rig.Now.State);
        });

        [Fact]
        public Task ARunTheHostCancelled_IsACancelledCheck() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            rig.Runner.Answer = _ => new CheckRunResult(CheckRunStatus.Cancelled, null, null, null, null);

            rig.Engine.StartCheck();
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.Checking, rig.Now.CancelledAt);
            Assert.Null(rig.Session.Current.Findings);
        });

        [Fact]
        public Task ACancelDuringTheRun_WinsOverARunThatFinishedAnyway() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            rig.Runner.Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Engine.StartCheck();
            await rig.UntilAsync(() => rig.Runner.Requests.Count == 1);

            Assert.True(rig.Engine.Cancel());
            Assert.True(rig.Now.IsCancelling);
            Assert.True(rig.Runner.Tokens.Single().IsCancellationRequested);
            rig.Runner.Gate.SetResult(true);   // the run was at its last task and ends well
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.Checking, rig.Now.CancelledAt);
            Assert.Null(rig.Session.Current.Findings);
            Assert.Null(rig.Now.Findings);
        });

        [Fact]
        public Task WhileSomethingRuns_EveryCommandButCancelIsRefused() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            rig.Runner.Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Engine.StartCheck();
            await rig.UntilAsync(() => rig.Runner.Requests.Count == 1);

            Assert.False(rig.Engine.RefreshOrders());
            Assert.False(rig.Engine.Start(Rig.OrderId, ReportLanguage.German));
            Assert.False(rig.Engine.ConfirmReplace(true));
            Assert.False(rig.Engine.ConfirmReplace(false));
            Assert.False(rig.Engine.StartCheck());
            Assert.False(rig.Engine.Finish());
            Assert.False(rig.Engine.Retry());
            Assert.False(rig.Engine.Back());
            Assert.Single(rig.Runner.Requests);

            rig.Runner.Gate.SetResult(true);
            await rig.Settled();
            Assert.Equal(WorkflowState.Assessing, rig.Now.State);
        });

        // ── the finish ──────────────────────────────────────────────────────

        [Fact]
        public Task Finish_MakesTheFinalReport_UploadsItsBytes_AndTheOrderIsFinishedInTheStore() => Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.NotSupported);
            var finalPath = Path.Combine(rig.Location.OutputRoot, Rig.Job, Rig.Job + "-" + Rig.Chip + "_final.pdf");
            Assert.False(File.Exists(finalPath));

            Assert.True(rig.Engine.Finish());
            Assert.Equal(WorkflowState.Completing, rig.Now.State);
            Assert.True(rig.Now.IsBusy);
            Assert.Equal(WorkflowControls.None, rig.Now.Controls);   // no safe boundary: nothing, not even a cancel
            Assert.False(rig.Engine.Cancel());
            await rig.Settled();

            Assert.Equal(WorkflowState.Completed, rig.Now.State);
            Assert.True(File.Exists(finalPath));
            Assert.Equal(finalPath, rig.Paths.Final);
            var uploaded = Assert.Single(rig.Store.Finalized);
            Assert.Equal(Rig.OrderId, uploaded.Id);
            Assert.Equal(File.ReadAllBytes(finalPath), uploaded.Report);
            var order = (await rig.Stub.ListAsync(CancellationToken.None)).Value.Single(o => o.Id == Rig.OrderId);
            Assert.Equal(OrderStatus.CheckFinished, order.Status);
            Assert.NotNull(rig.Now.Cleanup);
            Assert.False(rig.Now.CleanupFailed);
            Assert.Equal(WorkflowControls.Back, rig.Now.Controls);
        });

        [Theory]
        [InlineData(ChipVerdict.NotSupported, true, false)]
        [InlineData(ChipVerdict.NotSupported, false, true)]
        [InlineData(ChipVerdict.SupportedOnly, true, true)]
        [InlineData(ChipVerdict.Indeterminate, true, true)]
        public async Task Finish_RemovesTheWorkingReportsOnlyWhenTheVerdictAndTheSettingSayTo(ChipVerdict verdict, bool removeTemporary, bool firstReportRemains)
        {
            await Sta(async rig =>
            {
                await rig.AssessedAsync(verdict);

                rig.Engine.Finish();
                await rig.Settled();

                Assert.Equal(WorkflowState.Completed, rig.Now.State);
                Assert.Equal(firstReportRemains, File.Exists(rig.Paths.Step1));
                Assert.True(File.Exists(rig.Paths.Final));
            }, removeTemporary);
        }

        [Fact]
        public Task Finish_AnUploadThatFails_IsCompletionFailed_NothingIsCleaned_AndRetryCanSucceed() => Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.NotSupported);
            rig.Store.FinalizeAnswer = Result.Fail(new Failure(FailureKind.Unreachable, "orders.unreachable", "No server."));

            rig.Engine.Finish();
            await rig.Settled();

            Assert.Equal(WorkflowState.CompletionFailed, rig.Now.State);
            Assert.Equal("orders.unreachable", rig.Now.Failure!.Code);
            Assert.True(File.Exists(rig.Paths.Final));   // stays on disk for the next try
            Assert.True(File.Exists(rig.Paths.Step1));   // the source of the retry: the setting says to remove it, but only after an upload
            Assert.Null(rig.Now.Cleanup);
            Assert.Equal(WorkflowControls.Retry | WorkflowControls.Cancel, rig.Now.Controls);

            rig.Store.FinalizeAnswer = null;
            Assert.True(rig.Engine.Retry());
            Assert.Equal(WorkflowState.Completing, rig.Now.State);
            Assert.Null(rig.Now.Failure);
            await rig.Settled();

            Assert.Equal(WorkflowState.Completed, rig.Now.State);
            Assert.Equal(2, rig.Store.Finalized.Count);
            Assert.Equal(File.ReadAllBytes(rig.Paths.Final), rig.Store.Finalized[1].Report);   // the report is made again for the retry
            Assert.False(File.Exists(rig.Paths.Step1));   // removed now that the order is finished
            Assert.NotNull(rig.Now.Cleanup);
        }, true);

        [Fact]
        public Task Finish_AFinishedReportThatCannotBeRead_IsCompletionFailed_AndNothingIsUploaded() => Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.NotSupported);

            rig.Engine.Finish();
            await rig.Settled();

            Assert.Equal(WorkflowState.CompletionFailed, rig.Now.State);
            Assert.Equal(WorkflowFailureCodes.FinalUnreadable, rig.Now.Failure!.Code);
            Assert.Empty(rig.Store.Finalized);
        }, false, null, _ => new NoFileReportService());

        [Fact]
        public Task ClosingTheWindowDuringTheUpload_DoesNotInterruptIt_AndRaisesNothing() => Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.NotSupported);
            rig.Store.FinalizeGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Engine.Finish();
            await rig.UntilAsync(() => rig.Store.Finalized.Count == 1);
            var before = rig.Events.Count;

            rig.Engine.Dispose();
            rig.Store.FinalizeGate.SetResult(true);
            await rig.Settled();

            Assert.Equal(before, rig.Events.Count);
            var order = (await rig.Stub.ListAsync(CancellationToken.None)).Value.Single(o => o.Id == Rig.OrderId);
            Assert.Equal(OrderStatus.CheckFinished, order.Status);   // the upload went through
            Assert.True(File.Exists(rig.Paths.Final));
        });

        [Fact]
        public Task Finish_AReportThatCannotBeLocked_IsCompletionFailed_AndNothingIsUploaded() => Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.NotSupported);
            File.WriteAllText(rig.Paths.Step1, "this is not a PDF");

            rig.Engine.Finish();
            await rig.Settled();

            Assert.Equal(WorkflowState.CompletionFailed, rig.Now.State);
            Assert.StartsWith("report.", rig.Now.Failure!.Code, StringComparison.Ordinal);
            Assert.Empty(rig.Store.Finalized);
        });

        [Fact]
        public Task Finish_ACleanupThatThrows_DoesNotUndoAFinishedOrder_AndIsLoggedByTypeOnly() => Sta(async rig =>
        {
            await rig.AssessedAsync(ChipVerdict.NotSupported);

            rig.Engine.Finish();
            await rig.Settled();

            Assert.Equal(WorkflowState.Completed, rig.Now.State);
            Assert.True(rig.Now.CleanupFailed);
            Assert.Single(rig.Store.Finalized);
            Assert.Contains("IOException", rig.Log.Everything, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", rig.Log.Everything, StringComparison.Ordinal);
        }, false, files => new ThrowingCleanupFiles(files));

        // ── cancel, back, dispose, faults ───────────────────────────────────

        [Theory]
        [InlineData("Assessing")]
        [InlineData("RunFailed")]
        [InlineData("CompletionFailed")]
        [InlineData("WaitingForCard")]
        public Task ACancel_WhereNothingRuns_CancelsAtOnce_AndBackGoesToTheListWithoutTheOrder(string stateName) => Sta(async rig =>
        {
            // The state is passed by name: the test class is public, the enumeration is not.
            var state = Enum.Parse<WorkflowState>(stateName);
            await rig.ReachAsync(state);

            Assert.True(rig.Engine.Cancel());
            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(state, rig.Now.CancelledAt);
            Assert.Equal(WorkflowControls.Back, rig.Now.Controls);

            Assert.True(rig.Engine.Back());
            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Null(rig.Now.Order);
            Assert.Null(rig.Now.Card);
            Assert.Null(rig.Now.Check);
            Assert.Null(rig.Now.CancelledAt);
            Assert.Null(rig.Session.Current.Order);
            Assert.Equal(5, rig.Now.Orders.Count);   // the list stays
            Assert.Equal(WorkflowControls.Refresh | WorkflowControls.Start, rig.Now.Controls);
        });

        [Fact]
        public Task ACancel_AfterAFailedUpload_KeepsTheFailure_SoThePageCanSayWhatToCheck() => Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.CompletionFailed);

            Assert.True(rig.Engine.Cancel());

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.CompletionFailed, rig.Now.CancelledAt);
            Assert.Equal("orders.unreachable", rig.Now.Failure!.Code);
        });

        [Fact]
        public Task ACancel_AfterAFailedRun_ClearsTheFailure() => Sta(async rig =>
        {
            await rig.ReachAsync(WorkflowState.RunFailed);
            Assert.NotNull(rig.Now.Failure);

            Assert.True(rig.Engine.Cancel());

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Null(rig.Now.Failure);
        });

        [Fact]
        public Task AListRead_ThatThrows_IsAFailedRead_NotAFault_AndTheNextReadWorks() => Sta(async rig =>
        {
            Assert.True(rig.Engine.RefreshOrders());
            await rig.Settled();
            Assert.Equal(5, rig.Now.Orders.Count);
            rig.Store.ListThrows = new InvalidOperationException("secret path of the share");

            Assert.True(rig.Engine.RefreshOrders());
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Equal(ContextFailureCodes.OrdersUnexpected, rig.Now.OrdersFailure!.Code);
            Assert.Empty(rig.Now.Orders);
            Assert.DoesNotContain("secret", rig.Now.OrdersFailure.SafeMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", rig.Log.Everything, StringComparison.Ordinal);

            rig.Store.ListThrows = null;
            Assert.True(rig.Engine.RefreshOrders());
            await rig.Settled();

            Assert.Null(rig.Now.OrdersFailure);
            Assert.Equal(5, rig.Now.Orders.Count);
        });

        [Fact]
        public Task Dispose_StopsTheRunningStep_AndNothingIsRaisedAfterwards() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            rig.Runner.Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Engine.StartCheck();
            await rig.UntilAsync(() => rig.Runner.Requests.Count == 1);
            var before = rig.Events.Count;
            var version = rig.Now.Version;

            rig.Engine.Dispose();
            rig.Engine.Dispose();   // twice is harmless
            Assert.True(rig.Runner.Tokens.Single().IsCancellationRequested);
            Assert.False(rig.Engine.Cancel());
            rig.Runner.Gate.SetResult(true);
            await rig.Settled();

            Assert.Equal(before, rig.Events.Count);
            Assert.Equal(version, rig.Now.Version);   // nothing changes any more once the window is gone
            Assert.Null(rig.Session.Current.Findings);
        });

        [Fact]
        public Task AnUnexpectedException_Faults_IsLoggedByTypeOnly_AndBackWorks() => Sta(async rig =>
        {
            await rig.WaitingForCardAsync();
            rig.Runner.Throw = new FormatException("secret text of the host");

            rig.Engine.StartCheck();
            await rig.Settled();

            Assert.Equal(WorkflowState.Faulted, rig.Now.State);
            Assert.Equal(WorkflowFailureCodes.Faulted, rig.Now.Failure!.Code);
            Assert.Contains("FormatException", rig.Now.Failure.SafeMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", rig.Now.Failure.SafeMessage, StringComparison.Ordinal);
            Assert.Contains("FormatException", rig.Log.Everything, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", rig.Log.Everything, StringComparison.Ordinal);
            Assert.False(rig.Now.IsBusy);

            Assert.True(rig.Engine.Back());
            Assert.Equal(WorkflowState.Idle, rig.Now.State);
        });

        [Fact]
        public Task AMoveTheTableDoesNotAllow_FaultsTheWorkflow_AndIsLogged() => Sta(rig =>
        {
            rig.Engine.MoveForTest(WorkflowState.Completed);

            Assert.Equal(WorkflowState.Faulted, rig.Now.State);
            Assert.Contains("is not allowed", rig.Log.Everything, StringComparison.Ordinal);

            return Task.CompletedTask;
        });

        // ── the events ──────────────────────────────────────────────────────

        [Fact]
        public Task TheEvents_ComeOneAtATimeInTheOrderOfTheVersions_AlsoWhenAHandlerCallsACommand() => Sta(async rig =>
        {
            var seen = new List<long>();
            var depth = 0;
            var deepest = 0;

            // The handler that calls a command is first and the recorder second: with a nested delivery the recorder would
            // see the newer version before the older one.
            rig.Engine.Changed += (_, snapshot) =>
            {
                depth++;
                deepest = Math.Max(deepest, depth);
                if (snapshot.State == WorkflowState.Assessing)
                {
                    rig.Engine.Cancel();
                }

                depth--;
            };
            rig.Engine.Changed += (_, snapshot) => seen.Add(snapshot.Version);

            rig.Verdict(ChipVerdict.NotSupported);
            await rig.WaitingForCardAsync();
            rig.Engine.StartCheck();
            await rig.Settled();

            Assert.Equal(WorkflowState.Cancelled, rig.Now.State);
            Assert.Equal(WorkflowState.Assessing, rig.Now.CancelledAt);
            Assert.Equal(1, deepest);
            Assert.Equal(Enumerable.Range(1, seen.Count).Select(i => (long)i), seen);
            Assert.Equal(rig.Now.Version, seen.Last());
        }, false, null, null, false);

        [Fact]
        public Task AHandlerThatAdvancesTheWorkflow_OnTheEventThatEndedAStep_StartsTheNextStepInsteadOfFaultingTheOldOne() => Sta(async rig =>
        {
            // The page auto-advances: as soon as the check is assessed it finishes the order.
            rig.Engine.Changed += (_, s) =>
            {
                if (s.State == WorkflowState.Assessing && (s.Controls & WorkflowControls.Finish) != 0)
                {
                    rig.Engine.Finish();
                }
            };
            rig.Verdict(ChipVerdict.NotSupported);
            await rig.WaitingForCardAsync();

            rig.Engine.StartCheck();
            await rig.Settled();

            Assert.Equal(WorkflowState.Completed, rig.Now.State);
            Assert.DoesNotContain(rig.Events, e => e.State == WorkflowState.Faulted);
            Assert.DoesNotContain("ended without a result", rig.Log.Everything, StringComparison.Ordinal);
            Assert.Single(rig.Store.Finalized);
        });

        [Fact]
        public Task AHandlerThatStartsTheCheck_OnTheEventThatSaysTheCardWasSeen_RunsItOnce() => Sta(async rig =>
        {
            rig.Engine.Changed += (_, s) =>
            {
                if (s.State == WorkflowState.WaitingForCard && (s.Controls & WorkflowControls.StartCheck) != 0)
                {
                    rig.Engine.StartCheck();
                }
            };

            rig.Engine.Start(Rig.OrderId, ReportLanguage.German);
            await rig.Settled();

            Assert.Equal(WorkflowState.Assessing, rig.Now.State);
            Assert.Single(rig.Runner.Requests);
            Assert.DoesNotContain(rig.Events, e => e.State == WorkflowState.Faulted);
        });

        [Fact]
        public Task AHandlerThatRefreshesTheList_OnTheEventOfARefusedStart_DoesNotFaultTheWorkflow() => Sta(async rig =>
        {
            rig.Engine.Changed += (_, s) =>
            {
                if (s.State == WorkflowState.Idle && s.Failure?.Code == WorkflowFailureCodes.OrderNotStartable)
                {
                    rig.Engine.RefreshOrders();
                }
            };

            rig.Engine.Start("STUB-1", ReportLanguage.German);
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Equal(5, rig.Now.Orders.Count);
            Assert.DoesNotContain(rig.Events, e => e.State == WorkflowState.Faulted);
        });

        [Fact]
        public Task AHandlerThatClosesTheWindow_StopsTheDeliveryToTheOthersAndOfWhatIsQueued() => Sta(async rig =>
        {
            var later = new List<WorkflowState>();
            rig.Engine.Changed += (_, s) =>
            {
                if (s.State == WorkflowState.Completed)
                {
                    rig.Engine.Back();      // queues the next snapshot
                    rig.Engine.Dispose();   // and the window closes
                }
            };
            rig.Engine.Changed += (_, s) => later.Add(s.State);
            await rig.AssessedAsync(ChipVerdict.NotSupported);

            rig.Engine.Finish();
            await rig.Settled();

            Assert.DoesNotContain(WorkflowState.Completed, later);   // the rest of that very event is dropped
            Assert.DoesNotContain(WorkflowState.Idle, later);         // and so is what was queued
        }, false, null, null, false);

        [Fact]
        public Task AHandlerThatThrows_DoesNotStopTheWorkflowOrTheHandlersAfterIt() => Sta(async rig =>
        {
            var versions = new List<long>();
            rig.Engine.Changed += (_, _) => throw new InvalidOperationException("a page bug");
            rig.Engine.Changed += (_, s) => versions.Add(s.Version);

            rig.Engine.RefreshOrders();
            await rig.Settled();

            Assert.Equal(WorkflowState.Idle, rig.Now.State);
            Assert.Equal(5, rig.Now.Orders.Count);
            Assert.Equal(new long[] { 1, 2 }, versions);
            Assert.Contains("InvalidOperationException", rig.Log.Everything, StringComparison.Ordinal);
        }, false, null, null, false);

        [Fact]
        public void Arguments_AreChecked()
        {
            using var area = new TempArea();
            var log = new RecordingCardCheckLog();
            var context = new FakeContextSource();
            var session = CardCheckServices.CreateWorkflowSession(log);
            var files = CardCheckServices.CreateReportFiles(log);
            var reports = CardCheckServices.CreateReportService(log);
            var runner = new FakeCheckRunner();
            var waiter = new FakeCardWaiter();
            var reservations = new OrderReservations();

            Assert.Throws<ArgumentNullException>(() => new CardCheckWorkflow(null!, session, files, reports, runner, waiter, reservations, log));
            Assert.Throws<ArgumentNullException>(() => new CardCheckWorkflow(context, null!, files, reports, runner, waiter, reservations, log));
            Assert.Throws<ArgumentNullException>(() => new CardCheckWorkflow(context, session, null!, reports, runner, waiter, reservations, log));
            Assert.Throws<ArgumentNullException>(() => new CardCheckWorkflow(context, session, files, null!, runner, waiter, reservations, log));
            Assert.Throws<ArgumentNullException>(() => new CardCheckWorkflow(context, session, files, reports, null!, waiter, reservations, log));
            Assert.Throws<ArgumentNullException>(() => new CardCheckWorkflow(context, session, files, reports, runner, null!, reservations, log));
            Assert.Throws<ArgumentNullException>(() => new CardCheckWorkflow(context, session, files, reports, runner, waiter, null!, log));
            Assert.Throws<ArgumentNullException>(() => new CardCheckWorkflow(context, session, files, reports, runner, waiter, reservations, null!));
            using var workflow = new CardCheckWorkflow(context, session, files, reports, runner, waiter, reservations, log);
            Assert.Throws<ArgumentNullException>(() => workflow.Start(null!, ReportLanguage.German));
            Assert.Throws<ArgumentNullException>(() => CardCheckWorkflow.IsStartable(null!));
        }

        // ── the rig ─────────────────────────────────────────────────────────

        // The workflow is for one context: each test runs on a thread with a dispatcher, as the window's thread does.
        internal static Task Sta(
            Func<Rig, Task> body,
            bool removeTemporary = false,
            Func<IReportFiles, IReportFiles>? wrapFiles = null,
            Func<IReportService, IReportService>? wrapReports = null,
            bool record = true) =>
            StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var rig = new Rig(wrapFiles, removeTemporary, wrapReports, record);

                // A workflow that does not do what a test expects must fail the test, not hold up the whole run.
                await body(rig).WaitAsync(TimeSpan.FromSeconds(60));
            });

        internal sealed class Rig : IDisposable
        {
            public const string OrderId = "STUB-3";
            public const string Job = "STUB-1003";
            public const string Chip = "3";

            private readonly TempArea _area = new TempArea();
            private WorkflowSettings _settings;

            public Rig(Func<IReportFiles, IReportFiles>? wrapFiles, bool removeTemporary, Func<IReportService, IReportService>? wrapReports, bool record)
            {
                Log = new RecordingCardCheckLog();
                Stub = CardCheckServices.CreateStubOrderStore(Log);
                Store = new ScriptedOrderStore(Stub);
                Files = CardCheckServices.CreateReportFiles(Log);
                Session = CardCheckServices.CreateWorkflowSession(Log);
                Location = new ReportLocation(_area.Directory, true);
                _settings = new WorkflowSettings(Location, removeTemporary, ReportLanguage.German, _area.ProjectPath, true);
                UseSettings(s => s);
                var reports = CardCheckServices.CreateReportService(Log);
                Engine = new CardCheckWorkflow(Context, Session, wrapFiles == null ? Files : wrapFiles(Files), wrapReports == null ? reports : wrapReports(reports), Runner, Waiter, Reservations, Log);
                if (record)
                {
                    Engine.Changed += (_, snapshot) => Events.Add(snapshot);
                }

                RunnerWritesFirstReport(true, true, "1760");
            }

            public RecordingCardCheckLog Log { get; }

            public IOrderStore Stub { get; }

            public ScriptedOrderStore Store { get; }

            public IReportFiles Files { get; }

            public IWorkflowSession Session { get; }

            public ReportLocation Location { get; }

            public FakeContextSource Context { get; } = new FakeContextSource();

            public FakeCheckRunner Runner { get; } = new FakeCheckRunner();

            public FakeCardWaiter Waiter { get; } = new FakeCardWaiter();

            public OrderReservations Reservations { get; } = new OrderReservations();

            public CardCheckWorkflow Engine { get; }

            public List<WorkflowSnapshot> Events { get; } = new List<WorkflowSnapshot>();

            public WorkflowSnapshot Now => Engine.Current;

            public WorkflowSettings Settings => _settings;

            public string ProjectPath => _area.ProjectPath;

            public OrderReportPaths Paths => Files.Resolve(Location, Job, Chip).Value;

            public static bool IsStartableIn(IReadOnlyList<OrderRecord> orders, string id) =>
                CardCheckWorkflow.IsStartable(orders.Single(o => o.Id == id));

            public void Dispose()
            {
                Engine.Dispose();
                _area.Dispose();
            }

            public void UseSettings(Func<WorkflowSettings, WorkflowSettings> change)
            {
                _settings = change(_settings);
                Context.Answer = Result<WorkflowContext>.Ok(new WorkflowContext(Store, _settings));
            }

            public Task Settled() => SettledAsync(Engine);

            // Waits for the operation, and for the one a handler started on its last event, and so on.
            private static async Task SettledAsync(CardCheckWorkflow engine)
            {
                Task last;
                do
                {
                    last = engine.CurrentOperation;
                    await last.WaitAsync(TimeSpan.FromSeconds(30));
                }
                while (!ReferenceEquals(last, engine.CurrentOperation));
            }

            // A second window of the assistant: its own session and workflow, the same store, files, runner and reservations.
            public Window SecondWindow()
            {
                var session = CardCheckServices.CreateWorkflowSession(Log);
                var engine = new CardCheckWorkflow(Context, session, Files, CardCheckServices.CreateReportService(Log), Runner, Waiter, Reservations, Log);

                return new Window(engine, session);
            }

            public sealed class Window : IDisposable
            {
                public Window(CardCheckWorkflow engine, IWorkflowSession session)
                {
                    Engine = engine;
                    Session = session;
                }

                public CardCheckWorkflow Engine { get; }

                public IWorkflowSession Session { get; }

                public WorkflowSnapshot Now => Engine.Current;

                public Task Settled() => SettledAsync(Engine);

                public void Dispose() => Engine.Dispose();
            }

            // Lets the dispatcher run until a condition holds (a held step is waiting on a gate of the test).
            public async Task UntilAsync(Func<bool> condition)
            {
                for (var i = 0; i < 2000 && !condition(); i++)
                {
                    await Task.Delay(5);
                }

                Assert.True(condition(), "The condition was not reached.");
            }

            public void WriteReport(ReportFile file)
            {
                Assert.True(Files.EnsureOrderFolder(Paths).IsSuccess);
                ReportFixtures.WriteFirstReport(Paths.PathOf(file));
            }

            public async Task WaitingForCardAsync(ReportLanguage language = ReportLanguage.German)
            {
                Assert.True(Engine.Start(OrderId, language));
                await Settled();
                Assert.Equal(WorkflowState.WaitingForCard, Now.State);
                Waiter.Timeouts.Clear();
            }

            public async Task AssessedAsync(ChipVerdict verdict)
            {
                Verdict(verdict);
                await WaitingForCardAsync();
                Assert.True(Engine.StartCheck());
                await Settled();
                Assert.Equal(WorkflowState.Assessing, Now.State);
            }

            // Brings the workflow to a state in which nothing runs.
            public async Task ReachAsync(WorkflowState state)
            {
                switch (state)
                {
                    case WorkflowState.WaitingForCard:
                        await WaitingForCardAsync();
                        break;
                    case WorkflowState.Assessing:
                        await AssessedAsync(ChipVerdict.NotSupported);
                        break;
                    case WorkflowState.RunFailed:
                        await WaitingForCardAsync();
                        Runner.Answer = _ => new CheckRunResult(CheckRunStatus.Failed, new Failure(FailureKind.Unexpected, CheckFailureCodes.RunFailed, "The run failed."), null, null, null);
                        Engine.StartCheck();
                        await Settled();
                        break;
                    case WorkflowState.CompletionFailed:
                        await AssessedAsync(ChipVerdict.NotSupported);
                        Store.FinalizeAnswer = Result.Fail(new Failure(FailureKind.Unreachable, "orders.unreachable", "No server."));
                        Engine.Finish();
                        await Settled();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state), state, "Not a state this test can reach.");
                }

                Assert.Equal(state, Now.State);
                Assert.False(Now.IsBusy);
            }

            // The check project writes a first report with the flags a verdict needs, and the runner answers with what it says.
            public void Verdict(ChipVerdict kind)
            {
                switch (kind)
                {
                    case ChipVerdict.NotSupported:
                        RunnerWritesFirstReport(false, false, "N/A");
                        break;
                    case ChipVerdict.SupportedOnly:
                        RunnerWritesFirstReport(true, true, "100");
                        break;
                    case ChipVerdict.Indeterminate:
                        RunnerWritesFirstReport(false, true, "1760");
                        break;
                    default:
                        RunnerWritesFirstReport(true, true, "1760");
                        break;
                }
            }

            public void RunnerWritesFirstReport(bool supported, bool programmable, string memory)
            {
                Runner.Answer = request =>
                {
                    var paths = Files.Resolve(request.Location, request.JobNumber, request.ChipNumber).Value;
                    Files.EnsureOrderFolder(paths);
                    var reportPath = paths.PathOf(request.Report);
                    ReportFixtures.WriteFirstReport(reportPath, supported ? "Yes" : "No", programmable ? "Yes" : "No", memory);

                    return new CheckRunResult(
                        CheckRunStatus.Finished,
                        null,
                        FakeResults.Success(2),
                        new ReportAnalysis { Supported = supported, Programmable = programmable, FreeMemoryText = memory, ChipType = "MIFARE DESFire EV2" },
                        reportPath);
                };
            }
        }
    }
}
