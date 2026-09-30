namespace RFiDGear.HIL.Tests
{
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

        /// <summary>Default key value for DES (8 bytes zero-padded).</summary>
        public const string DefaultKeyDes = "0000000000000000";

        /// <summary>Default key value for AES and 3K3DES (16 bytes / 24 bytes zero).</summary>
        public const string DefaultKeyAes = "00000000000000000000000000000000";

        /// <summary>Default key value for 3K3DES (24 bytes zero).</summary>
        public const string DefaultKey3K3Des = "000000000000000000000000000000000000000000000000";

        /// <summary>
        /// Payload written to the test file. Must be exactly <see cref="TestFileSize"/> bytes.
        /// </summary>
        public static readonly byte[] TestPayload =
            { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04,
              0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C };
    }
}
