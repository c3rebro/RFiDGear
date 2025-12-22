using System;

namespace RFiDGear.Infrastructure.AccessControl
{
    /// <summary>
    /// Target block selection inside the MIFARE data explorer views.
    /// Cast these values to <see cref="byte"/> when writing access control bytes.
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
    /// Logical block selection for MIFARE Classic sector trailer editing.
    /// </summary>
    public enum SectorTrailer_DataBlock
    {
        Block0 = 0,
        Block1 = 1,
        Block2 = 2,
        BlockAll = 3
    }

    /// <summary>
    /// Access control flags for MIFARE Classic sector trailers.
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
    /// Access rules for individual slots in a MIFARE Classic sector trailer.
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
    /// Access rules for DESFire application creation or authentication.
    /// </summary>
    [Flags]
    public enum AccessCondition_MifareDesfireAppCreation : byte
    {
        ChangeKeyUsingMK = DESFireKeySettings.ChangeKeyWithMasterKey,
        ChangeKeyUsingKeyNo = DESFireKeySettings.ChangeKeyWithTargetedKeyNumber,
        ChangeKeyFrozen = DESFireKeySettings.ChangeKeyFrozen
    }

    /// <summary>
    /// DESFire key settings as defined by the NXP datasheet. These values are sent directly
    /// as configuration bytes during application creation or key updates. MIFARE-specific
    /// primitives are grouped in <see cref="RFiDGear.Infrastructure.MifareConstants"/> to
    /// keep related concerns together.
    /// </summary>
    [Flags]
    public enum DESFireKeySettings : byte
    {
        ChangeKeyWithMasterKey = 0x00,
        AllowChangeMasterKey = 0x01,
        AllowFreeListingWithoutMasterKey = 0x02,
        AllowFreeCreateDeleteWithoutMasterKey = 0x04,
        ConfigurationChangeable = 0x08,
        Default = AllowChangeMasterKey | AllowFreeListingWithoutMasterKey | ConfigurationChangeable,
        ChangeKeyWithTargetedKeyNumber = 0xE0,
        ChangeKeyFrozen = 0xF0,
        ChangeKeyMask = ChangeKeyFrozen
    }

    /// <summary>
    /// Binary representation of the MIFARE access bits used to build sector trailers.
    /// </summary>
    public struct AccessBits
    {
        public short c1;
        public short c2;
        public short c3;
    }

    /// <summary>
    /// Bundles access-bit triplets for the three data blocks and the sector trailer.
    /// </summary>
    public struct SectorAccessBits
    {
        public AccessBits d_data_block0_access_bits;
        public AccessBits d_data_block1_access_bits;
        public AccessBits d_data_block2_access_bits;
        public AccessBits d_sector_trailer_access_bits;
    }

    /// <summary>
    /// Helpers that ensure access-control flags are serialised with valid combinations
    /// before sending them to a reader.
    /// </summary>
    public static class AccessConditionValidation
    {
        private const DESFireKeySettings AllowedGeneralFlags = DESFireKeySettings.AllowChangeMasterKey
                                                              | DESFireKeySettings.AllowFreeListingWithoutMasterKey
                                                              | DESFireKeySettings.AllowFreeCreateDeleteWithoutMasterKey
                                                              | DESFireKeySettings.ConfigurationChangeable;

        /// <summary>
        /// Verifies that the supplied DESFire key settings contain only known flags and a valid
        /// change-key mode.
        /// </summary>
        /// <param name="settings">The flag combination to validate.</param>
        /// <param name="reason">When invalid, receives a human readable reason.</param>
        /// <returns><c>true</c> when the settings can be written to the card.</returns>
        public static bool IsValid(DESFireKeySettings settings, out string reason)
        {
            var unknownFlags = settings & ~(AllowedGeneralFlags | DESFireKeySettings.ChangeKeyMask);
            if (unknownFlags != 0)
            {
                reason = $"Unknown DESFire key setting bits: 0x{(byte)unknownFlags:X2}.";
                return false;
            }

            var changeKeyMode = settings & DESFireKeySettings.ChangeKeyMask;
            if (changeKeyMode != DESFireKeySettings.ChangeKeyWithMasterKey
                && changeKeyMode != DESFireKeySettings.ChangeKeyWithTargetedKeyNumber
                && changeKeyMode != DESFireKeySettings.ChangeKeyFrozen)
            {
                reason = $"Unsupported change-key mode bits: 0x{(byte)changeKeyMode:X2}.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if the provided settings contain illegal flag combinations.
        /// </summary>
        /// <param name="settings">The flag combination to validate.</param>
        public static void EnsureValid(DESFireKeySettings settings)
        {
            if (!IsValid(settings, out var reason))
            {
                throw new ArgumentException(reason, nameof(settings));
            }
        }

        /// <summary>
        /// Validates the simplified access-condition selector used by the UI when creating
        /// a DESFire application.
        /// </summary>
        public static bool IsValid(AccessCondition_MifareDesfireAppCreation accessCondition, out string reason)
        {
            return IsValid((DESFireKeySettings)accessCondition, out reason);
        }
    }
}
