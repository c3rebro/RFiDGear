using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Classic;
using System;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Serialization;

namespace RFiDGear.Models
{
    /// <summary>
    /// Description of chipMifareClassicSector.
    /// </summary>
    [XmlRootAttribute("MifareClassicSectorNode", IsNullable = false)]
    public class MifareClassicSectorModel
    {
        private ObservableCollection<MifareClassicDataBlockAccessConditionModel> dataBlock_AccessBits = new ObservableCollection<MifareClassicDataBlockAccessConditionModel>
            (new[]
             {
                 new MifareClassicDataBlockAccessConditionModel(0,
                                                                SectorTrailer_DataBlock.BlockAll,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB),

                 new MifareClassicDataBlockAccessConditionModel(1,
                                                                SectorTrailer_DataBlock.BlockAll,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicDataBlockAccessConditionModel(2,
                                                                SectorTrailer_DataBlock.BlockAll,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicDataBlockAccessConditionModel(3,
                                                                SectorTrailer_DataBlock.BlockAll,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB),

                 new MifareClassicDataBlockAccessConditionModel(4,
                                                                SectorTrailer_DataBlock.BlockAll,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB),

                 new MifareClassicDataBlockAccessConditionModel(5,
                                                                SectorTrailer_DataBlock.BlockAll,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicDataBlockAccessConditionModel(6,
                                                                SectorTrailer_DataBlock.BlockAll,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicDataBlockAccessConditionModel(7,
                                                                SectorTrailer_DataBlock.BlockAll,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                                AccessCondition_MifareClassicSectorTrailer.NotAllowed)
             });

        private ObservableCollection<MifareClassicSectorAccessConditionModel> sectorTrailer_AccessBits = new ObservableCollection<MifareClassicSectorAccessConditionModel>
            (new[]
             {
                 new MifareClassicSectorAccessConditionModel(0,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA),

                 new MifareClassicSectorAccessConditionModel(1,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB),

                 new MifareClassicSectorAccessConditionModel(2,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicSectorAccessConditionModel(3,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicSectorAccessConditionModel(4,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA),

                 new MifareClassicSectorAccessConditionModel(5,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicSectorAccessConditionModel(6,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB),

                 new MifareClassicSectorAccessConditionModel(7,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                             AccessCondition_MifareClassicSectorTrailer.NotAllowed)
             });

        public MifareClassicSectorModel()
        {
            SectorAccessCondition = new MifareClassicSectorAccessConditionModel();
            DataBlock = new ObservableCollection<MifareClassicDataBlockModel>();

            DataBlock0 = new MifareClassicDataBlockModel();
            DataBlock1 = new MifareClassicDataBlockModel();
            DataBlock2 = new MifareClassicDataBlockModel();
            DataBlockCombined = new MifareClassicDataBlockModel();

        }

        public MifareClassicSectorModel(short _cx,
                                        AccessCondition_MifareClassicSectorTrailer _readKeyA,
                                        AccessCondition_MifareClassicSectorTrailer _writeKeyA,
                                        AccessCondition_MifareClassicSectorTrailer _readAccessCondition,
                                        AccessCondition_MifareClassicSectorTrailer _writeAccessCondition,
                                        AccessCondition_MifareClassicSectorTrailer _readKeyB,
                                        AccessCondition_MifareClassicSectorTrailer _writeKeyB)
        {
            SectorAccessCondition = new MifareClassicSectorAccessConditionModel();
            DataBlock = new ObservableCollection<MifareClassicDataBlockModel>();

            DataBlock0 = new MifareClassicDataBlockModel();
            DataBlock1 = new MifareClassicDataBlockModel();
            DataBlock2 = new MifareClassicDataBlockModel();
            DataBlockCombined = new MifareClassicDataBlockModel();

            Cx = _cx;

            Read_KeyA = _readKeyA;
            Write_KeyA = _writeKeyA;

            Read_AccessCondition_MifareClassicSectorTrailer = _readAccessCondition;
            Write_AccessCondition_MifareClassicSectorTrailer = _writeAccessCondition;

            Read_KeyB = _readKeyB;
            Write_KeyB = _writeKeyB;
        }

        public MifareClassicSectorModel(int _sectorNumber) : this(_sectorNumber, "ff ff ff ff ff ff", "FF0780C3" , "ff ff ff ff ff ff")
        {
        }

        public MifareClassicSectorModel(
        int _sectorNumber,
        string _keyA, 
        string _accessBitsAsString, 
        string _keyB)
        {
            SectorAccessCondition = new MifareClassicSectorAccessConditionModel();
            DataBlock = new ObservableCollection<MifareClassicDataBlockModel>();

            DataBlock0 = new MifareClassicDataBlockModel();
            DataBlock1 = new MifareClassicDataBlockModel();
            DataBlock2 = new MifareClassicDataBlockModel();
            DataBlockCombined = new MifareClassicDataBlockModel();

            SectorNumber = _sectorNumber;
            KeyA = _keyA;
            AccessBitsAsString = _accessBitsAsString;

            KeyB = _keyB;
        }

        public string AccessBitsAsString
        {
            get => EncodeSectorTrailer();
            set => IsValidSectorTrailer = TryDecodeSectorTrailer(value);
        }

        /// <summary>Gets a value that says whether the last text that was set as the access bits was taken (see <see cref="AccessBitsAsString"/>).</summary>
        public bool IsValidSectorTrailer { get; private set; }

        /// <summary>Gets or sets the general purpose byte of the sector trailer; it is what the text of the sector trailer says (not part of a project).</summary>
        [XmlIgnore]
        public byte GeneralPurposeByte { get; set; } = 0xC3;
        public int SectorNumber { get; set; }
        public string KeyA { get; set; }
        public string KeyB { get; set; }
        public ObservableCollection<MifareClassicDataBlockModel> DataBlock { get; set; }

        public MifareClassicDataBlockModel DataBlock0 { get; set; }
        public MifareClassicDataBlockModel DataBlock1 { get; set; }
        public MifareClassicDataBlockModel DataBlock2 { get; set; }
        public MifareClassicDataBlockModel DataBlockCombined { get; set; }

        public MifareClassicSectorAccessConditionModel SectorAccessCondition { get; set; }

        public AccessCondition_MifareClassicSectorTrailer Read_KeyA { get => SectorAccessCondition.Read_KeyA; set => SectorAccessCondition.Read_KeyA = value; }
        public AccessCondition_MifareClassicSectorTrailer Write_KeyA { get => SectorAccessCondition.Write_KeyA; set => SectorAccessCondition.Write_KeyA = value; }
        public AccessCondition_MifareClassicSectorTrailer Read_AccessCondition_MifareClassicSectorTrailer { get => SectorAccessCondition.Read_AccessCondition_MifareClassicSectorTrailer; set => SectorAccessCondition.Read_AccessCondition_MifareClassicSectorTrailer = value; }
        public AccessCondition_MifareClassicSectorTrailer Write_AccessCondition_MifareClassicSectorTrailer { get => SectorAccessCondition.Write_AccessCondition_MifareClassicSectorTrailer; set => SectorAccessCondition.Write_AccessCondition_MifareClassicSectorTrailer = value; }
        public AccessCondition_MifareClassicSectorTrailer Read_KeyB { get => SectorAccessCondition.Read_KeyB; set => SectorAccessCondition.Read_KeyB = value; }
        public AccessCondition_MifareClassicSectorTrailer Write_KeyB { get => SectorAccessCondition.Write_KeyB; set => SectorAccessCondition.Write_KeyB = value; }

        /// <summary>
        /// Gets the access bits as one condition per group in the shape that the readers take: each is made from the conditions that the dialog holds now
        /// (and from nothing that an earlier call left behind), and all zero when they are not usable (that means: no access bits given). There is no setter:
        /// the conditions are set through the groups, the text or the properties of the groups.
        /// </summary>
        public SectorAccessBits SAB
        {
            get
            {
                var bits = ToAccessBits();

                return bits == null
                    ? default
                    : new SectorAccessBits
                    {
                        d_data_block0_access_bits = ToAccessBitsOfGroup(bits.GetCondition(0)),
                        d_data_block1_access_bits = ToAccessBitsOfGroup(bits.GetCondition(1)),
                        d_data_block2_access_bits = ToAccessBitsOfGroup(bits.GetCondition(2)),
                        d_sector_trailer_access_bits = ToAccessBitsOfGroup(bits.GetCondition(3))
                    };
            }
        }

        private static AccessBits ToAccessBitsOfGroup(int condition) =>
            new AccessBits { c1 = (short)(condition & 1), c2 = (short)((condition >> 1) & 1), c3 = (short)((condition >> 2) & 1) };

        public bool IsAuthenticated { get; set; }
        public short Cx { get => SectorAccessCondition.Cx; set => SectorAccessCondition.Cx = value; }

        #region Extensions

        /// <summary>
        /// Takes the access bits from the text of a sector trailer: <c>KeyA,AccessBits,KeyB</c> (the keys 12 hexadecimal digits each, the access
        /// bits the three bytes of the trailer and the general purpose byte, 8 digits) or the 8 digits alone. The conditions of the dialog are set
        /// from them. A text that is none of this, or whose access bits are not the inverse of their copies, changes nothing.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns><c>true</c> when the text was taken.</returns>
        private bool TryDecodeSectorTrailer(string text)
        {
            if (text == null)
            {
                return false;
            }

            var parts = text.Split(new[] { ',', ';' });
            string accessBytesText;

            if (parts.Length == 3 && IsHexOfLength(parts[0], 12) && IsHexOfLength(parts[2], 12))
            {
                accessBytesText = parts[1];
            }
            else if (parts.Length == 1)
            {
                accessBytesText = parts[0];
            }
            else
            {
                return false;
            }

            if (!IsHexOfLength(accessBytesText, 8))
            {
                return false;
            }

            var bytes = Convert.FromHexString(accessBytesText);

            if (!ClassicAccessBits.TryFromBytes(bytes.AsSpan(0, 3), out var bits))
            {
                return false;
            }

            DataBlock0.DataBlockAccessCondition = dataBlock_AccessBits[bits.GetCondition(0)];
            DataBlock1.DataBlockAccessCondition = dataBlock_AccessBits[bits.GetCondition(1)];
            DataBlock2.DataBlockAccessCondition = dataBlock_AccessBits[bits.GetCondition(2)];
            SectorAccessCondition = sectorTrailer_AccessBits[bits.GetCondition(3)];

            if (bits.GetCondition(0) == bits.GetCondition(1) && bits.GetCondition(1) == bits.GetCondition(2))
            {
                DataBlockCombined.DataBlockAccessCondition = DataBlock0.DataBlockAccessCondition;
            }

            GeneralPurposeByte = bytes[3];

            return true;
        }

        private static bool IsHexOfLength(string text, int length) =>
            text != null && text.Length == length && text.All(Uri.IsHexDigit);

        /// <summary>
        /// The access bits that the dialog holds: the conditions of the three groups of data blocks and of the trailer.
        /// </summary>
        /// <returns>The access bits, or <c>null</c> when a condition is not 0 to 7.</returns>
        private ClassicAccessBits ToAccessBits()
        {
            var conditions = new[] { (int)DataBlock0.Cx, (int)DataBlock1.Cx, (int)DataBlock2.Cx, (int)Cx };

            return conditions.All(condition => condition >= 0 && condition <= 7)
                ? new ClassicAccessBits(conditions[0], conditions[1], conditions[2], conditions[3])
                : null;
        }

        /// <summary>
        /// Makes the text of the sector trailer from the conditions that the dialog holds: <c>KeyA,AccessBits,KeyB</c> with the access bits as
        /// the chip wants them (every bit followed by its inverse) and the general purpose byte. The keys of the text are not the keys of the task.
        /// </summary>
        /// <returns>The text.</returns>
        private string EncodeSectorTrailer()
        {
            var bits = ToAccessBits() ?? ClassicAccessBits.Transport;

            return string.Join(",", "FFFFFFFFFFFF", Convert.ToHexString(bits.ToBytes()) + GeneralPurposeByte.ToString("X2", CultureInfo.InvariantCulture), "FFFFFFFFFFFF");
        }


        #endregion Extensions
    }
}