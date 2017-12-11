using RFiDGear.DataAccessLayer;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipMifareClassicSector.
    /// </summary>
    [XmlRootAttribute("MifareClassicSectorNode", IsNullable = false)]
    public class MifareClassicSectorModel
    {
        private string keyA;

        //private byte[] accessBitsAsByte;
        private string accessBitsAsString;

        private string keyB;
        private bool isAuthenticated;

        private uint cx;

        private AccessCondition_MifareClassicSectorTrailer read_KeyA;
        private AccessCondition_MifareClassicSectorTrailer write_KeyA;
        private AccessCondition_MifareClassicSectorTrailer read_AccessCondition_MifareClassicSectorTrailer;
        private AccessCondition_MifareClassicSectorTrailer write_AccessCondition_MifareClassicSectorTrailer;
        private AccessCondition_MifareClassicSectorTrailer read_KeyB;
        private AccessCondition_MifareClassicSectorTrailer write_KeyB;

        private ObservableCollection<MifareClassicDataBlockModel> mifareClassicBlock;

        public MifareClassicSectorModel()
        {
            mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();
        }

        public MifareClassicSectorModel(short _c1x, short _c2x, short _c3x)
        {
            mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();

            cx = 0;
            cx |= (uint)_c3x;
            cx <<= 1;
            cx |= (uint)_c2x;
            cx <<= 1;
            cx |= (uint)_c1x;

            read_KeyA = AccessCondition_MifareClassicSectorTrailer.NotApplicable;
            write_KeyA = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
            read_AccessCondition_MifareClassicSectorTrailer = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
            write_AccessCondition_MifareClassicSectorTrailer = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
            read_KeyB = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
            write_KeyB = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
            //this = sectorTrailer_AccessBits[(int)cx];
        }

        public MifareClassicSectorModel(uint _cx,
            AccessCondition_MifareClassicSectorTrailer _readKeyA = AccessCondition_MifareClassicSectorTrailer.NotApplicable,
            AccessCondition_MifareClassicSectorTrailer _writeKeyA = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
            AccessCondition_MifareClassicSectorTrailer _readAccessCondition = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
            AccessCondition_MifareClassicSectorTrailer _writeAccessCondition = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
            AccessCondition_MifareClassicSectorTrailer _readKeyB = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
            AccessCondition_MifareClassicSectorTrailer _writeKeyB = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA)
        {
            mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();

            cx = _cx;

            read_KeyA = _readKeyA;
            write_KeyA = _writeKeyA;

            read_AccessCondition_MifareClassicSectorTrailer = _readAccessCondition;
            write_AccessCondition_MifareClassicSectorTrailer = _writeAccessCondition;

            read_KeyB = _readKeyB;
            write_KeyB = _writeKeyB;
        }

        public MifareClassicSectorModel(int _sectorNumber)
        {
            mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();

            sectorNumber = _sectorNumber;
        }

        public MifareClassicSectorModel(
            int _sectorNumber = 0,
            string _keyA = "ff ff ff ff ff ff", // optional parameter: if not specified use transport configuration
            string _accessBitsAsString = "FF0780C3", // optional parameter: if not specified use transport configuration
            string _keyB = "ff ff ff ff ff ff") // optional parameter: if not specified use transport configuration
        {
            mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();

            sectorNumber = _sectorNumber;
            keyA = _keyA;
            accessBitsAsString = _accessBitsAsString;

            keyB = _keyB;
        }

        public ObservableCollection<MifareClassicDataBlockModel> DataBlock
        {
            get { return mifareClassicBlock; }
            set { mifareClassicBlock = value; }
        }

        private int sectorNumber;

        public int SectorNumber { get { return sectorNumber; } set { sectorNumber = value; } }
        public string KeyA { get { return keyA; } set { keyA = value; } }
        public string AccessBitsAsString { get { return accessBitsAsString; } set { accessBitsAsString = value; } }

        //public byte[] AccessBitsAsByte { get { return accessBitsAsByte; }}
        public string KeyB { get { return keyB; } set { keyB = value; } }

        public AccessCondition_MifareClassicSectorTrailer Read_KeyA { get { return read_KeyA; } set { read_KeyA = value; } }
        public AccessCondition_MifareClassicSectorTrailer Write_KeyA { get { return write_KeyA; } set { write_KeyA = value; } }
        public AccessCondition_MifareClassicSectorTrailer Read_AccessCondition_MifareClassicSectorTrailer { get { return read_AccessCondition_MifareClassicSectorTrailer; } set { read_AccessCondition_MifareClassicSectorTrailer = value; } }
        public AccessCondition_MifareClassicSectorTrailer Write_AccessCondition_MifareClassicSectorTrailer { get { return write_AccessCondition_MifareClassicSectorTrailer; } set { write_AccessCondition_MifareClassicSectorTrailer = value; } }
        public AccessCondition_MifareClassicSectorTrailer Read_KeyB { get { return read_KeyB; } set { read_KeyB = value; } }
        public AccessCondition_MifareClassicSectorTrailer Write_KeyB { get { return write_KeyB; } set { write_KeyB = value; } }

        public bool IsAuthenticated { get { return isAuthenticated; } set { isAuthenticated = value; } }
        public uint Cx { get { return cx; } set { cx = value; } }
    }

    //	public struct Sector_AccessCondition
    //	{
    //		public Sector_AccessCondition(
    //			AccessCondition_MifareClassicSectorTrailer _readKeyA = AccessCondition_MifareClassicSectorTrailer.NotApplicable,
    //			AccessCondition_MifareClassicSectorTrailer _writeKeyA = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
    //			AccessCondition_MifareClassicSectorTrailer _readAccessCondition = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
    //			AccessCondition_MifareClassicSectorTrailer _writeAccessCondition = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
    //			AccessCondition_MifareClassicSectorTrailer _readKeyB = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
    //			AccessCondition_MifareClassicSectorTrailer _writeKeyB = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA)
    //		{
    //			cx = 0;
    //
    //			read_KeyA =  _readKeyA;
    //			write_KeyA = _writeKeyA;
    //
    //			read_AccessCondition_MifareClassicSectorTrailer = _readAccessCondition;
    //			write_AccessCondition_MifareClassicSectorTrailer = _writeAccessCondition;
    //
    //			read_KeyB = _readKeyB;
    //			write_KeyB = _writeKeyB;
    //		}
    //
    //		public Sector_AccessCondition(short _c1x, short _c2x, short _c3x)
    //		{
    //			cx = 0;
    //			cx |= (uint)_c3x;
    //			cx <<= 1;
    //			cx |= (uint)_c2x;
    //			cx <<= 1;
    //			cx |= (uint)_c1x;
    //
    //			read_KeyA = AccessCondition_MifareClassicSectorTrailer.NotApplicable;
    //			write_KeyA = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
    //			read_AccessCondition_MifareClassicSectorTrailer = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
    //			write_AccessCondition_MifareClassicSectorTrailer = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
    //			read_KeyB = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
    //			write_KeyB = AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA;
    //			//this = sectorTrailer_AccessBits[(int)cx];
    //		}
    //
    //		private uint cx;
    //
    //		private AccessCondition_MifareClassicSectorTrailer read_KeyA;
    //		private AccessCondition_MifareClassicSectorTrailer write_KeyA;
    //		private AccessCondition_MifareClassicSectorTrailer read_AccessCondition_MifareClassicSectorTrailer;
    //		private AccessCondition_MifareClassicSectorTrailer write_AccessCondition_MifareClassicSectorTrailer;
    //		private AccessCondition_MifareClassicSectorTrailer read_KeyB;
    //		private AccessCondition_MifareClassicSectorTrailer write_KeyB;
    //
    //		public AccessCondition_MifareClassicSectorTrailer Read_KeyA { get { return read_KeyA; }}
    //		public AccessCondition_MifareClassicSectorTrailer Write_KeyA { get { return write_KeyA; }}
    //		public AccessCondition_MifareClassicSectorTrailer Read_AccessCondition_MifareClassicSectorTrailer { get { return read_AccessCondition_MifareClassicSectorTrailer; }}
    //		public AccessCondition_MifareClassicSectorTrailer Write_AccessCondition_MifareClassicSectorTrailer { get { return write_AccessCondition_MifareClassicSectorTrailer; }}
    //		public AccessCondition_MifareClassicSectorTrailer Read_KeyB { get { return read_KeyB; }}
    //		public AccessCondition_MifareClassicSectorTrailer Write_KeyB { get { return write_KeyB; }}
    //
    //		public uint Cx { get { return cx; } set { cx = value; }}
    //	}
}