#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Services.SecureKeys;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// Whether a program can supply keys right now: off without a bridge, no supplier while the bridge listens and nobody
    /// registered, connected when a client did. The provider tells, and the status of the window listens to it.
    /// </summary>
    public class KeySupplierStatusTests
    {
        private sealed class Channel : IBridgeKeyChannel
        {
            public bool HasSupplier { get; set; }

            public Task<ResolveKeyResponse?> RequestKeyAsync(ResolveKeyRequest request, CancellationToken cancellationToken) =>
                Task.FromResult<ResolveKeyResponse?>(null);
        }

        [Fact]
        public void Without_a_channel_external_control_is_off()
        {
            Assert.Equal(KeySupplierAvailability.Off, new BridgeKeyProvider().Availability);
        }

        [Fact]
        public void With_a_channel_the_state_is_no_supplier_until_a_client_registered_and_connected_after_it()
        {
            var provider = new BridgeKeyProvider();
            var channel = new Channel();
            provider.Attach(channel);

            Assert.Equal(KeySupplierAvailability.NoSupplier, provider.Availability);

            channel.HasSupplier = true;
            Assert.Equal(KeySupplierAvailability.Connected, provider.Availability);

            channel.HasSupplier = false;
            Assert.Equal(KeySupplierAvailability.NoSupplier, provider.Availability);
        }

        [Fact]
        public void Letting_go_of_the_channel_turns_it_off_again()
        {
            var provider = new BridgeKeyProvider();
            var channel = new Channel { HasSupplier = true };
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
            var channel = new Channel { HasSupplier = true };
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
    }
}
