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
    /// The snapshot of the reader host before anything was scanned: the host's guess from the state of the reader the
    /// application uses, built without a hardware reader.
    /// </summary>
    public class ReaderHostSnapshotTests
    {
        [Fact]
        public void BeforeAnythingWasRead_WithNoReaderDevice_TheSnapshotSaysNoReader()
        {
            using var scope = ReaderDeviceScope.Use(null);

            Assert.Equal(Snapshots.NoReader("Elatec"), new ReaderHostImpl("Elatec").CurrentSnapshot);
        }

        [Fact]
        public void BeforeAnythingWasRead_TheSnapshotIsMadeFromTheReaderTheApplicationUses()
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
        public void ALookAtAReaderThatCannotBeAsked_IsNoReader_NotAnException()
        {
            var reader = new ReaderHostImpl("Elatec");
            using var scope = ReaderDeviceScope.Use(new ThrowingProvider());

            Assert.Equal(Snapshots.NoReader("Elatec"), reader.CurrentSnapshot);
        }

        /// <summary>A reader that is connected or not as the test says, and holds the chip the test gave it.</summary>
        private sealed class ConnectedProvider : ElatecNetProvider
        {
            /// <summary>Gets or sets whether the reader reports itself as connected.</summary>
            public bool Connected { get; set; }

            /// <inheritdoc />
            public override bool IsConnected => Connected;
        }

        /// <summary>A reader that cannot say whether it is connected.</summary>
        private sealed class ThrowingProvider : ElatecNetProvider
        {
            /// <inheritdoc />
            public override bool IsConnected => throw new System.InvalidOperationException("The reader cannot be asked.");
        }
    }
}
