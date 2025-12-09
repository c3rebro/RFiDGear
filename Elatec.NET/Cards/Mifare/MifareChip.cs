namespace Elatec.NET.Cards.Mifare
{
    /// <summary>
    /// A MIFARE chip.
    /// </summary>
    public class MifareChip : BaseChip
    {
        /// <summary>
        /// Answer To Request TypeA
        /// </summary>
        public ushort ATQA { get; set; }

        /// <summary>
        /// Select Acknowledge
        /// </summary>
        public byte SAK { get; set; }

        /// <summary>
        /// Answer To Select
        /// </summary>
        public byte[] ATS { get; set; }

        public byte[] VersionL4 { get; set; }
        public MifareChipSubType SubType { get; set; }

        public MifareChip()
        {
        }

        public MifareChip(ChipType chipType, byte[] uid)
        {
            UID = uid;
            ChipType = chipType;
        }

        public MifareChip(ChipType chipType, byte[] uid, byte sak, byte[] ats)
        {
            UID = uid;
            ChipType = chipType;
            SAK = sak;
            ATS = ats;
        }

        public MifareChip(ChipType chipType, byte[] uid, byte sak, byte[] ats, byte[] versionL4)
        {
            UID = uid;
            ChipType = chipType;
            SAK = sak;
            ATS = ats;
            VersionL4 = versionL4;
        }
    }
}