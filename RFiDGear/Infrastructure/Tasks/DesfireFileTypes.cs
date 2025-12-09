using System;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// Available DESFire file types. Cast to <see cref="byte"/> when building
    /// file creation APDUs.
    /// </summary>
    [Flags]
    public enum FileType_MifareDesfireFileType
    {
        StdDataFile = 0,
        BackupFile = 1,
        ValueFile = 2,
        LinearRecordFile = 3,
        CyclicRecordFile = 4
    }
}
