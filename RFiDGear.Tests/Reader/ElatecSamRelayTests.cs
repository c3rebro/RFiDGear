#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Services.Sam;
using RFiDGear.Tests.Classic;
using RFiDGear.Tests.Sam;

using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The TWN4 as the relay between a MIFARE SAM AV3 and a DESFire card: the SAM sits in a socket of the same reader that the card is on, and
    /// the whole relay runs under the lock of the COM port, so that nothing else talks to the reader meanwhile. The reader is a script
    /// (<see cref="FakeTwn4Ports"/>); the SAM and the card answer as they did on the bench.
    /// </summary>
    [Collection("SamPortGate")]
    public class ElatecSamRelayTests
    {
        private const string Ff = "FFFFFFFFFFFF";

        private static readonly SamKeyReference Entry1 = new SamKeyReference(1, 0);
        private static readonly SamKeyReference Entry2 = new SamKeyReference(2, 0);

        private static DesfireSamAccess Access(byte keyNumber = 0) => new DesfireSamAccess(0, keyNumber, DESFireKeyType.DF_KEY_DES, Entry2);

        private static DesfireSamKeyChange Change() =>
            new DesfireSamKeyChange(0, 0, DESFireKeyType.DF_KEY_DES, Entry2, 0, DESFireKeyType.DF_KEY_AES, Entry2, Entry1);

        // A TWN4 with the SAM in the first socket, whose SAM and card are the script of the bench.
        private static FakeTwn4Ports Ports(PiccRig rig)
        {
            var ports = new FakeTwn4Ports { Channel = rig.Sam };
            ports.Cards[Twn4SamSlot.Sam1] = SamTestData.SamAtr;
            ports.Card = apdu => rig.Card.Exchange(apdu);

            return ports;
        }

        [Fact]
        public async Task An_authentication_ends_with_the_answer_of_the_session_and_the_sam_is_powered_down()
        {
            var rig = new PiccRig();
            var ports = Ports(rig);
            using var reader = new RelayElatec(ports);

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { "has Sam1", "power on Sam1", "has Sam2", "set t1 Sam1", "channel Sam1", "search card" }, ports.Events.Take(6));
            Assert.Equal("power off Sam1", ports.Events.Last());
            Assert.Equal(6, rig.Frames.Count);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task A_key_change_is_checked_by_authenticating_with_the_new_key()
        {
            var rig = new PiccRig();
            using var reader = new RelayElatec(Ports(rig));

            var result = await reader.ChangeMifareDesfireKeyWithSamAsync(Change());

            Assert.True(result.Succeeded);
            Assert.Contains(rig.CardFrames, f => f.StartsWith("CARD 90C4", StringComparison.Ordinal));

            // The card master key was DES and is AES now: the check authenticates with the AES command.
            Assert.Contains(rig.CardFrames, f => f.StartsWith("CARD 90AA", StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_reader_that_is_not_connected_is_a_transport_error_and_nothing_is_sent()
        {
            using var reader = new RelayElatec(null);

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(SamPiccFailure.ReaderFailure, result.Failure);
            Assert.False(SamPortGate.IsHeld);
        }

        [Fact]
        public async Task A_request_that_cannot_be_sent_is_refused_before_the_reader_is_asked()
        {
            var ports = new FakeTwn4Ports();
            using var reader = new RelayElatec(ports);

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access(keyNumber: 14));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(SamPiccFailure.InvalidRequest, result.Failure);
            Assert.Empty(ports.Events);
        }

        [Fact]
        public async Task A_key_change_that_cannot_be_sent_is_refused_before_the_reader_is_asked()
        {
            var ports = new FakeTwn4Ports();
            using var reader = new RelayElatec(ports);
            var change = new DesfireSamKeyChange(0, 0, DESFireKeyType.DF_KEY_DES, Entry2, 14, DESFireKeyType.DF_KEY_AES, Entry2, Entry1);

            var result = await reader.ChangeMifareDesfireKeyWithSamAsync(change);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(SamPiccFailure.InvalidRequest, result.Failure);
            Assert.Empty(ports.Events);
        }

        [Fact]
        public async Task A_reader_without_a_sam_says_so_and_the_card_is_never_searched()
        {
            var ports = new FakeTwn4Ports();
            using var reader = new RelayElatec(ports);

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(ERROR.KeyUnavailable, result.Error);
            Assert.Equal(SamPiccFailure.SamNotFound, result.Failure);
            Assert.DoesNotContain("search card", ports.Events);
        }

        [Fact]
        public async Task A_sam_in_each_socket_is_no_choice_that_the_reader_makes()
        {
            var rig = new PiccRig();
            var ports = Ports(rig);
            ports.Cards[Twn4SamSlot.Sam2] = SamTestData.SamAtr;
            using var reader = new RelayElatec(ports);

            var result = await reader.AuthenticateMifareDesfireWithSamAsync(Access());

            Assert.Equal(ERROR.KeyUnavailable, result.Error);
            Assert.Equal(SamPiccFailure.SamAmbiguous, result.Failure);
            Assert.DoesNotContain("search card", ports.Events);
            Assert.Empty(rig.Frames);
        }

        [Fact]
        public async Task A_relay_holds_the_lock_of_the_com_port_and_another_operation_of_the_reader_waits_for_it()
        {
            var rig = new PiccRig();
            var ports = Ports(rig);
            ports.HoldSearch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reader = new RelayElatec(ports);

            var relay = reader.AuthenticateMifareDesfireWithSamAsync(Access());
            await WaitUntilAsync(() => ports.Events.Contains("search card"));

            // The relay is under way with the card search held: a Classic read needs the same lock and must not get to the reader.
            var classic = reader.ReadMifareClassicSingleSector(2, Ff, Ff);
            await Task.Delay(150);

            Assert.False(classic.IsCompleted);
            Assert.DoesNotContain("classic open", ports.Events);

            ports.HoldSearch.SetResult(true);

            Assert.True((await relay).Succeeded);
            await classic;
            Assert.True(ports.Events.IndexOf("classic open") > ports.Events.IndexOf("power off Sam1"));
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(10);

            while (!condition() && DateTime.UtcNow < until)
            {
                await Task.Delay(10);
            }

            Assert.True(condition(), "The relay never reached the card.");
        }

        /// <summary>A TWN4 provider whose reader is a script, and whose Classic port records that it was asked for.</summary>
        private sealed class RelayElatec : ElatecNetProvider
        {
            private readonly FakeTwn4Ports? _ports;

            internal RelayElatec(FakeTwn4Ports? ports)
            {
                _ports = ports;
            }

            internal override ITwn4SamRelayPorts? CreateTwn4Ports() => _ports;

            protected override Task<IClassicPort> OpenClassicPortAsync()
            {
                _ports?.Events.Add("classic open");

                return Task.FromResult<IClassicPort>(new SimulatedClassicChip());
            }
        }
    }
}
