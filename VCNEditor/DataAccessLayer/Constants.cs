/*
 * Created by SharpDevelop.
 * Date: 12.10.2017
 * Time: 11:21
 *
 */

using System;

namespace RFiDGear.DataAccessLayer
{
    // change key using mk = 0 enum
    // allow change mk = 1 or-ing
    // listing without mk = 2 or-ing
    // create del without mk = 4 or-ing
    // config changeable = 8 or-ing
    // default setting = 11
    // change using keyno = 224 enum
    // change frozen = 240 enum

    public static class Constants
    {
        /// <summary>
        /// Gets or sets the timeout for chip response in milliseconds.
        /// </summary>
        public static int MaxWaitInsertion { get; set; } = 200;

        /// <summary>
        /// Gets or sets the suffix used for special app versions.
        /// </summary>
        public static string TitleSuffix { get; set; } = "DEVELOPER PREVIEW";
    }
    
    /// <summary>
    ///
    /// </summary>
    public enum FileType_MifareDesfireFileType
    {
        StdDataFile,
        BackupFile,
        ValueFile,
        CyclicRecordFile,
        LinearRecordFile
    }

    /// <summary>
    ///
    /// </summary>
    public enum TaskType_MifareClassicTask
    {
        None,
        ReadData,
        WriteData,
        ChangeDefault
    }

    public enum TaskType_MifareDesfireTask
    {
        None,
        FormatDesfireCard,
        PICCMasterKeyChangeover,
        ApplicationKeyChangeover,
        ReadData,
        WriteData,
        CreateApplication,
        CreateFile,
        DeleteApplication,
        DeleteFile,
        ChangeDefault
    }

    /// <summary>
    /// Select DataBlock in Data Explorer
    /// </summary>
    [Flags]
    public enum DataExplorer_DataBlock
    {
        Block0 = 0,
        Block1 = 1,
        Block2 = 2,
        Block3 = 3
    }

    /// <summary>
    /// Select DataBlock in Sector Trailer Access Bits
    /// </summary>
    public enum SectorTrailer_DataBlock
    {
        Block0 = 0,
        Block1 = 1,
        Block2 = 2,
        BlockAll = 3
    }

    [Flags]
    public enum SectorTrailer_AccessType
    {
        WriteKeyB = 1,
        ReadKeyB = 2,
        WriteAccessBits = 4,
        ReadAccessBits = 8,
        WriteKeyA = 16,
        ReadKeyA = 32
    }

    /// <summary>
    ///
    /// </summary>
    [Flags]
    public enum AccessCondition_MifareDesfireAppCreation
    {
        ChangeKeyUsingMK = 0,
        ChangeKeyUsingKeyNo = 224,
        ChangeKeyFrozen = 240
    }

    /// <summary>
    ///
    /// </summary>
    public enum AccessCondition_MifareClassicSectorTrailer
    {
        NotApplicable,
        NotAllowed,
        Allowed_With_KeyA,
        Allowed_With_KeyB,
        Allowed_With_KeyA_Or_KeyB
    }

    /// <summary>
    /// Identifies a card by type and UID.
    /// </summary>
    public readonly struct CARD_INFO : IEquatable<CARD_INFO>
    {
        public CARD_INFO(CARD_TYPE type, string uid)
        {
            CardType = type;
            Uid = uid;
        }

        /// <summary>
        /// Gets the card UID.
        /// </summary>
        public string Uid { get; }

        /// <summary>
        /// Gets the card type.
        /// </summary>
        public CARD_TYPE CardType { get; }

        public bool Equals(CARD_INFO other)
        {
            return CardType == other.CardType
                && string.Equals(Uid, other.Uid, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CARD_INFO other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CardType, Uid);
        }

        public static bool operator ==(CARD_INFO left, CARD_INFO right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CARD_INFO left, CARD_INFO right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Description of Constants.
    /// </summary>
    public enum CARD_TYPE
    {
        Unspecified,
        Mifare1K,
        Mifare2K,
        Mifare4K,
        DESFire,
        DESFireEV1,
        DESFireEV2,
        CT_PLUS_1K,
        CT_PLUS_2K,
        CT_PLUS_4K,
        MifareUltralight
    };

    /// <summary>
    ///
    /// </summary>
    public enum ERROR
    {
        Empty,
        NoError,
        AuthenticationError,
        DeviceNotReadyError,
        IOError
    }

    /// <summary>
    ///
    /// </summary>
    public enum KEY_ERROR
    {
        KEY_IS_EMPTY,
        KEY_HAS_WRONG_LENGTH,
        KEY_HAS_WRONG_FORMAT,
        NO_ERROR
    };
}
