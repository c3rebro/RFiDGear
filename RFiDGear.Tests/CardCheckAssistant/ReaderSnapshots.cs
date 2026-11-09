using RFiDGear.Contracts;
using RFiDGear.Infrastructure;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The pictures of a reader the tests use, written once so that a test says only what it is about.</summary>
    internal static class Snapshots
    {
        public const string Reader = "Test reader";

        public static ReaderSnapshot NoReader(string reader = Reader) =>
            new ReaderSnapshot(reader, ReaderScanState.NoReader, System.Array.Empty<CredentialInfo>());

        public static ReaderSnapshot NoCard(string reader = Reader) =>
            new ReaderSnapshot(reader, ReaderScanState.Ready, System.Array.Empty<CredentialInfo>());

        public static ReaderSnapshot ReadFailed(string reader = Reader) =>
            new ReaderSnapshot(reader, ReaderScanState.ReadFailed, System.Array.Empty<CredentialInfo>());

        public static ReaderSnapshot Busy(string reader = Reader) =>
            new ReaderSnapshot(reader, ReaderScanState.Busy, System.Array.Empty<CredentialInfo>());

        /// <summary>A card of this type (the name of a card type, whose code it takes) on the reader.</summary>
        public static ReaderSnapshot Card(string uid, string typeName = "DESFireEV1") => CardOn(Reader, uid, typeName);

        /// <summary>A card of this type on a reader of this name.</summary>
        public static ReaderSnapshot CardOn(string reader, string uid, string typeName = "DESFireEV1") =>
            new ReaderSnapshot(
                reader,
                ReaderScanState.Ready,
                new[] { new CredentialInfo(uid, (int)System.Enum.Parse<CARD_TYPE>(typeName), typeName, true) });
    }
}
