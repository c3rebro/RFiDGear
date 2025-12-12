using System;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// MIFARE-specific constants and helper types extracted from <see cref="Constants"/>
    /// to keep the generic infrastructure definitions concise.
    /// </summary>
    public static class MifareConstants
    {
    }

    /// <summary>
    /// Known default DESFire key roles that can be pre-seeded on a card.
    /// </summary>
    public enum KeyType_MifareDesFireKeyType
    {
        DefaultDesfireCardCardMasterKey,
        DefaultDesfireCardApplicationMasterKey,
        DefaultDesfireCardReadKey,
        DefaultDesfireCardWriteKey
    }

    /// <summary>
    /// Supported DESFire key cryptographic types.
    /// </summary>
    [Flags]
    public enum DESFireKeyType
    {
        DF_KEY_DES = 0,
        DF_KEY_3K3DES = 64,
        DF_KEY_AES = 128
    }

    /// <summary>
    /// Container describing the metadata for a DESFire file.
    /// </summary>
    public class DESFireFileSettings
    {
        /// <summary>
        /// Access rights encoded for the underlying DESFire file.
        /// </summary>
        public byte[] accessRights;

        /// <summary>
        /// The file type as defined by the DESFire specification.
        /// </summary>
        public byte FileType;

        /// <summary>
        /// Communication settings (plain, MACed, encrypted).
        /// </summary>
        public byte comSett;

        /// <summary>
        /// Additional settings when the file is a data file.
        /// </summary>
        public DataFileSetting dataFile;

        /// <summary>
        /// Additional settings when the file is a record file.
        /// </summary>
        public RecordFileSetting recordFile;

        /// <summary>
        /// Additional settings when the file is a value file.
        /// </summary>
        public ValueFileSetting valueFile;
    }

    /// <summary>
    /// Size information for a DESFire data file.
    /// </summary>
    public struct DataFileSetting
    {
        /// <summary>
        /// The size of the data file in bytes.
        /// </summary>
        public uint fileSize;
    }

    /// <summary>
    /// Placeholder for record-file-specific settings.
    /// </summary>
    public struct RecordFileSetting
    {
    }

    /// <summary>
    /// Placeholder for value-file-specific settings.
    /// </summary>
    public struct ValueFileSetting
    {
    }

    /// <summary>
    /// Access rights used when applying tasks to DESFire files.
    /// </summary>
    [Flags]
    public enum TaskAccessRights
    {
        AR_KEY0 = 0,
        AR_KEY1 = 1,
        AR_KEY2 = 2,
        AR_KEY3 = 3,
        AR_KEY4 = 4,
        AR_KEY5 = 5,
        AR_KEY6 = 6,
        AR_KEY7 = 7,
        AR_KEY8 = 8,
        AR_KEY9 = 9,
        AR_KEY10 = 10,
        AR_KEY11 = 11,
        AR_KEY12 = 12,
        AR_KEY13 = 13,
        AR_FREE = 14,
        AR_NEVER = 15
    }

    /// <summary>
    /// Human readable breakdown of DESFire access rights for a file.
    /// </summary>
    public struct DESFireAccessRights
    {
        /// <summary>
        /// Which key grants read access.
        /// </summary>
        public TaskAccessRights readAccess;

        /// <summary>
        /// Which key grants write access.
        /// </summary>
        public TaskAccessRights writeAccess;

        /// <summary>
        /// Which key grants change access.
        /// </summary>
        public TaskAccessRights changeAccess;

        /// <summary>
        /// Which key grants read and write access.
        /// </summary>
        public TaskAccessRights readAndWriteAccess;
    }

    /// <summary>
    /// Communication mode flags for DESFire operations.
    /// </summary>
    [Flags]
    public enum EncryptionMode
    {
        CM_PLAIN = 0,
        CM_MAC = 1,
        CM_ENCRYPT = 3,
        CM_UNKNOWN = 255
    }

    /// <summary>
    /// Default key definitions used for DESFire operations (e.g., test keys).
    /// </summary>
    public struct MifareDesfireDefaultKeys
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MifareDesfireDefaultKeys"/> struct.
        /// </summary>
        /// <param name="_keyType">The logical role of the key.</param>
        /// <param name="_encryptionType">The encryption algorithm used by the key.</param>
        /// <param name="_key">The key value represented as a string.</param>
        public MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType _keyType, DESFireKeyType _encryptionType, string _key)
        {
            KeyType = _keyType;
            EncryptionType = _encryptionType;
            Key = _key;
        }

        /// <summary>
        /// The logical role of the key.
        /// </summary>
        public KeyType_MifareDesFireKeyType KeyType;

        /// <summary>
        /// The encryption algorithm used by the key.
        /// </summary>
        public DESFireKeyType EncryptionType;

        /// <summary>
        /// The key value represented as a string.
        /// </summary>
        public string Key;
    }

    /// <summary>
    /// Default key definitions used for MIFARE Classic operations.
    /// </summary>
    public struct MifareClassicDefaultKeys
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MifareClassicDefaultKeys"/> struct.
        /// </summary>
        /// <param name="_keyNumber">The logical key slot.</param>
        /// <param name="_accessBits">The access bits string.</param>
        public MifareClassicDefaultKeys(int _keyNumber, string _accessBits)
        {
            KeyNumber = _keyNumber;
            accessBits = _accessBits;
        }

        private string accessBits;

        /// <summary>
        /// The logical key slot.
        /// </summary>
        public int KeyNumber;

        /// <summary>
        /// The access bits string.
        /// </summary>
        public string AccessBits { get { return accessBits; } set { accessBits = value; } }
    }
}
