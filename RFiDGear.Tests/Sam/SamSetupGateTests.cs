#nullable enable
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Services.Sam;

using System;
using System.Collections.Generic;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// The setup holds the reader for as long as it is open, so that nothing else uses it meanwhile: the slot of the coordinator (no run,
    /// load, edit or scan is admitted, and the setup is not admitted while one is in progress) and the gate of the reader.
    /// </summary>
    public class SamSetupGateTests
    {
        [Fact]
        public void The_reader_is_held_while_the_setup_is_open_and_the_main_window_is_told_that_it_is_busy()
        {
            var gate = new ReaderAccessGate();
            var coordinator = new ExtensionOperationCoordinator();
            var told = new List<bool?>();
            var heldInside = false;
            var scanningInside = false;

            var ran = SamSetupGate.RunExclusive(gate, coordinator, told.Add, () =>
            {
                heldInside = gate.IsHeld;
                scanningInside = coordinator.IsScanning;
            });

            Assert.True(ran);
            Assert.True(heldInside);
            Assert.True(scanningInside);
            Assert.False(gate.IsHeld);
            Assert.True(coordinator.IsIdle);
            Assert.Equal(new bool?[] { true, false }, told);
        }

        [Fact]
        public void While_the_setup_is_open_no_run_and_no_edit_is_admitted()
        {
            var gate = new ReaderAccessGate();
            var coordinator = new ExtensionOperationCoordinator();
            var execution = true;
            var projectEdit = true;
            var scan = true;

            SamSetupGate.RunExclusive(gate, coordinator, _ => { }, () =>
            {
                execution = coordinator.TryBeginExecution();
                projectEdit = coordinator.TryBeginProjectOperation();
                scan = coordinator.TryBeginScan();
            });

            Assert.False(execution);
            Assert.False(projectEdit);
            Assert.False(scan);
        }

        [Fact]
        public void A_run_that_is_in_progress_keeps_the_setup_from_opening_and_is_not_disturbed()
        {
            var gate = new ReaderAccessGate();
            var coordinator = new ExtensionOperationCoordinator();
            var told = new List<bool?>();
            var run = false;

            // A run of tasks holds the coordinator, and not the gate, while it works.
            Assert.True(coordinator.TryBeginExecution());

            var ran = SamSetupGate.RunExclusive(gate, coordinator, told.Add, () => run = true);

            Assert.False(ran);
            Assert.False(run);
            Assert.Empty(told);
            Assert.True(coordinator.IsExecuting);
            Assert.False(gate.IsHeld);

            coordinator.EndExecution();
        }

        [Fact]
        public void When_somebody_else_holds_the_gate_nothing_runs_the_gate_stays_theirs_and_the_slot_is_given_back()
        {
            var gate = new ReaderAccessGate();
            var coordinator = new ExtensionOperationCoordinator();
            Assert.True(gate.TryEnter());
            var told = new List<bool?>();
            var run = false;

            var ran = SamSetupGate.RunExclusive(gate, coordinator, told.Add, () => run = true);

            Assert.False(ran);
            Assert.False(run);
            Assert.Empty(told);
            Assert.True(gate.IsHeld);
            Assert.True(coordinator.IsIdle);

            gate.Exit();
        }

        [Fact]
        public void Both_are_given_back_when_the_setup_throws_and_the_exception_is_not_swallowed()
        {
            var gate = new ReaderAccessGate();
            var coordinator = new ExtensionOperationCoordinator();
            var told = new List<bool?>();

            Assert.Throws<InvalidOperationException>(() => SamSetupGate.RunExclusive(gate, coordinator, told.Add, () => throw new InvalidOperationException("x")));

            Assert.False(gate.IsHeld);
            Assert.True(coordinator.IsIdle);
            Assert.Equal(new bool?[] { true, false }, told);
        }

        [Fact]
        public void The_reader_can_be_taken_again_after_the_setup_was_closed()
        {
            var gate = new ReaderAccessGate();
            var coordinator = new ExtensionOperationCoordinator();

            Assert.True(SamSetupGate.RunExclusive(gate, coordinator, _ => { }, () => { }));
            Assert.True(SamSetupGate.RunExclusive(gate, coordinator, _ => { }, () => { }));
            Assert.True(coordinator.TryBeginExecution());
        }

        [Fact]
        public void A_missing_part_is_refused()
        {
            var gate = new ReaderAccessGate();
            var coordinator = new ExtensionOperationCoordinator();

            Assert.Throws<ArgumentNullException>(() => SamSetupGate.RunExclusive(null!, coordinator, _ => { }, () => { }));
            Assert.Throws<ArgumentNullException>(() => SamSetupGate.RunExclusive(gate, null!, _ => { }, () => { }));
            Assert.Throws<ArgumentNullException>(() => SamSetupGate.RunExclusive(gate, coordinator, null!, () => { }));
            Assert.Throws<ArgumentNullException>(() => SamSetupGate.RunExclusive(gate, coordinator, _ => { }, null!));
            Assert.False(gate.IsHeld);
            Assert.True(coordinator.IsIdle);
        }
    }
}
