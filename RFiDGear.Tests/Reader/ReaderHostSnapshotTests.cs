using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;
using RFiDGear.Tests.CardCheckAssistant;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The host's side of <see cref="IReaderHost.CurrentSnapshot"/>, without a reader: the picture comes from the
    /// accessor the application supplies, and a missing one means there is no reader.
    /// </summary>
    public class ReaderHostSnapshotTests
    {
        [Fact]
        public void CurrentSnapshot_IsWhatTheAccessorGives_EachTimeItIsAsked()
        {
            var picture = Snapshots.Card("04AA");
            var reader = new ReaderHostImpl(() => "Elatec", () => picture);

            Assert.Same(picture, reader.CurrentSnapshot);
            picture = Snapshots.NoCard();
            Assert.Same(picture, reader.CurrentSnapshot);
        }

        [Fact]
        public void AnAccessorThatGivesNothing_MeansNoReader_UnderTheNameOfTheReader()
        {
            var name = "first";
            var reader = new ReaderHostImpl(() => name, () => null);

            Assert.Equal(Snapshots.NoReader("first"), reader.CurrentSnapshot);
            name = "second";
            Assert.Equal(Snapshots.NoReader("second"), reader.CurrentSnapshot);
        }

        [Fact]
        public void WithoutAnAccessor_ThereIsNoReader_WhenTheApplicationHasNone()
        {
            using var scope = ReaderDeviceScope.Use(null);

            Assert.Equal(Snapshots.NoReader("Elatec"), new ReaderHostImpl("Elatec").CurrentSnapshot);
        }

        [Fact]
        public void WithoutAnAccessor_TheSnapshotIsMadeFromTheReaderTheApplicationUses()
        {
            var device = new ConnectedProvider { Connected = true, GenericChip = new GenericChipModel("04aa", CARD_TYPE.Mifare1K) };
            using var scope = ReaderDeviceScope.Use(device);
            var reader = new ReaderHostImpl("Elatec");

            Assert.Equal(ReaderScanState.Ready, reader.CurrentSnapshot.State);
            Assert.Equal(new CredentialInfo("04AA", (int)CARD_TYPE.Mifare1K, "Mifare1K", true), Assert.Single(reader.CurrentSnapshot.Credentials));

            device.Connected = false;
            Assert.Equal(Snapshots.NoReader("Elatec"), reader.CurrentSnapshot);
        }

        [Fact]
        public async Task AScan_IsNotAvailableYet_AndACancelledOneIsCancelled()
        {
            var reader = new ReaderHostImpl(() => "Elatec", () => Snapshots.NoCard());
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            await Assert.ThrowsAsync<NotSupportedException>(() => reader.ScanAsync());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ScanAsync(cancelled.Token));
        }

        /// <summary>A reader that is connected or not as the test says, and has read the chip the test gave it.</summary>
        private sealed class ConnectedProvider : ElatecNetProvider
        {
            /// <summary>Gets or sets whether the reader reports itself as connected.</summary>
            public bool Connected { get; set; }

            /// <inheritdoc />
            public override bool IsConnected => Connected;
        }
    }
}
