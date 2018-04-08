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
        public MifareClassicSectorModel()
        {
            DataBlock = new ObservableCollection<MifareClassicDataBlockModel>();
        }

        public MifareClassicSectorModel(short _cx,
            AccessCondition_MifareClassicSectorTrailer _readKeyA,
            AccessCondition_MifareClassicSectorTrailer _writeKeyA,
            AccessCondition_MifareClassicSectorTrailer _readAccessCondition,
            AccessCondition_MifareClassicSectorTrailer _writeAccessCondition,
            AccessCondition_MifareClassicSectorTrailer _readKeyB,
            AccessCondition_MifareClassicSectorTrailer _writeKeyB)
        {
            DataBlock = new ObservableCollection<MifareClassicDataBlockModel>();

            Cx = _cx;

            Read_KeyA = _readKeyA;
            Write_KeyA = _writeKeyA;

            Read_AccessCondition_MifareClassicSectorTrailer = _readAccessCondition;
            Write_AccessCondition_MifareClassicSectorTrailer = _writeAccessCondition;

            Read_KeyB = _readKeyB;
            Write_KeyB = _writeKeyB;
        }

        public MifareClassicSectorModel(
            int _sectorNumber = 0,
            string _keyA = "ff ff ff ff ff ff", // optional parameter: if not specified use transport configuration
            string _accessBitsAsString = "FF0780C3", // optional parameter: if not specified use transport configuration
            string _keyB = "ff ff ff ff ff ff") // optional parameter: if not specified use transport configuration
        {
            DataBlock = new ObservableCollection<MifareClassicDataBlockModel>();

            SectorNumber = _sectorNumber;
            KeyA = _keyA;
            AccessBitsAsString = _accessBitsAsString;

            KeyB = _keyB;
        }

        public ObservableCollection<MifareClassicDataBlockModel> DataBlock { get; set; }

        public int SectorNumber { get; set; }
        public string KeyA { get; set; }
        public string AccessBitsAsString { get; set; }
        public string KeyB { get; set; }

        public AccessCondition_MifareClassicSectorTrailer Read_KeyA { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Write_KeyA { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Read_AccessCondition_MifareClassicSectorTrailer { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Write_AccessCondition_MifareClassicSectorTrailer { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Read_KeyB { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Write_KeyB { get; set; }

        public bool IsAuthenticated { get; set; }
        public short Cx { get; set; }
    }
}