using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;
using Page = RFiDGear.Tests.CardCheckAssistant.GuidedPage;

namespace RFiDGear.Tests.CardCheckAssistant
{
    public sealed class GuidedCheckViewModelTests
    {
        private static OrderRecord Order(string id, OrderStatus status = OrderStatus.InProgress) => new OrderRecord
        {
            Id = id,
            JobNumber = "J" + id,
            ChipNumber = "1",
            CustomerName = "Customer " + id,
            StatusText = status.ToString(),
        };

        private static Task Run(
            Func<Page, Task> body,
            WorkflowState state = WorkflowState.Idle,
            bool settings = true,
            string culture = "en-US",
            long firstVersion = 1) =>
            Page.Run(body, state, settings, culture, firstVersion);

        // The mapping is written out here on purpose: a test that asks the code under test what it maps proves nothing.
        private static readonly Dictionary<WorkflowState, GuidedStep> ExpectedSteps = new Dictionary<WorkflowState, GuidedStep>
        {
            [WorkflowState.Idle] = GuidedStep.Orders,
            [WorkflowState.LoadingOrders] = GuidedStep.Orders,
            [WorkflowState.Starting] = GuidedStep.Prepare,
            [WorkflowState.ConfirmingReplace] = GuidedStep.Replace,
            [WorkflowState.WaitingForCard] = GuidedStep.Card,
            [WorkflowState.Checking] = GuidedStep.Check,
            [WorkflowState.RunFailed] = GuidedStep.Check,
            [WorkflowState.Assessing] = GuidedStep.Verdict,
            [WorkflowState.Programming] = GuidedStep.Program,
            [WorkflowState.WritingReport] = GuidedStep.Program,
            [WorkflowState.ProgrammingFailed] = GuidedStep.Program,
            [WorkflowState.ReadingOut] = GuidedStep.Check,
            [WorkflowState.ReadOutFailed] = GuidedStep.Check,
            [WorkflowState.Reviewing] = GuidedStep.Review,
            [WorkflowState.Completing] = GuidedStep.Finish,
            [WorkflowState.CompletionFailed] = GuidedStep.Finish,
            [WorkflowState.Completed] = GuidedStep.End,
            [WorkflowState.Cancelled] = GuidedStep.End,
            [WorkflowState.Faulted] = GuidedStep.End,
        };

        private static Func<WorkflowSnapshot, WorkflowSnapshot> WithOrders(params OrderRecord[] orders) => s => s with { Orders = orders };

        [Fact]
        public Task EveryState_IsShownByExactlyOneStep() => Run(async page =>
        {
            Assert.Equal(WorkflowTables.States.Count, ExpectedSteps.Count);
            foreach (var state in WorkflowTables.States)
            {
                await page.PushAsync(state);
                var vm = page.Vm;

                Assert.Equal(ExpectedSteps[state], vm.Step);
                Assert.Equal(ExpectedSteps[state], GuidedSteps.Of(state));
                Assert.Single(
                    new[] { vm.IsOrdersStep, vm.IsPrepareStep, vm.IsReplaceStep, vm.IsCardStep, vm.IsCheckStep, vm.IsVerdictStep, vm.IsProgramStep, vm.IsReviewStep, vm.IsFinishStep, vm.IsEndStep },
                    shown => shown);
            }
        });

        [Fact]
        public Task Commands_FollowTheControls_AndStartNeedsAStartableSelection() => Run(async page =>
        {
            var vm = page.Vm;
            var changed = 0;
            vm.StartOrderCommand.CanExecuteChanged += (_, _) => changed++;
            await page.PushAsync(WorkflowState.Idle, WithOrders(Order("A"), Order("B", OrderStatus.Created)));

            Assert.True(vm.RefreshCommand.CanExecute(null));
            Assert.False(vm.StartOrderCommand.CanExecute(null));
            vm.SelectedOrder = vm.Orders[1];
            Assert.False(vm.StartOrderCommand.CanExecute(null));
            Assert.NotEmpty(vm.SelectedOrderHint);

            var before = changed;
            vm.SelectedOrder = vm.Orders[0];
            Assert.True(changed > before, "Choosing a row has to tell the command.");
            Assert.True(vm.StartOrderCommand.CanExecute(null));
            Assert.Empty(vm.SelectedOrderHint);

            vm.SelectedLanguage = vm.Languages[1];
            vm.StartOrderCommand.Execute(null);
            Assert.Equal(new[] { "Start:A:English" }, page.Workflow.Calls);

            Assert.False(vm.CancelCommand.CanExecute(null));
            vm.CancelCommand.Execute(null);
            Assert.DoesNotContain("Cancel", page.Workflow.Calls);

            page.Workflow.Accept = false;
            vm.RefreshCommand.Execute(null);
            Assert.Contains("Refresh", page.Workflow.Calls);

            await page.PushAsync(WorkflowState.Starting, busy: true);
            Assert.False(vm.RefreshCommand.CanExecute(null));
            Assert.True(vm.CancelCommand.CanExecute(null));
        });

        [Fact]
        public Task TheFirstSnapshotIsShown_OlderAndEqualOnesAreIgnored_AndNothingIsShownAfterDispose() => Run(async page =>
        {
            var vm = page.Vm;
            Assert.True(vm.RefreshCommand.CanExecute(null));

            page.Workflow.Raise(page.Workflow.Make(WorkflowState.WaitingForCard) with { Version = 5 });
            await ViewModelTestSupport.Flush();
            Assert.Equal(GuidedStep.Card, vm.Step);

            page.Workflow.Raise(page.Workflow.Make(WorkflowState.Assessing) with { Version = 3 });
            page.Workflow.Raise(page.Workflow.Make(WorkflowState.Assessing) with { Version = 5 });
            await ViewModelTestSupport.Flush();
            Assert.Equal(GuidedStep.Card, vm.Step);

            vm.Dispose();
            page.Workflow.Raise(page.Workflow.Make(WorkflowState.Assessing) with { Version = 9 });
            await ViewModelTestSupport.Flush();
            Assert.Equal(GuidedStep.Card, vm.Step);
        }, firstVersion: 0);

        [Fact]
        public Task ASnapshotRaisedOnAnotherThread_IsShownOnTheWindowsThread() => Run(async page =>
        {
            var uiThread = Environment.CurrentManagedThreadId;
            var raisedOn = new List<int>();
            page.Vm.PropertyChanged += (_, _) => raisedOn.Add(Environment.CurrentManagedThreadId);

            await Task.Run(() => page.Workflow.Push(WorkflowState.WaitingForCard));
            await ViewModelTestSupport.Flush();

            Assert.Equal(GuidedStep.Card, page.Vm.Step);
            Assert.NotEmpty(raisedOn);
            Assert.All(raisedOn, id => Assert.Equal(uiThread, id));
        });

        [Fact]
        public Task HostEventsFromOtherThreads_ChangeThePageOnlyOnItsOwnThread_AndTheStateIsReadWhenTheEventIsHandled() => Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.WaitingForCard);
            var uiThread = Environment.CurrentManagedThreadId;
            var raisedOn = new System.Collections.Concurrent.ConcurrentBag<int>();
            vm.PropertyChanged += (_, _) => raisedOn.Add(Environment.CurrentManagedThreadId);
            vm.Strings.PropertyChanged += (_, _) => raisedOn.Add(Environment.CurrentManagedThreadId);

            await Task.Run(() =>
            {
                page.Host.ExecutionHost.SetRunning(true);
                page.Host.ReaderHost.RaiseReaderStateChanged();
                page.Host.Context.ChangeCulture("de-DE");
            });
            await ViewModelTestSupport.Flush();

            Assert.NotEmpty(raisedOn);
            Assert.All(raisedOn, id => Assert.Equal(uiThread, id));
            Assert.Equal(vm.Strings[Keys.GuidedPromptHostRunning], vm.PromptText);

            // The event says that the state changed, not what it is: the page reads it when it handles the event.
            page.Host.ExecutionHost.SetRunning(true);
            page.Host.ExecutionHost.SetRunningQuietly(false);
            await ViewModelTestSupport.Flush();

            Assert.Equal(vm.Strings[Keys.GuidedPromptPlaceChip], vm.PromptText);
        });

        [Fact]
        public Task ABindingThatFails_DoesNotEscapeIntoTheApplication() => Run(async page =>
        {
            var reachedTheDispatcher = false;
            Dispatcher.CurrentDispatcher.UnhandledException += (_, e) =>
            {
                reachedTheDispatcher = true;
                e.Handled = true;
            };
            page.Vm.PropertyChanged += (_, _) => throw new InvalidOperationException("a binding that fails");

            await page.PushAsync(WorkflowState.WaitingForCard);

            Assert.Equal(GuidedStep.Card, page.Vm.Step);
            Assert.False(reachedTheDispatcher, "The page has to keep an exception of a binding to itself.");
        });

        [Fact]
        public Task TheSelection_IsKeptById_WhenTheRowsAreReplaced_AndAListThatDidNotChangeIsLeftAlone() => Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.Idle, WithOrders(Order("A"), Order("B")));
            vm.SelectedOrder = vm.Orders[1];

            // A list control writes null into the selection when the rows go.
            vm.Orders.CollectionChanged += (_, _) => vm.SelectedOrder = null;
            await page.PushAsync(WorkflowState.Idle, WithOrders(Order("A"), Order("B"), Order("C")));

            Assert.Equal("B", vm.SelectedOrder?.Id);
            Assert.Contains(vm.SelectedOrder, vm.Orders);
            Assert.Equal(3, vm.Orders.Count);

            var rows = vm.Orders.ToList();
            await page.PushAsync(WorkflowState.Idle, WithOrders(Order("A"), Order("B"), Order("C")));

            Assert.True(rows.SequenceEqual(vm.Orders), "A list that did not change keeps its rows.");
            Assert.Equal("B", vm.SelectedOrder?.Id);
        });

        [Fact]
        public Task TheEmptyListText_TellsLoadingFromNoOrdersFromNoneInProgress() => Run(async page =>
        {
            var vm = page.Vm;
            Assert.True(vm.HasOrdersEmptyText);
            Assert.Equal(vm.Strings[Keys.GuidedOrdersLoading], vm.OrdersEmptyText);

            await page.PushAsync(WorkflowState.LoadingOrders, busy: true);
            await page.PushAsync(WorkflowState.Idle);
            Assert.Equal(vm.Strings[Keys.GuidedOrdersNone], vm.OrdersEmptyText);

            await page.PushAsync(WorkflowState.Idle, WithOrders(Order("A", OrderStatus.Created)));
            Assert.Equal(vm.Strings[Keys.GuidedOrdersNoneInProgress], vm.OrdersEmptyText);

            await page.PushAsync(WorkflowState.Idle, WithOrders(Order("A", OrderStatus.Created), Order("B")));
            Assert.False(vm.HasOrdersEmptyText);
        });

        [Fact]
        public Task TheEmptyListText_IsNotFooledByACancelledRead_OrByAReadOfTheTimer() => Run(async page =>
        {
            var vm = page.Vm;
            await vm.Open();

            // The first read is cancelled: the list ends as it was, which is no proof that there are no orders.
            await page.PushAsync(WorkflowState.LoadingOrders, busy: true);
            vm.CancelCommand.Execute(null);
            await page.PushAsync(WorkflowState.Idle);
            Assert.Equal(vm.Strings[Keys.GuidedOrdersLoading], vm.OrdersEmptyText);

            // A read that finished: there are none. And the timer's next read does not unsay it.
            await page.PushAsync(WorkflowState.LoadingOrders, busy: true);
            await page.PushAsync(WorkflowState.Idle);
            Assert.Equal(vm.Strings[Keys.GuidedOrdersNone], vm.OrdersEmptyText);

            page.Timer.Tick!();
            await ViewModelTestSupport.Flush();
            await page.PushAsync(WorkflowState.LoadingOrders, busy: true);
            Assert.True(vm.IsUpdatingOrders);
            Assert.Equal(vm.Strings[Keys.GuidedOrdersNone], vm.OrdersEmptyText);

            // One the operator started is a read like the first.
            await page.PushAsync(WorkflowState.Idle);
            vm.RefreshCommand.Execute(null);
            await page.PushAsync(WorkflowState.LoadingOrders, busy: true);
            Assert.Equal(vm.Strings[Keys.GuidedOrdersLoading], vm.OrdersEmptyText);
        });

        [Fact]
        public Task CancelAtTheFootOfThePage_LeavesTheQuestionAboutEarlierReports_LikeItsOwnButton() => Run(async page =>
        {
            var vm = page.Vm;

            await page.PushAsync(WorkflowState.ConfirmingReplace);
            Assert.True(vm.ShowCancel);
            Assert.True(vm.CancelCommand.CanExecute(null));
            vm.CancelCommand.Execute(null);

            Assert.Equal(new[] { "ConfirmReplace:False" }, page.Workflow.Calls);
        });

        [Fact]
        public Task EachCommand_CallsItsOwnEngineCommand() => Run(async page =>
        {
            var vm = page.Vm;

            await page.PushAsync(WorkflowState.ConfirmingReplace);
            Assert.True(vm.ConfirmReplaceCommand.CanExecute(null));
            Assert.True(vm.KeepReportsCommand.CanExecute(null));
            vm.ConfirmReplaceCommand.Execute(null);
            vm.KeepReportsCommand.Execute(null);

            await page.PushAsync(WorkflowState.WaitingForCard, s => s with { Card = Snapshots.Card("04AABBCC") });
            vm.StartCheckCommand.Execute(null);
            vm.CancelCommand.Execute(null);

            await page.PushAsync(WorkflowState.Assessing);
            vm.FinishCommand.Execute(null);

            await page.PushAsync(WorkflowState.RunFailed);
            vm.RetryCommand.Execute(null);

            await page.PushAsync(WorkflowState.CompletionFailed);
            vm.RetryCommand.Execute(null);

            Assert.Equal(
                new[] { "ConfirmReplace:True", "ConfirmReplace:False", "StartCheck", "Cancel", "Finish", "Retry", "Retry" },
                page.Workflow.Calls);

            // A button that the state does not offer does nothing.
            await page.PushAsync(WorkflowState.Assessing, s => s with { Controls = WorkflowControls.Cancel });
            Assert.False(vm.FinishCommand.CanExecute(null));
            vm.FinishCommand.Execute(null);
            Assert.Equal(7, page.Workflow.Calls.Count);
        });

        [Fact]
        public Task TheMessages_ComeFromTheListInTheList_AndFromTheStepElsewhere() => Run(async page =>
        {
            var vm = page.Vm;
            var unreachable = new Failure(FailureKind.Unreachable, "orders.unreachable", "No server.");
            var runFailed = new Failure(FailureKind.Unexpected, CheckFailureCodes.RunFailed, "The run failed.");

            await page.PushAsync(WorkflowState.Idle, s => s with { OrdersFailure = unreachable });
            var message = Assert.Single(vm.Messages);
            Assert.Equal("orders.unreachable", message.Code);
            Assert.Equal(vm.Strings[Keys.FailOrdersUnreachable], message.Text);
            Assert.True(vm.HasMessages);
            Assert.False(vm.HasOrdersEmptyText);

            await page.PushAsync(WorkflowState.RunFailed, s => s with { Failure = runFailed });
            Assert.Equal(CheckFailureCodes.RunFailed, Assert.Single(vm.Messages).Code);

            // The reason a list could not be read belongs to the list.
            await page.PushAsync(WorkflowState.RunFailed, s => s with { OrdersFailure = unreachable });
            Assert.Empty(vm.Messages);
            Assert.False(vm.HasMessages);

            // A cancel after a failed upload keeps its reason, so the operator knows what to check.
            await page.PushAsync(WorkflowState.Cancelled, s => s with { CancelledAt = WorkflowState.CompletionFailed, Failure = unreachable });
            Assert.Equal("orders.unreachable", Assert.Single(vm.Messages).Code);

            // The same reason twice is told once.
            await page.PushAsync(WorkflowState.Idle, s => s with { Failure = unreachable, OrdersFailure = unreachable });
            Assert.Single(vm.Messages);
        });

        [Fact]
        public void ThePrompt_PutsAMissingReaderFirst_ThenBusyRFiDGear_ThenTheChip_AndABusyScanIsNotAMissingReader()
        {
            var strings = new LocalizedStrings("en-US");
            var seen = new WorkflowSnapshot { Card = Snapshots.Card("04AABBCC") };
            var nothing = new WorkflowSnapshot();

            Assert.Equal(strings[Keys.GuidedPromptNoReader], GuidedTexts.Prompt(seen, Snapshots.NoReader(), true, strings));
            Assert.Equal(strings[Keys.GuidedPromptHostRunning], GuidedTexts.Prompt(seen, Snapshots.NoCard(), true, strings));
            Assert.Contains("04AABBCC", GuidedTexts.Prompt(seen, Snapshots.NoCard(), false, strings), StringComparison.Ordinal);
            Assert.Equal(strings[Keys.GuidedPromptPlaceChip], GuidedTexts.Prompt(nothing, Snapshots.NoCard(), false, strings));
            Assert.Equal(strings[Keys.GuidedPromptPlaceChip], GuidedTexts.Prompt(nothing, Snapshots.Busy(), false, strings));
            Assert.Equal(strings[Keys.GuidedPromptPlaceChip], GuidedTexts.Prompt(nothing, Snapshots.ReadFailed(), false, strings));
            Assert.Equal(strings[Keys.GuidedPromptPlaceChip], GuidedTexts.Prompt(nothing, null, false, strings));
        }

        [Fact]
        public Task ARefusedStart_LeavesANotice_ThatNoReadSwallows_AndThatTheOperatorsNextActionClears() => Run(async page =>
        {
            var vm = page.Vm;
            var refused = new Failure(FailureKind.NotFound, WorkflowFailureCodes.OrderNotStartable, "The order is not in progress.");
            var list = WithOrders(Order("A"));
            await page.PushAsync(WorkflowState.Idle, list);
            vm.Orders.CollectionChanged += (_, _) => vm.SelectedOrder = null;   // a list control writes null when the rows go
            vm.SelectedOrder = vm.Orders[0];

            vm.StartOrderCommand.Execute(null);
            await page.PushAsync(WorkflowState.Starting, list, busy: true);
            // The relist inside Start found the order changed: the rows are replaced in the same snapshot as the refusal.
            var changed = WithOrders(Order("A", OrderStatus.Created));
            await page.PushAsync(WorkflowState.Idle, s => changed(s) with { Failure = refused });
            Assert.Equal(WorkflowFailureCodes.OrderNotStartable, Assert.Single(vm.Messages).Code);
            Assert.Equal(vm.Strings[Keys.FailOrderNotStartable], vm.Messages[0].Text);
            Assert.Equal("A", vm.SelectedOrder?.Id);

            // The timer starts a read: the engine's own failure is gone, the notice stays.
            await page.PushAsync(WorkflowState.LoadingOrders, busy: true);
            await page.PushAsync(WorkflowState.Idle, list);
            Assert.Single(vm.Messages);

            vm.RefreshCommand.Execute(null);
            Assert.Empty(vm.Messages);

            // And a notice does not outlive the list.
            await page.PushAsync(WorkflowState.Idle, s => list(s) with { Failure = refused });
            Assert.Single(vm.Messages);
            await page.PushAsync(WorkflowState.WaitingForCard);
            await page.PushAsync(WorkflowState.Idle);
            Assert.Empty(vm.Messages);
        });

        [Fact]
        public Task Back_GoesToTheListAtOnce_AndLetsGoOfTheSelection() => Run(
            async page =>
            {
                var vm = page.Vm;
                await page.PushAsync(WorkflowState.Completed, WithOrders(Order("A")));
                vm.SelectedOrder = vm.Orders[0];
                Assert.True(vm.BackCommand.CanExecute(null));

                vm.BackCommand.Execute(null);

                Assert.Equal(new[] { "Back", "Refresh" }, page.Workflow.Calls);
                Assert.Null(vm.SelectedOrder);
            },
            state: WorkflowState.Completed);

        [Fact]
        public Task Open_OffersFirst_ThenReads_ThenStartsTheTimer_AndOnlyOnce() => Run(async page =>
        {
            var seen = string.Empty;
            page.Offer.WhileOffering = () => seen = page.Timer.Starts + "/" + page.Workflow.Calls.Count;

            await page.Vm.Open();
            await page.Vm.Open();

            Assert.Equal("0/0", seen);
            Assert.Equal(1, page.Offer.Calls);
            Assert.Equal(new[] { "Refresh" }, page.Workflow.Calls);
            Assert.Equal(1, page.Timer.Starts);
            Assert.Equal(TimeSpan.FromSeconds(10), page.Timer.Interval);
        });

        [Fact]
        public Task Open_AnOfferThatFails_DoesNotStopThePage() => Run(async page =>
        {
            page.Offer.Throws = new InvalidOperationException("the offer fails");

            await page.Vm.Open();

            Assert.Equal(new[] { "Refresh" }, page.Workflow.Calls);
            Assert.Equal(1, page.Timer.Starts);
        });

        [Fact]
        public Task TheTimer_ReadsTheListOnlyInsideItsGate() => Run(async page =>
        {
            var vm = page.Vm;
            await vm.Open();
            await page.PushAsync(WorkflowState.Idle, WithOrders(Order("A")));

            async Task<bool> TickReads()
            {
                var before = page.Workflow.Calls.Count;
                page.Timer.Tick!();
                await ViewModelTestSupport.Flush();

                return page.Workflow.Calls.Count > before;
            }

            Assert.True(await TickReads(), "Nothing is selected and the last read worked.");

            vm.SelectedOrder = vm.Orders[0];
            Assert.False(await TickReads(), "An order is selected.");
            vm.SelectedOrder = null;

            await page.PushAsync(WorkflowState.Idle, s => s with { OrdersFailure = new Failure(FailureKind.Unreachable, "orders.unreachable", "No server.") });
            Assert.False(await TickReads(), "The last read failed; only a manual refresh tries again.");
            await page.PushAsync(WorkflowState.Idle);

            await page.PushAsync(WorkflowState.LoadingOrders, busy: true);
            Assert.False(await TickReads(), "A read is going on.");
            await page.PushAsync(WorkflowState.Idle, busy: true);
            Assert.False(await TickReads(), "Something is running.");
            await page.PushAsync(WorkflowState.WaitingForCard);
            Assert.False(await TickReads(), "A check is under way.");
            await page.PushAsync(WorkflowState.Idle);

            page.Settings.WhileOpen = () =>
            {
                page.Timer.Tick!();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
            };
            var beforeModal = page.Workflow.Calls.Count;
            vm.OpenSettingsCommand.Execute(null);
            Assert.Equal(1, page.Settings.Shows);
            Assert.Equal(beforeModal, page.Workflow.Calls.Count);

            Assert.True(await TickReads(), "The gate opens again.");

            vm.Dispose();
            Assert.False(await TickReads(), "A disposed page reads nothing.");
            Assert.True(page.Timer.Disposed);
        });

        [Fact]
        public Task AReadStartedByTheTimer_ShowsOnlyAnUpdatingLine_NotACancelButton() => Run(async page =>
        {
            var vm = page.Vm;
            await vm.Open();

            page.Timer.Tick!();
            await ViewModelTestSupport.Flush();
            await page.PushAsync(WorkflowState.LoadingOrders, busy: true);

            Assert.True(vm.IsUpdatingOrders);
            Assert.False(vm.ShowCancel);

            await page.PushAsync(WorkflowState.Idle);
            vm.RefreshCommand.Execute(null);
            await page.PushAsync(WorkflowState.LoadingOrders, busy: true);

            Assert.False(vm.IsUpdatingOrders);
            Assert.True(vm.ShowCancel);
        });

        [Fact]
        public Task TheReportLanguage_FollowsTheSetting_UntilTheOperatorChoosesOne() => Run(async page =>
        {
            var vm = page.Vm;
            Assert.Equal(ReportLanguage.German, vm.SelectedLanguage.Language);

            await page.PushAsync(WorkflowState.Idle, s => s with { DefaultLanguage = ReportLanguage.English });
            Assert.Equal(ReportLanguage.English, vm.SelectedLanguage.Language);

            vm.SelectedLanguage = vm.Languages[0];
            await page.PushAsync(WorkflowState.Idle, s => s with { DefaultLanguage = ReportLanguage.English });
            Assert.Equal(ReportLanguage.German, vm.SelectedLanguage.Language);
        });

        [Fact]
        public Task TheCheckStep_NamesThePhase_AndTheHintAfterAFailedRun() => Run(async page =>
        {
            var vm = page.Vm;
            var phases = new Dictionary<CheckPhase, string>
            {
                [CheckPhase.Preparing] = Keys.GuidedPreparing,
                [CheckPhase.LoadingProject] = Keys.GuidedPhaseLoading,
                [CheckPhase.Running] = Keys.GuidedPhaseRunning,
                [CheckPhase.ReadingReport] = Keys.GuidedPhaseReading,
            };

            foreach (var pair in phases)
            {
                await page.PushAsync(WorkflowState.Checking, s => s with { Phase = pair.Key }, busy: true);
                Assert.Equal(vm.Strings[pair.Value], vm.CheckText);
            }

            await page.PushAsync(WorkflowState.RunFailed);
            Assert.Equal(vm.Strings[Keys.GuidedRunFailedHint], vm.CheckText);
        });

        [Fact]
        public Task TheVerdictStep_ShowsTheVerdict_AndWhatTheReportSays() => Run(async page =>
        {
            var vm = page.Vm;
            var verdicts = new[]
            {
                (ChipVerdict.NotSupported, Keys.GuidedVerdictNotSupported),
                (ChipVerdict.SupportedOnly, Keys.GuidedVerdictSupportedOnly),
                (ChipVerdict.Programmable, Keys.GuidedVerdictProgrammable),
                (ChipVerdict.Indeterminate, Keys.GuidedVerdictIndeterminate),
            };

            foreach (var (verdict, key) in verdicts)
            {
                var selection = new TemplateSelection(verdict, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null);
                await page.PushAsync(WorkflowState.Assessing, s => s with { Selection = selection });

                Assert.Equal(vm.Strings[key], vm.VerdictText);
                Assert.Equal(verdict == ChipVerdict.Indeterminate, vm.IsIndeterminate);
                Assert.Equal(verdict == ChipVerdict.Programmable, vm.IsProgrammable);
            }

            var findings = new ReportAnalysis { ChipType = "DESFire EV1", FreeMemoryText = "1760", Notes = "  " };
            var check = new CheckRunResult(CheckRunStatus.Finished, null, null, findings, "first-report.pdf");
            await page.PushAsync(WorkflowState.Assessing, s => s with { Findings = findings, Check = check });

            Assert.Equal(vm.Strings.Format(Keys.GuidedChipTypeFormat, "DESFire EV1"), vm.ChipTypeText);
            Assert.Equal(vm.Strings.Format(Keys.GuidedFreeMemoryFormat, "1760"), vm.FreeMemoryText);
            Assert.Equal(string.Empty, vm.NotesText);
            Assert.Equal(vm.Strings.Format(Keys.GuidedReportPathFormat, "first-report.pdf"), vm.ReportPathText);
        });

        [Fact]
        public Task TheTaskRows_FollowEachResult_AlsoWhenTheCountIsTheSame_AndStayWhenTheResultDoesNot() => Run(async page =>
        {
            var vm = page.Vm;
            var now = DateTimeOffset.UtcNow;
            ExecResult Result(string type) => new ExecResult(
                "run", ExecutionOutcome.Success, 1, 0, now, now, new[] { new TaskOutcome(0, "1", type, true, "NoError") });
            CheckRunResult Check(ExecResult result) => new CheckRunResult(CheckRunStatus.Finished, null, result, null, null);

            var first = Result("Classic");
            await page.PushAsync(WorkflowState.Assessing, s => s with { Check = Check(first) });
            var rows = vm.Rows.ToList();
            Assert.Equal("Classic", Assert.Single(vm.Rows).TaskType);

            // The same result in the next snapshot: nothing is rebuilt.
            await page.PushAsync(WorkflowState.Assessing, s => s with { Check = Check(first) });
            Assert.True(rows.SequenceEqual(vm.Rows));

            // Another run with as many tasks: the rows are its own.
            await page.PushAsync(WorkflowState.Assessing, s => s with { Check = Check(Result("Desfire")) });
            Assert.Equal("Desfire", Assert.Single(vm.Rows).TaskType);

            await page.PushAsync(WorkflowState.Idle);
            Assert.Empty(vm.Rows);
            Assert.False(vm.HasRows);
        });

        [Fact]
        public Task EveryKindOfTaskRow_IsShownInItsOwnWords_AndTheTextsFollowTheLanguage() => Run(async page =>
        {
            var vm = page.Vm;
            var now = DateTimeOffset.UtcNow;
            var outcomes = new[]
            {
                new TaskOutcome(0, "1", "Classic", true, "NoError"),
                new TaskOutcome(1, "2", "Desfire", false, "AuthFailure"),
                new TaskOutcome(2, "3", "Ultralight", null, "Empty"),
                new TaskOutcome(3, "4", "A", null, "Empty", TaskSkipReasons.NotReached),
                new TaskOutcome(4, "5", "B", null, "Empty", TaskSkipReasons.ExecuteConditionNotMet),
                new TaskOutcome(5, "6", "C", null, "Empty", TaskSkipReasons.NotExecuted),
                new TaskOutcome(6, "7", "D", null, "Empty", TaskSkipReasons.TaskModelUnavailable),
                new TaskOutcome(7, "8", "E", null, "Empty", "a reason this version does not know"),
            };
            var result = new ExecResult("run", ExecutionOutcome.Failure, 8, 1, now, now, outcomes);
            await page.PushAsync(WorkflowState.Assessing, s => s with { Check = new CheckRunResult(CheckRunStatus.Finished, null, result, null, null) });

            var expected = new[]
            {
                (TaskRowKind.Success, Keys.RowOk),
                (TaskRowKind.Failure, Keys.RowFailed),
                (TaskRowKind.Unknown, Keys.RowUnknown),
                (TaskRowKind.Skipped, Keys.RowNotReached),
                (TaskRowKind.Skipped, Keys.RowConditionNotMet),
                (TaskRowKind.Skipped, Keys.RowNotExecuted),
                (TaskRowKind.Skipped, Keys.RowTaskUnavailable),
            };
            Assert.Equal(8, vm.Rows.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].Item1, vm.Rows[i].Kind);
                Assert.Equal(vm.Strings[expected[i].Item2], vm.Rows[i].ResultText);
                Assert.Equal(i + 1, vm.Rows[i].Position);
            }

            Assert.Equal("a reason this version does not know", vm.Rows[7].ResultText);   // shown as it is, not hidden
            Assert.Equal("2", vm.Rows[1].TaskIndex);
            Assert.Equal("AuthFailure", vm.Rows[1].ErrorCode);
            Assert.Equal(vm.Strings.Format(Keys.SummaryFormat, 1, 8), vm.SummaryText);

            var changed = new List<string?>();
            vm.Rows[1].PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            var before = vm.Rows[1].ResultText;
            page.Host.Context.ChangeCulture("de-DE");
            await ViewModelTestSupport.Flush();

            Assert.Contains("ResultText", changed);
            Assert.NotEqual(before, vm.Rows[1].ResultText);   // "Failed" becomes German; "OK" is the same in both
        });

        [Fact]
        public Task TheReplaceStep_NamesTheReportsThatExist() => Run(async page =>
        {
            var vm = page.Vm;

            await page.PushAsync(WorkflowState.ConfirmingReplace, s => s with { ExistingReports = new[] { ReportFile.Step1, ReportFile.Final } });

            Assert.Equal(
                vm.Strings.Format(Keys.GuidedReplaceFormat, vm.Strings[Keys.GuidedFileStep1] + ", " + vm.Strings[Keys.GuidedFileFinal]),
                vm.ReplaceText);
        });

        [Fact]
        public void TheChipOnTheReader_IsDescribedByTypeAndUid_AndTheOthersAreCounted()
        {
            var strings = new LocalizedStrings("en-US");
            var one = Snapshots.Card("04AABBCC");
            var two = new ReaderSnapshot(
                Snapshots.Reader,
                ReaderScanState.Ready,
                new[] { new CredentialInfo("04AA", 1, "DESFireEV1_8K", true), new CredentialInfo("0102", 2, "EM4102", false) });

            Assert.Equal(strings.Format(Keys.CardFormat, "DESFireEV1", "04AABBCC"), GuidedTexts.Card(one, strings));
            Assert.Equal(strings.Format(Keys.CardMoreFormat, strings.Format(Keys.CardFormat, "DESFireEV1_8K", "04AA"), 1), GuidedTexts.Card(two, strings));
            Assert.Equal(string.Empty, GuidedTexts.Card(Snapshots.NoCard(), strings));
            Assert.Equal(string.Empty, GuidedTexts.Card(null, strings));
        }

        [Fact]
        public Task Settings_AreOnlyOfferedInTheList_NotTwice_AndASavedDialogReadsTheListAgain() => Run(async page =>
        {
            var vm = page.Vm;
            Assert.True(vm.HasSettings);
            Assert.True(vm.OpenSettingsCommand.CanExecute(null));

            // Not saved: nothing is read again. And not a second window while the first is open.
            var whileOpen = new List<bool>();
            page.Settings.WhileOpen = () => whileOpen.Add(vm.OpenSettingsCommand.CanExecute(null));
            page.Settings.Answer = false;
            await vm.OpenSettingsCommand.ExecuteAsync();
            Assert.Empty(page.Workflow.Calls);
            Assert.Equal(new[] { false }, whileOpen);
            Assert.True(vm.OpenSettingsCommand.CanExecute(null));

            // Saved: the list is read again.
            page.Settings.WhileOpen = null;
            page.Settings.Answer = true;
            await vm.OpenSettingsCommand.ExecuteAsync();
            Assert.Equal(new[] { "Refresh" }, page.Workflow.Calls);

            // The page is closed while the window is open: nothing is read afterwards.
            page.Workflow.Calls.Clear();
            page.Settings.WhileOpen = vm.Dispose;
            await vm.OpenSettingsCommand.ExecuteAsync();
            Assert.Empty(page.Workflow.Calls);
        });

        [Fact]
        public Task Settings_AreNotOfferedOutsideTheList() => Run(async page =>
        {
            await page.PushAsync(WorkflowState.WaitingForCard);
            Assert.False(page.Vm.OpenSettingsCommand.CanExecute(null));
        });

        [Fact]
        public Task Settings_WithoutADialog_AreNotOffered() => Run(
            page =>
            {
                Assert.False(page.Vm.HasSettings);
                Assert.False(page.Vm.OpenSettingsCommand.CanExecute(null));

                return Task.CompletedTask;
            },
            settings: false);

        [Fact]
        public Task TheCardStep_AsksForTheChip_UnlessThereIsNoReader_OrRFiDGearIsBusy() => Run(async page =>
        {
            var vm = page.Vm;
            await page.PushAsync(WorkflowState.WaitingForCard);
            Assert.Equal(vm.Strings[Keys.GuidedPromptPlaceChip], vm.PromptText);

            page.Host.ReaderHost.Show(Snapshots.NoReader());
            await ViewModelTestSupport.Flush();
            Assert.Equal(vm.Strings[Keys.GuidedPromptNoReader], vm.PromptText);

            page.Host.ReaderHost.Show(Snapshots.NoCard());
            page.Host.ExecutionHost.SetRunning(true);
            await ViewModelTestSupport.Flush();
            Assert.Equal(vm.Strings[Keys.GuidedPromptHostRunning], vm.PromptText);

            page.Host.ExecutionHost.SetRunning(false);
            await ViewModelTestSupport.Flush();
            await page.PushAsync(WorkflowState.WaitingForCard, s => s with { Card = Snapshots.Card("04AABBCC") });
            Assert.Contains("04AABBCC", vm.PromptText, StringComparison.Ordinal);
        });

        [Fact]
        public Task Close_IsAllowed_Refused_OrAskedAbout_AccordingToTheWorkflow() => Run(async page =>
        {
            var vm = page.Vm;
            Assert.True(vm.TryRequestClose());
            Assert.Empty(page.Close.Asked);

            await page.PushAsync(WorkflowState.Starting, busy: true);
            Assert.False(vm.TryRequestClose());
            Assert.True(vm.ShowCloseHint);
            Assert.Empty(page.Close.Asked);

            await page.PushAsync(WorkflowState.WaitingForCard);
            Assert.False(vm.ShowCloseHint);
            page.Close.Answer = false;
            Assert.False(vm.TryRequestClose());
            page.Close.Answer = true;
            Assert.True(vm.TryRequestClose());
            Assert.Equal(vm.Strings[Keys.GuidedCloseNothing], page.Close.Asked[1].Text);
            Assert.Equal(vm.Strings[Keys.GuidedCloseTitle], page.Close.Asked[1].Title);

            await page.PushAsync(WorkflowState.Assessing);
            Assert.True(vm.TryRequestClose());
            Assert.Equal(vm.Strings[Keys.GuidedCloseVerdict], page.Close.Asked[2].Text);

            await page.PushAsync(WorkflowState.CompletionFailed);
            Assert.True(vm.TryRequestClose());
            Assert.Equal(vm.Strings[Keys.GuidedCloseUpload], page.Close.Asked[3].Text);

            // From the programming on, closing says that the chip may be programmed all the same; while the report is written
            // or the memory read, closing is refused like during the check.
            var next = 4;
            foreach (var state in new[] { WorkflowState.Programming, WorkflowState.ProgrammingFailed, WorkflowState.ReadOutFailed, WorkflowState.Reviewing })
            {
                await page.PushAsync(state);
                Assert.True(vm.TryRequestClose());
                Assert.Equal(vm.Strings[Keys.GuidedCloseProgrammed], page.Close.Asked[next++].Text);
            }

            foreach (var state in new[] { WorkflowState.WritingReport, WorkflowState.ReadingOut })
            {
                await page.PushAsync(state, busy: true);
                Assert.False(vm.TryRequestClose());
            }

            Assert.Equal(next, page.Close.Asked.Count);

            // The workflow decides, not the picture the page shows: a change that was not applied yet counts.
            await page.PushAsync(WorkflowState.Idle);
            page.Close.WhileAsking = null;
            var asked = page.Close.Asked.Count;
            page.Workflow.Push(WorkflowState.Starting, busy: true);
            Assert.False(vm.TryRequestClose());
            Assert.True(vm.ShowCloseHint);
            Assert.Equal(asked, page.Close.Asked.Count);
            await ViewModelTestSupport.Flush();
            page.Workflow.Push(WorkflowState.Idle);
            Assert.True(vm.TryRequestClose());
            await ViewModelTestSupport.Flush();
            await page.PushAsync(WorkflowState.CompletionFailed);
            page.Close.Answer = true;

            // The question is never asked twice at once.
            var nested = true;
            page.Close.WhileAsking = () => nested = vm.TryRequestClose();
            Assert.True(vm.TryRequestClose());
            Assert.False(nested);
            Assert.Equal(asked + 1, page.Close.Asked.Count);
        });

        [Fact]
        public Task Dispose_IsIdempotent_StopsEverything_AndDropsWhatWasPostedBefore() => Run(async page =>
        {
            var vm = page.Vm;
            await vm.Open();
            Assert.Equal(3, page.Host.PageSubscriberCount);

            var raised = 0;
            vm.PropertyChanged += (_, _) => raised++;
            page.Workflow.Push(WorkflowState.WaitingForCard);
            page.Host.ReaderHost.RaiseReaderStateChanged();
            page.Host.Context.ChangeCulture("de-DE");
            vm.Dispose();
            vm.Dispose();
            await ViewModelTestSupport.Flush();

            Assert.Equal(0, raised);

            Assert.True(vm.IsDisposed);
            Assert.Equal(GuidedStep.Orders, vm.Step);
            Assert.Equal(0, page.Host.PageSubscriberCount);
            Assert.Equal(0, page.Workflow.ChangedSubscribers);
            Assert.Equal(1, page.Workflow.DisposeCount);
            Assert.True(page.Timer.Disposed);
            Assert.True(page.Offer.SeenToken.IsCancellationRequested);
            Assert.True(vm.TryRequestClose());

            page.Host.Context.ChangeCulture("de-DE");
            page.Host.ReaderHost.RaiseReaderStateChanged();
            await ViewModelTestSupport.Flush();
        });

        [Fact]
        public Task ACultureChange_RefreshesEveryText() => Run(async page =>
        {
            var vm = page.Vm;
            var everythingChanged = false;
            vm.PropertyChanged += (_, e) => everythingChanged |= e.PropertyName == string.Empty;
            Assert.Equal(ExtensionStrings.Get(Keys.GuidedPromptPlaceChip, "en-US"), vm.Strings[Keys.GuidedPromptPlaceChip]);

            page.Host.Context.ChangeCulture("de-DE");
            await ViewModelTestSupport.Flush();

            Assert.True(everythingChanged);
            Assert.Equal(ExtensionStrings.Get(Keys.GuidedPromptPlaceChip, "de-DE"), vm.Strings[Keys.GuidedPromptPlaceChip]);
            Assert.Equal(ExtensionStrings.Get(Keys.LanguageGerman, "de-DE"), vm.Languages[0].Name);
        });

        [Fact]
        public void TheVerdictAndTheEnd_AreToldInWords_ThatDependOnWhereTheCheckStood()
        {
            var strings = new LocalizedStrings("en-US");
            var programmable = new TemplateSelection(ChipVerdict.Programmable, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null);
            var withReport = new CheckRunResult(CheckRunStatus.Finished, null, null, null, "C:\\reports\\first.pdf");

            Assert.Equal(strings[Keys.GuidedVerdictProgrammable], GuidedTexts.Verdict(programmable, false, strings));
            Assert.Equal(strings[Keys.GuidedVerdictProgrammableBlocked], GuidedTexts.Verdict(programmable, true, strings));
            Assert.Equal(string.Empty, GuidedTexts.Verdict(null, false, strings));

            string End(WorkflowState state, Func<WorkflowSnapshot, WorkflowSnapshot>? edit = null) =>
                GuidedTexts.End((edit == null ? new WorkflowSnapshot() : edit(new WorkflowSnapshot())) with { State = state }, strings);

            Assert.Equal(strings[Keys.GuidedEndCompleted], End(WorkflowState.Completed));
            Assert.EndsWith(strings[Keys.GuidedEndCleanupFailed], End(WorkflowState.Completed, s => s with { CleanupFailed = true }), StringComparison.Ordinal);
            Assert.Equal(strings[Keys.GuidedEndFaulted], End(WorkflowState.Faulted));
            Assert.Equal(strings[Keys.GuidedEndCancelledNothing], End(WorkflowState.Cancelled, s => s with { CancelledAt = WorkflowState.WaitingForCard }));
            Assert.Equal(strings[Keys.GuidedEndCancelledMaybe], End(WorkflowState.Cancelled, s => s with { CancelledAt = WorkflowState.CompletionFailed, Check = withReport }));

            // After the upload of a programmed chip failed, a new check is not safe: it would replace the reports.
            Assert.Equal(
                strings[Keys.GuidedEndCancelledMaybeProgrammed],
                End(WorkflowState.Cancelled, s => s with { CancelledAt = WorkflowState.CompletionFailed, Check = withReport, Selection = programmable }));
            Assert.Contains("first.pdf", End(WorkflowState.Cancelled, s => s with { CancelledAt = WorkflowState.Assessing, Check = withReport }), StringComparison.Ordinal);
            Assert.Equal(
                strings.Format(Keys.GuidedEndCancelledProgrammableFormat, "C:\\reports\\first.pdf"),
                End(WorkflowState.Cancelled, s => s with { CancelledAt = WorkflowState.Assessing, Check = withReport, Selection = programmable }));

            // After a cancel while the chip was being programmed, the end text does not say that nothing was changed.
            foreach (var at in new[] { WorkflowState.Programming, WorkflowState.WritingReport, WorkflowState.ProgrammingFailed, WorkflowState.ReadingOut, WorkflowState.ReadOutFailed, WorkflowState.Reviewing })
            {
                Assert.StartsWith(
                    strings[Keys.GuidedEndCancelledProgrammed],
                    End(WorkflowState.Cancelled, s => s with { CancelledAt = at, Check = withReport, Selection = programmable }),
                    StringComparison.Ordinal);
            }
            Assert.Equal(string.Empty, End(WorkflowState.Idle));
        }
    }
}
