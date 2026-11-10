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
            WorkflowEngineTests.Sta(async rig =>
            {
                using var vm = new GuidedCheckViewModel(
                    new FakeExtensionHost(),
                    Dispatcher.CurrentDispatcher,
                    rig.Engine,
                    null,
                    new StubImportOffer(),
                    new FakeCloseConfirmation(),
                    new ManualTimer().Start);

                await body(rig, vm);
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
