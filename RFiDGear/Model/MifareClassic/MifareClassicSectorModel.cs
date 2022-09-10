using RFiDGear.DataAccessLayer;

using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Serialization;

namespace RFiDGear.Model
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
            get => encodeSectorTrailer(this);
            set => IsValidSectorTrailer = decodeSectorTrailer(value, this);
        }

        public bool IsValidSectorTrailer { get; private set; }
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
        public AccessCondition_MifareClassicSectorTrailer Write_KeyB { get => SectorAccessCondition.Read_KeyB; set => SectorAccessCondition.Read_KeyB = value; }

        public SectorAccessBits SAB { get => sab; set => sab = value; }
        private SectorAccessBits sab;

        public bool IsAuthenticated { get; set; }
        public short Cx { get => SectorAccessCondition.Cx; set => SectorAccessCondition.Cx = value; }

        #region Extensions

        /// <summary>
        /// turns a given byte or string sector trailer to a access bits selection
        /// </summary>
        /// <param name="st"></param>
        /// <param name="_sector"></param>
        /// <returns></returns>
        private bool decodeSectorTrailer(byte[] st, ref MifareClassicSectorModel _sector)
        {
            uint C1x, C2x;

            uint tmpAccessBitCx;

            if (CustomConverter.SectorTrailerHasWrongFormat(st))
            {
                _sector = null;
                return true;
            }

            #region getAccessBitsForSectorTrailer

            C1x = st[1];
            C2x = st[2];

            C1x &= 0xF0;
            C1x >>= 7;
            C1x &= 0x01;

            sab.d_sector_trailer_access_bits.c1 = (short)C1x;

            C2x >>= 2;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 1;
            tmpAccessBitCx &= 0x01;

            sab.d_sector_trailer_access_bits.c2 = (short)tmpAccessBitCx;

            C1x |= C2x;
            C2x >>= 3;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 2;
            tmpAccessBitCx &= 0x01;

            sab.d_sector_trailer_access_bits.c3 = (short)tmpAccessBitCx;

            C1x &= 0x03;
            C1x |= C2x;
            C1x &= 0x07; //now we have C1³ + C2³ + C3³ as integer in C1x see mifare manual
                         //
                         //            if (C1x == 4)
                         //                isTransportConfiguration = true;
                         //            else
                         //                isTransportConfiguration = false;

            _sector.SectorAccessCondition = sectorTrailer_AccessBits[(int)C1x];

            #endregion getAccessBitsForSectorTrailer

            #region getAccessBitsForDataBlock2

            C1x = st[1];
            C2x = st[2];

            C1x &= 0xF0;
            C1x >>= 6;
            C1x &= 0x01;

            sab.d_data_block2_access_bits.c1 = (short)C1x;

            C2x >>= 1;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 1;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block2_access_bits.c2 = (short)tmpAccessBitCx;

            C1x |= C2x;
            //C2 &= 0xF8;
            C2x >>= 3;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 2;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block2_access_bits.c3 = (short)tmpAccessBitCx;

            C1x &= 0x03;
            C1x |= C2x;
            C1x &= 0x07;

            DataBlock2.DataBlockAccessCondition = dataBlock_AccessBits[(int)C1x];

            #endregion getAccessBitsForDataBlock2

            #region getAccessBitsForDataBlock1

            C1x = st[1];
            C2x = st[2];

            C1x &= 0xF0;
            C1x >>= 5;
            C1x &= 0x01;

            sab.d_data_block1_access_bits.c1 = (short)C1x;

            C1x |= C2x;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 1;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block1_access_bits.c2 = (short)tmpAccessBitCx;

            C2x >>= 3;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 2;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block1_access_bits.c3 = (short)tmpAccessBitCx;

            C1x &= 0x03;
            C1x |= C2x;
            C1x &= 0x07;

            DataBlock1.DataBlockAccessCondition = dataBlock_AccessBits[(int)C1x];

            #endregion getAccessBitsForDataBlock1

            #region getAccessBitsForDataBlock0

            C1x = st[1];
            C2x = st[2];

            C1x &= 0xF0;
            C1x >>= 4;
            C1x &= 0x01;

            tmpAccessBitCx = C1x;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block0_access_bits.c1 = (short)C1x;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block0_access_bits.c2 = (short)C1x;

            C2x <<= 1;
            C1x |= C2x;
            C2x >>= 3;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 2;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block0_access_bits.c3 = (short)C1x;

            C2x &= 0xFC;
            C1x &= 0x03;
            C1x |= C2x;
            C1x &= 0x07;

            DataBlock0.DataBlockAccessCondition = dataBlock_AccessBits[(int)C1x];

            if (DataBlock0.DataBlockAccessCondition == DataBlock1.DataBlockAccessCondition
                && DataBlock1.DataBlockAccessCondition == DataBlock2.DataBlockAccessCondition)
            {
                DataBlockCombined.DataBlockAccessCondition = DataBlock0.DataBlockAccessCondition;
            }

            #endregion getAccessBitsForDataBlock0

            return false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="st"></param>
        /// <param name="_sector"></param>
        /// <returns></returns>
        private bool decodeSectorTrailer(string st, MifareClassicSectorModel _sector)
        {
            _ = new byte[255];

            string[] sectorTrailer = st.Split(new[] { ',', ';' });
            if (sectorTrailer.Count() != 3 ||
                !(CustomConverter.IsInHexFormat(sectorTrailer[1]) && sectorTrailer[1].Length == 8) ||
                !(CustomConverter.IsInHexFormat(sectorTrailer[0]) && sectorTrailer[0].Length == 12) ||
                !(CustomConverter.IsInHexFormat(sectorTrailer[2]) && sectorTrailer[2].Length == 12))
            {
                return true;
            }

            byte[] _bytes = CustomConverter.GetBytes(sectorTrailer[1], out int _);

            if (!decodeSectorTrailer(_bytes, ref _sector))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Converts a given selection for either sector
        /// access bits or datablock access bits to the equivalent 3 bytes sector trailer
        /// </summary>
        /// <param name="_sector"></param>
        /// <returns></returns>
        private string encodeSectorTrailer(MifareClassicSectorModel _sector)
        {
            byte[] st = new byte[4] { 0x00, 0x00, 0x00, 0xC3 };

            uint sectorAccessBitsIndex = (uint)_sector.Cx;
            uint dataBlock0AccessBitsIndex = (uint)_sector.DataBlock0.Cx;
            uint dataBlock1AccessBitsIndex = (uint)_sector.DataBlock1.Cx; 
            uint dataBlock2AccessBitsIndex = (uint)_sector.DataBlock2.Cx; 

            // DataBlock 0 = C1/0; C2/0; C3/0

            st[1] |= (byte)((dataBlock0AccessBitsIndex & 0x0001) << 4);   // C1/0
            st[2] |= (byte)((dataBlock0AccessBitsIndex & 0x0002) >> 1);   // C2/0
            st[2] |= (byte)((dataBlock0AccessBitsIndex & 0x0004) << 2);   // C3/0

            sab.d_data_block0_access_bits.c1 |= (short)(dataBlock0AccessBitsIndex & 0x0001);
            sab.d_data_block0_access_bits.c2 |= (short)((dataBlock0AccessBitsIndex & 0x0002) >> 1);
            sab.d_data_block0_access_bits.c3 |= (short)((dataBlock0AccessBitsIndex & 0x0004) >> 2);

            // DataBlock 1 = C1/1; C2/1; C3/1

            st[1] |= (byte)((dataBlock1AccessBitsIndex & 0x01) << 5);   // C1/1
            st[2] |= (byte)(dataBlock1AccessBitsIndex & 0x02);          // C2/1
            st[2] |= (byte)((dataBlock1AccessBitsIndex & 0x04) << 3);   // C3/1

            sab.d_data_block1_access_bits.c1 |= (short)(dataBlock1AccessBitsIndex & 0x01);
            sab.d_data_block1_access_bits.c2 |= (short)((dataBlock1AccessBitsIndex & 0x02) >> 1);
            sab.d_data_block1_access_bits.c3 |= (short)((dataBlock1AccessBitsIndex & 0x04) >> 2);

            // DataBlock 2 = C1/2; C2/2; C3/2

            st[1] |= (byte)((dataBlock2AccessBitsIndex & 0x01) << 6);   // C1/2
            st[2] |= (byte)((dataBlock2AccessBitsIndex & 0x02) << 1);   // C2/2
            st[2] |= (byte)((dataBlock2AccessBitsIndex & 0x04) << 4);   // C3/2

            sab.d_data_block2_access_bits.c1 |= (short)(dataBlock2AccessBitsIndex & 0x01);
            sab.d_data_block2_access_bits.c2 |= (short)((dataBlock2AccessBitsIndex & 0x02) >> 1);
            sab.d_data_block2_access_bits.c3 |= (short)((dataBlock2AccessBitsIndex & 0x04) >> 2);

            // SectorAccessBits = C1/3; C2/3; C3/3

            st[1] |= (byte)((sectorAccessBitsIndex & 0x01) << 7);   // C1/3
            st[2] |= (byte)((sectorAccessBitsIndex & 0x02) << 2);   // C2/3
            st[2] |= (byte)((sectorAccessBitsIndex & 0x04) << 5);   // C3/3

            sab.d_sector_trailer_access_bits.c1 |= (short)(sectorAccessBitsIndex & 0x01);
            sab.d_sector_trailer_access_bits.c2 |= (short)((sectorAccessBitsIndex & 0x02) >> 1);
            sab.d_sector_trailer_access_bits.c3 |= (short)((sectorAccessBitsIndex & 0x04) >> 2);

            st = CustomConverter.buildSectorTrailerInvNibble(st);
            string[] stAsString;

            stAsString = new string[] { "FFFFFFFFFFFF", "FF0780C3", "FFFFFFFFFFFF" };

            stAsString[1] = CustomConverter.HexToString(st);
            return string.Join(",", stAsString);
        }


        #endregion Extensions
    }
}