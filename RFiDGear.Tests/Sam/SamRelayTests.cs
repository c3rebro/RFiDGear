#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Services.Sam;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// The relay that every reader shares: the gate of the session is held for the whole relay, the SAM is opened first and nothing is sent to the
    /// card until it is, the card and the SAM are let go of whatever happens, and no text of an exception reaches the log. The reader is a script:
    /// the SAM is the fake PC/SC reader of the other SAM tests, and the card is a transport that answers success.
    /// </summary>
    [Collection("SamPortGate")]
    public class SamRelayTests
    {
        private const string SamReader = "SAM READER";
        private const string CardReader = "CARD READER";

        [Fact]
        public async Task The_sam_is_opened_before_the_card_and_both_are_let_go_of_in_order()
        {
            var rig = new Rig();

            var result = await SamRelay.RunAsync(rig.Parts(), "test", rig.Run);

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { "sam open", "card open", "run", "card release" }, rig.Events);
            Assert.True(rig.Source.Handed.Single().Connection.Disposed);
            rig.AssertEverythingLetGo();
        }

        [Fact]
        public async Task A_sam_that_cannot_be_opened_ends_the_relay_before_the_card_is_asked_for()
        {
            var rig = new Rig { SamReaderSet = false };

            var result = await SamRelay.RunAsync(rig.Parts(), "test", rig.Run);

            Assert.Equal(ERROR.KeyUnavailable, result.Error);
            Assert.Equal(SamPiccFailure.NotConfigured, result.Failure);
            Assert.Equal(new[] { "sam open" }, rig.Events);
            rig.AssertEverythingLetGo();
        }

        [Theory]
        [InlineData(SamPiccFailure.NoCard, ERROR.TransportError)]
        [InlineData(SamPiccFailure.NotDesfire, ERROR.ProtocolConstraint)]
        [InlineData(SamPiccFailure.ReaderFailure, ERROR.TransportError)]
        public async Task A_card_that_is_missing_or_no_desfire_card_ends_the_relay_and_the_card_and_the_sam_are_let_go_of(SamPiccFailure failure, ERROR error)
        {
            var rig = new Rig { CardFails = failure };

            var result = await SamRelay.RunAsync(rig.Parts(), "test", rig.Run);

            Assert.Equal(error, result.Error);
            Assert.Equal(failure, result.Failure);
            Assert.Equal(new[] { "sam open", "card open", "card release" }, rig.Events);
            Assert.True(rig.Source.Handed.Single().Connection.Disposed);
            rig.AssertEverythingLetGo();
        }

        [Fact]
        public async Task A_fault_in_the_operation_is_a_transport_error_without_its_text_and_everything_is_let_go_of()
        {
            using var logs = CapturedLogs.Install();
            var rig = new Rig { RunThrows = new InvalidOperationException("PRIVATE-READER-TEXT") };

            var result = await SamRelay.RunAsync(rig.Parts(), "test", rig.Run);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(SamPiccFailure.ReaderFailure, result.Failure);
            Assert.Equal(new[] { "sam open", "card open", "run", "card release" }, rig.Events);
            Assert.True(rig.Source.Handed.Single().Connection.Disposed);
            rig.AssertEverythingLetGo();
            Assert.Contains(logs.All(), entry => CapturedLogs.EverythingWrittenFor(entry).Contains("TEST", StringComparison.Ordinal));
            Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), text => Assert.DoesNotContain("PRIVATE-READER-TEXT", text, StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_cancelled_operation_is_not_swallowed_and_everything_is_let_go_of()
        {
            var rig = new Rig { RunThrows = new OperationCanceledException() };

            await Assert.ThrowsAsync<OperationCanceledException>(() => SamRelay.RunAsync(rig.Parts(), "test", rig.Run));

            Assert.Equal(new[] { "sam open", "card open", "run", "card release" }, rig.Events);
            Assert.True(rig.Source.Handed.Single().Connection.Disposed);
            rig.AssertEverythingLetGo();
        }

        [Fact]
        public async Task A_card_that_cannot_be_opened_because_it_throws_is_still_let_go_of()
        {
            var rig = new Rig { CardOpenThrows = new InvalidOperationException("PRIVATE-READER-TEXT") };

            var result = await SamRelay.RunAsync(rig.Parts(), "test", rig.Run);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(new[] { "sam open", "card open", "card release" }, rig.Events);
            Assert.True(rig.Source.Handed.Single().Connection.Disposed);
            rig.AssertEverythingLetGo();
        }

        [Fact]
        public async Task Two_relays_on_one_gate_run_one_after_the_other()
        {
            var rig = new Rig();
            var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.HoldCard = hold;

            var first = SamRelay.RunAsync(rig.Parts(), "first", rig.Run);
            await WaitUntilAsync(() => rig.Events.Contains("card open"));
            var second = SamRelay.RunAsync(rig.Parts(), "second", rig.Run);

            // While the first one holds the gate the second does not even open the SAM.
            await Task.Delay(150);
            Assert.Equal(1, rig.Events.Count(e => e == "sam open"));
            Assert.False(second.IsCompleted);

            hold.SetResult(true);

            Assert.True((await first).Succeeded);
            Assert.True((await second).Succeeded);
            Assert.Equal(2, rig.Events.Count(e => e == "sam open"));
            Assert.Equal(2, rig.Source.Handed.Count);
            rig.AssertEverythingLetGo();
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(10);

            while (!condition() && DateTime.UtcNow < until)
            {
                await Task.Delay(10);
            }

            Assert.True(condition(), "The first relay never reached the card.");
        }

        /// <summary>A reader as the relay sees it: a SAM in a fake PC/SC reader, a card that is a script, and every step recorded in order.</summary>
        private sealed class Rig
        {
            internal List<string> Events { get; } = new List<string>();

            internal FakeSamReaderSource Source { get; } = new FakeSamReaderSource().With(SamReader, SamTestData.SamAtr);

            internal SemaphoreSlim Gate { get; } = new SemaphoreSlim(1, 1);

            internal bool SamReaderSet { get; set; } = true;

            internal SamPiccFailure CardFails { get; set; } = SamPiccFailure.None;

            internal Exception? CardOpenThrows { get; set; }

            internal Exception? RunThrows { get; set; }

            internal TaskCompletionSource<bool>? HoldCard { get; set; }

            internal SamRelayParts Parts() => new SamRelayParts("TEST", Gate, OpenSam, OpenCardAsync, ReleaseCard);

            internal Task<SamPiccResult> Run(SamPiccSession session)
            {
                Events.Add("run");

                if (RunThrows != null)
                {
                    throw RunThrows;
                }

                return Task.FromResult(SamPiccResult.Ok);
            }

            internal void AssertEverythingLetGo()
            {
                Assert.Equal(1, Gate.CurrentCount);
                Assert.False(SamPortGate.IsHeld);
            }

            private Task<SamPiccOpen> OpenSam()
            {
                Events.Add("sam open");
                var connector = new SamPiccConnector(new SamReaderFinder(Source));
                var samReader = SamReaderSet ? SamReader : string.Empty;

                return Task.Run(() => connector.Open(samReader, CardReader));
            }

            private async Task<SamCardLink> OpenCardAsync()
            {
                Events.Add("card open");

                if (CardOpenThrows != null)
                {
                    throw CardOpenThrows;
                }

                if (HoldCard != null)
                {
                    await HoldCard.Task;
                }

                return CardFails == SamPiccFailure.None
                    ? new SamCardLink(SamPiccFailure.None, ScriptedSamTransport.Always(0x90, 0x00))
                    : new SamCardLink(CardFails, null);
            }

            private Task ReleaseCard()
            {
                Events.Add("card release");

                return Task.CompletedTask;
            }
        }
    }
}
