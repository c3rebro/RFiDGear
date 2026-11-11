using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The page over the real engine (with the fakes of the engine tests behind it): the whole path a page takes.</summary>
    public sealed class GuidedCheckSmokeTests
    {
        private static Task Smoke(Func<WorkflowEngineTests.Rig, GuidedCheckViewModel, Task> body) =>
            Smoke((rig, vm, _) => body(rig, vm));

        private static Task Smoke(Func<WorkflowEngineTests.Rig, GuidedCheckViewModel, FakePageServices, Task> body) =>
            WorkflowEngineTests.Sta(async rig =>
            {
                var services = new FakePageServices();
                using var vm = new GuidedCheckViewModel(
                    new FakeExtensionHost(),
                    Dispatcher.CurrentDispatcher,
                    rig.Engine,
                    null,
                    new StubImportOffer(),
                    services,
                    new ManualTimer().Start);

                await body(rig, vm, services);
            });

        private static async Task ShownAsync(WorkflowEngineTests.Rig rig)
        {
            await rig.Settled();
            await ViewModelTestSupport.Flush();
        }

        private static async Task ChooseTheTestOrderAsync(WorkflowEngineTests.Rig rig, GuidedCheckViewModel vm)
        {
            vm.RefreshCommand.Execute(null);
            await ShownAsync(rig);
            Assert.Equal(5, vm.Orders.Count);
            vm.SelectedOrder = vm.Orders.Single(row => row.Id == WorkflowEngineTests.Rig.OrderId);
        }

        [Fact]
        public Task TheWholeCheck_InTestMode_ReachesCompleted_ThroughThePage() => Smoke(async (rig, vm) =>
        {
            rig.Verdict(ChipVerdict.NotSupported);
            await ChooseTheTestOrderAsync(rig, vm);
            Assert.True(vm.IsTestMode);

            vm.StartOrderCommand.Execute(null);
            await ShownAsync(rig);
            Assert.True(vm.IsCardStep);
            Assert.True(vm.StartCheckCommand.CanExecute(null));

            vm.StartCheckCommand.Execute(null);
            await ShownAsync(rig);
            Assert.True(vm.IsVerdictStep);
            Assert.Equal(vm.Strings[Keys.GuidedVerdictNotSupported], vm.VerdictText);
            Assert.True(vm.FinishCommand.CanExecute(null));

            vm.FinishCommand.Execute(null);
            await ShownAsync(rig);
            Assert.True(vm.IsEndStep);
            Assert.True(vm.IsCompleted);
            Assert.Equal(vm.Strings[Keys.GuidedEndCompleted], vm.EndText);
        });

        [Fact]
        public Task TheReview_ShowsTheReadOutReport_AndReportCheckedFinishesTheOrder() => Smoke(async (rig, vm) =>
        {
            await rig.ReachAsync(WorkflowState.Reviewing);
            await ViewModelTestSupport.Flush();

            Assert.True(vm.IsReviewStep);
            Assert.Contains(rig.Paths.PathOf(ReportFile.Step3), vm.ReadOutPathText, StringComparison.Ordinal);
            Assert.Empty(vm.ReviewWarnings);
            Assert.True(vm.FinishCommand.CanExecute(null));

            vm.FinishCommand.Execute(null);
            await ShownAsync(rig);

            Assert.True(vm.IsEndStep);
            Assert.True(vm.IsCompleted);
        });

        [Fact]
        public Task TheAnswers_AreWrittenIntoTheSecondReport_AsTheFormHadThem_AndTheRetryWritesTheSame() => Smoke(async (rig, vm) =>
        {
            await rig.ReachAsync(WorkflowState.Programming);
            await ViewModelTestSupport.Flush();
            Assert.True(vm.IsProgramStep);
            Assert.True(vm.Form.IsEditable);
            Assert.True(vm.Form.Templates.Count > 0);

            vm.Form.Programming.IsYes = true;
            vm.Form.FunctionTest.IsYes = true;
            vm.Form.Restricted.IsNo = true;
            Assert.True(vm.ContinueCommand.CanExecute(null));

            // The date cannot be set the first time: the work stops after the report was written.
            rig.Store.TouchAnswer = Result.Fail(new Failure(FailureKind.Unreachable, "orders.unreachable", "No server."));
            vm.ContinueCommand.Execute(null);
            await ShownAsync(rig);

            Assert.True(vm.IsProgramStep);
            Assert.False(vm.Form.IsEditable);                                   // the engine has the answers now
            Assert.True(vm.CanRetry);
            var first = Assert.Single(rig.Reports.Writes);
            Assert.Equal(vm.Form.ToAnswers().ToFields(vm.Form.SelectedTemplate!).Value.OrderBy(p => p.Key), first.Fields.OrderBy(p => p.Key));

            rig.Store.TouchAnswer = null;
            vm.RetryCommand.Execute(null);
            await ShownAsync(rig);

            Assert.True(vm.IsReviewStep);
            Assert.Equal(2, rig.Reports.Writes.Count);
            Assert.Equal(first.Fields.OrderBy(p => p.Key), rig.Reports.Writes[1].Fields.OrderBy(p => p.Key));
        });

        [Fact]
        public Task AFailedReadOut_CanBeFinishedWithoutIt_OnlyAfterTheOperatorSaidYes() => Smoke(async (rig, vm, services) =>
        {
            await rig.ReachAsync(WorkflowState.ReadOutFailed);
            await ViewModelTestSupport.Flush();
            Assert.True(vm.IsCheckStep);
            Assert.True(vm.ShowFinishWithoutReadOut);
            Assert.Equal(vm.Strings[Keys.GuidedRetryReadOutButton], vm.RetryText);

            services.Answer = false;
            vm.FinishWithoutReadOutCommand.Execute(null);
            await ShownAsync(rig);
            Assert.True(vm.IsCheckStep);

            services.Answer = true;
            vm.FinishWithoutReadOutCommand.Execute(null);
            await ShownAsync(rig);
            Assert.True(vm.IsCompleted);
            Assert.Equal(2, services.Asked.Count);
        });

        [Fact]
        public Task ACancelledCheck_EndsOnTheEndStep_AndBackReadsTheListAgain() => Smoke(async (rig, vm) =>
        {
            await ChooseTheTestOrderAsync(rig, vm);
            vm.StartOrderCommand.Execute(null);
            await ShownAsync(rig);
            Assert.True(vm.IsCardStep);

            vm.CancelCommand.Execute(null);
            await ViewModelTestSupport.Flush();
            Assert.True(vm.IsEndStep);
            Assert.Equal(vm.Strings[Keys.GuidedEndCancelledNothing], vm.EndText);

            vm.BackCommand.Execute(null);
            await ShownAsync(rig);
            Assert.True(vm.IsOrdersStep);
            Assert.Null(vm.SelectedOrder);
            Assert.Equal(5, vm.Orders.Count);
            Assert.Equal(3, rig.Store.ListCalls);
        });

        [Fact]
        public Task ARefusedStart_ShowsItsReason_AndALaterReadDoesNotTakeItAway() => Smoke(async (rig, vm) =>
        {
            rig.UseSettings(settings => settings with { DefaultProjectPath = string.Empty });
            await ChooseTheTestOrderAsync(rig, vm);

            vm.StartOrderCommand.Execute(null);
            await ShownAsync(rig);

            Assert.True(vm.IsOrdersStep);
            Assert.Equal(WorkflowFailureCodes.ProjectNotConfigured, Assert.Single(vm.Messages).Code);
            Assert.Equal(vm.Strings[Keys.FailProjectNotConfigured], vm.Messages[0].Text);

            // The timer's read: it goes through the engine, not through a command of the operator.
            rig.Engine.RefreshOrders();
            await ShownAsync(rig);
            Assert.Single(vm.Messages);
        });
    }
}
