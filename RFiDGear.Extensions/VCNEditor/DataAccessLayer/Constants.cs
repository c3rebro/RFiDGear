/*
 * Created by SharpDevelop.
 * Date: 12.10.2017
 * Time: 11:21
 *
 */

// Codacy TODO: Remove this file if it is not used in the project. possible duplicate in: RFiDGear.DataAccessLayer.Constants.cs

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
    /// Defines supported Mifare DESFire file types.
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
    /// Defines available task types for Mifare Classic operations.
    /// </summary>
    public enum TaskType_MifareClassicTask
    {
        None,
        ReadData,
        WriteData,
        ChangeDefault
    }

    /// <summary>
    /// Defines available task types for Mifare DESFire operations.
    /// </summary>
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
    /// Selects a data block in the data explorer (bit flags for blocks 0-3).
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
    /// Selects which data block is referenced by sector trailer access bits.
    /// </summary>
    public enum SectorTrailer_DataBlock
    {
        Block0 = 0,
        Block1 = 1,
        Block2 = 2,
        BlockAll = 3
    }

    /// <summary>
    /// Defines sector trailer access permissions as bit flags.
    /// </summary>
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
    /// Defines access condition flags for Mifare DESFire app creation keys (bit field values 0x00, 0xE0, 0xF0).
    /// </summary>
    [Flags]
    public enum AccessCondition_MifareDesfireAppCreation
    {
        ChangeKeyUsingMK = 0,
        ChangeKeyUsingKeyNo = 224,
        ChangeKeyFrozen = 240
    }

    /// <summary>
    /// Defines access conditions for Mifare Classic sector trailer operations.
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
    /// Identifies supported card types.
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
    /// Defines generic error identifiers for card operations.
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
    /// Defines error identifiers for key parsing and validation.
    /// </summary>
    public enum KEY_ERROR
    {
        KEY_IS_EMPTY,
        KEY_HAS_WRONG_LENGTH,
        KEY_HAS_WRONG_FORMAT,
        NO_ERROR
    };
}
