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
    	public const int MAX_WAIT_INSERTION = 200; //timeout for chip response in ms
		public const string TITLE_SUFFIX = "DEVELOPER PREVIEW"; //turns out special app versions
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
    ///
    /// </summary>
    public struct CARD_INFO
    {
        public CARD_INFO(CARD_TYPE _type, string _uid)
        {
            CardType = _type;
            uid = _uid;
        }

        public string uid;
        public CARD_TYPE CardType;
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