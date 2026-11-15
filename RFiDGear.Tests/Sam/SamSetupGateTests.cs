#nullable enable
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Services.Sam;

using System;
using System.Collections.Generic;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>The setup holds the gate of the reader for as long as it is open, so that nothing else uses the reader meanwhile.</summary>
    public class SamSetupGateTests
    {
        [Fact]
        public void The_gate_is_held_while_the_setup_is_open_and_the_main_window_is_told_that_the_reader_is_busy()
        {
            var gate = new ReaderAccessGate();
            var told = new List<bool?>();
            var heldInside = false;

            var ran = SamSetupGate.RunExclusive(gate, told.Add, () => heldInside = gate.IsHeld);

            Assert.True(ran);
            Assert.True(heldInside);
            Assert.False(gate.IsHeld);
            Assert.Equal(new bool?[] { true, false }, told);
        }

        [Fact]
        public void When_somebody_else_holds_the_gate_nothing_runs_and_the_gate_stays_theirs()
        {
            var gate = new ReaderAccessGate();
            Assert.True(gate.TryEnter());
            var told = new List<bool?>();
            var run = false;

            var ran = SamSetupGate.RunExclusive(gate, told.Add, () => run = true);

            Assert.False(ran);
            Assert.False(run);
            Assert.Empty(told);
            Assert.True(gate.IsHeld);

            gate.Exit();
        }

        [Fact]
        public void The_gate_is_given_back_when_the_setup_throws_and_the_exception_is_not_swallowed()
        {
            var gate = new ReaderAccessGate();
            var told = new List<bool?>();

            Assert.Throws<InvalidOperationException>(() => SamSetupGate.RunExclusive(gate, told.Add, () => throw new InvalidOperationException("x")));

            Assert.False(gate.IsHeld);
            Assert.Equal(new bool?[] { true, false }, told);
        }

        [Fact]
        public void The_gate_can_be_taken_again_after_the_setup_was_closed()
        {
            var gate = new ReaderAccessGate();

            Assert.True(SamSetupGate.RunExclusive(gate, _ => { }, () => { }));
            Assert.True(SamSetupGate.RunExclusive(gate, _ => { }, () => { }));
        }

        [Fact]
        public void A_missing_part_is_refused()
        {
            var gate = new ReaderAccessGate();

            Assert.Throws<ArgumentNullException>(() => SamSetupGate.RunExclusive(null!, _ => { }, () => { }));
            Assert.Throws<ArgumentNullException>(() => SamSetupGate.RunExclusive(gate, null!, () => { }));
            Assert.Throws<ArgumentNullException>(() => SamSetupGate.RunExclusive(gate, _ => { }, null!));
            Assert.False(gate.IsHeld);
        }
    }
}
