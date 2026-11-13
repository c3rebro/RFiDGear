#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Services.SecureKeys;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// Whether a program can supply keys right now: off without a bridge or when the bridge does not listen, no supplier while it
    /// listens and nobody registered, connected when a client did. The channel tells, the provider passes it on, and the status of
    /// a window listens to the provider.
    /// </summary>
    public class KeySupplierStatusTests
    {
        private sealed class Channel : IBridgeKeyChannel
        {
            public KeySupplierAvailability Availability { get; set; } = KeySupplierAvailability.NoSupplier;

            public Task<ResolveKeyResponse?> RequestKeyAsync(ResolveKeyRequest request, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                cancellationToken.ThrowIfCancellationRequested();

                return Task.FromResult<ResolveKeyResponse?>(null);
            }
        }

        [Fact]
        public void Without_a_channel_external_control_is_off()
        {
            Assert.Equal(KeySupplierAvailability.Off, new BridgeKeyProvider().Availability);
        }

        [Fact]
        public void With_a_channel_the_provider_says_what_the_channel_says_at_the_moment()
        {
            var provider = new BridgeKeyProvider();
            var channel = new Channel();
            provider.Attach(channel);

            Assert.Equal(KeySupplierAvailability.NoSupplier, provider.Availability);

            channel.Availability = KeySupplierAvailability.Connected;
            Assert.Equal(KeySupplierAvailability.Connected, provider.Availability);

            // A bridge that failed after it was bound listens no more, however long the channel stays attached.
            channel.Availability = KeySupplierAvailability.Off;
            Assert.Equal(KeySupplierAvailability.Off, provider.Availability);
        }

        [Fact]
        public void Letting_go_of_the_channel_turns_it_off_again()
        {
            var provider = new BridgeKeyProvider();
            var channel = new Channel { Availability = KeySupplierAvailability.Connected };
            provider.Attach(channel);

            provider.Detach(channel);

            Assert.Equal(KeySupplierAvailability.Off, provider.Availability);
        }

        [Fact]
        public void Attaching_detaching_and_a_change_of_connection_are_announced_but_letting_go_of_a_replaced_channel_is_not()
        {
            var provider = new BridgeKeyProvider();
            var raised = 0;
            provider.AvailabilityChanged += (_, _) => raised++;
            var first = new Channel();
            var second = new Channel();

            provider.Attach(first);
            Assert.Equal(1, raised);

            provider.NotifyConnectionChanged();
            Assert.Equal(2, raised);

            provider.Attach(second);
            Assert.Equal(3, raised);

            // The first channel was replaced: letting go of it changes nothing and says nothing.
            provider.Detach(first);
            Assert.Equal(3, raised);
            Assert.Equal(KeySupplierAvailability.NoSupplier, provider.Availability);

            provider.Detach(second);
            Assert.Equal(4, raised);
            Assert.Equal(KeySupplierAvailability.Off, provider.Availability);
        }

        [Fact]
        public void The_status_of_a_window_reads_and_forwards_what_the_provider_says_and_stops_when_it_unsubscribes()
        {
            var provider = new BridgeKeyProvider();
            var status = new BridgeKeySupplierStatus(provider);
            var heard = 0;
            EventHandler handler = (_, _) => heard++;

            status.Changed += handler;
            var channel = new Channel { Availability = KeySupplierAvailability.Connected };
            provider.Attach(channel);

            Assert.Equal(1, heard);
            Assert.Equal(KeySupplierAvailability.Connected, status.Current);

            status.Changed -= handler;
            provider.Detach(channel);

            // The provider of the process lives as long as the application: a window that closed must not be called.
            Assert.Equal(1, heard);
            Assert.Equal(KeySupplierAvailability.Off, status.Current);
        }

        [Fact]
        public void The_status_needs_a_provider()
        {
            Assert.Throws<ArgumentNullException>(() => new BridgeKeySupplierStatus(null!));
        }

        // ── The rule the real channel applies to the server it reads ──

        // The enums are internal, so the cases name them.
        [Theory]
        [InlineData("Created", false, "Off")]
        [InlineData("Created", true, "Off")]
        [InlineData("Failed", false, "Off")]
        [InlineData("Failed", true, "Off")]
        [InlineData("Stopped", false, "Off")]
        [InlineData("Stopped", true, "Off")]
        [InlineData("Listening", false, "NoSupplier")]
        [InlineData("Listening", true, "Connected")]
        [InlineData("SessionActive", false, "NoSupplier")]
        [InlineData("SessionActive", true, "Connected")]
        public void A_server_that_does_not_listen_is_off_whatever_else_is_true(string state, bool clientSupplies, string expected)
        {
            var actual = ExternalControlService.AvailabilityOf(Enum.Parse<BridgeServerState>(state), clientSupplies);

            Assert.Equal(Enum.Parse<KeySupplierAvailability>(expected), actual);
        }

        [Fact]
        public void Every_state_of_the_server_is_covered_by_the_cases_above()
        {
            // A state that is added later must be decided on: it is neither on nor off by default.
            Assert.Equal(5, Enum.GetValues<BridgeServerState>().Length);
        }
    }
}
