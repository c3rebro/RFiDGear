using System;

using RFiDGear.Infrastructure.ExtensionHost;

using Xunit;

namespace RFiDGear.Tests
{
    public class CoordinatorTaskListEditGateTests
    {
        [Fact]
        public void TryEdit_WhenARunIsInProgress_DoesNotRunTheEditAndTellsTheUser()
        {
            var coordinator = new ExtensionOperationCoordinator();
            var told = 0;
            var gate = new CoordinatorTaskListEditGate(coordinator, () => told++);
            Assert.True(coordinator.TryBeginExecution());
            var ran = false;

            var result = gate.TryEdit(() =>
            {
                ran = true;
                return true;
            });

            Assert.False(result);
            Assert.False(ran);
            Assert.Equal(1, told);
            Assert.True(coordinator.IsExecuting);   // the run's slot is not touched
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TryEdit_WhenIdle_ReturnsWhatTheEditReturnsAndGivesTheSlotBack(bool editResult)
        {
            var coordinator = new ExtensionOperationCoordinator();
            var told = 0;
            var gate = new CoordinatorTaskListEditGate(coordinator, () => told++);
            var holdingTheSlot = false;

            var result = gate.TryEdit(() =>
            {
                holdingTheSlot = coordinator.IsMutating;
                return editResult;
            });

            Assert.Equal(editResult, result);
            Assert.True(holdingTheSlot);
            Assert.Equal(0, told);
            Assert.False(coordinator.IsMutating);
        }

        [Fact]
        public void TryEdit_WhenTheEditThrows_StillGivesTheSlotBack()
        {
            var coordinator = new ExtensionOperationCoordinator();
            var gate = new CoordinatorTaskListEditGate(coordinator, () => { });

            Assert.Throws<InvalidOperationException>(() => gate.TryEdit(() => throw new InvalidOperationException()));

            Assert.False(coordinator.IsMutating);
            Assert.True(coordinator.TryBeginProjectOperation());
            coordinator.EndProjectOperation();
        }

        [Fact]
        public void TryEdit_WhenAnotherEditIsInProgress_IsRefused()
        {
            var coordinator = new ExtensionOperationCoordinator();
            var gate = new CoordinatorTaskListEditGate(coordinator, () => { });
            var innerResult = true;

            var outerResult = gate.TryEdit(() =>
            {
                innerResult = gate.TryEdit(() => true);
                return true;
            });

            Assert.True(outerResult);
            Assert.False(innerResult);
        }
    }
}
