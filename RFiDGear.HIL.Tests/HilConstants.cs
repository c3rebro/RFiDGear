using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The settings of a data file, as a HIL test creates it on the card and as a Check File Settings task expects it.
    /// </summary>
    /// <param name="FileType">The file type.</param>
    /// <param name="Size">The size in bytes.</param>
    /// <param name="CommunicationMode">The communication mode.</param>
    /// <param name="Read">Who may read.</param>
    /// <param name="Write">Who may write.</param>
    /// <param name="ReadWrite">Who may read and write.</param>
    /// <param name="Change">Who may change the access rights.</param>
    internal sealed record HilFileSettings(
        FileType_MifareDesfireFileType FileType,
        uint Size,
        EncryptionMode CommunicationMode,
        TaskAccessRights Read,
        TaskAccessRights Write,
        TaskAccessRights ReadWrite,
        TaskAccessRights Change)
    {
        /// <summary>
        /// A standard data file whose four access rights are four different values (key 1, free, never, key 0), so
        /// that a mix-up of two of them shows on a card: the application has two keys, which leaves these values.
        /// </summary>
        public static HilFileSettings Distinct { get; } = new HilFileSettings(
            FileType_MifareDesfireFileType.StdDataFile,
            HilConstants.TestFileSize,
            EncryptionMode.CM_PLAIN,
            TaskAccessRights.AR_KEY1,
            TaskAccessRights.AR_FREE,
            TaskAccessRights.AR_NEVER,
            TaskAccessRights.AR_KEY0);
    }

    /// <summary>
    /// Fixed parameters shared across all HIL tests.
    /// </summary>
    internal static class HilConstants
    {
        /// <summary>
        /// AID reserved for HIL tests. Chosen to avoid any production AID range.
        /// All tests create and clean up this application on the card under test.
        /// </summary>
        public const uint TestAppId = 0x5F5F01;

        /// <summary>File number used for read/write tests inside the test application.</summary>
        public const byte TestFileNo = 0x01;

        /// <summary>File size in bytes for the test data file.</summary>
        public const uint TestFileSize = 16;

        /// <summary>Default key value for DES / 2K3DES (16 bytes zero-padded). DF_KEY_DES in this codebase is 2-key 3DES.</summary>
        public const string DefaultKeyDes = "00000000000000000000000000000000";

        /// <summary>Default key value for AES (16 bytes zero). DESFire EV2+ uses AES for the programmed state.</summary>
        public const string DefaultKeyAes = "00000000000000000000000000000000";

        /// <summary>
        /// Payload written to the test file. Must be exactly <see cref="TestFileSize"/> bytes.
        /// </summary>
        public static readonly byte[] TestPayload =
            { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04,
              0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C };
    }
}
