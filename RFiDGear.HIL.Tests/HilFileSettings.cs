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
}
