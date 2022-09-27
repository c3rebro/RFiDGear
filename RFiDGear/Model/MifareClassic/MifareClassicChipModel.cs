using RFiDGear.DataAccessLayer;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipMifareClassicUid.
    /// </summary>
    [XmlRootAttribute("MifareClassicChipNode", IsNullable = false)]
    public class MifareClassicChipModel : GenericChipModel
    {
        private readonly List<MifareClassicSectorModel> _sectorList = new List<MifareClassicSectorModel>();

        public List<MifareClassicSectorModel> SectorList => _sectorList;

        public MifareClassicChipModel()
        {
        }

        public MifareClassicChipModel(string uid, CARD_TYPE cardType)
        {
            CardType = cardType;
            UID = uid;
        }

        public uint FreeMemory { get; set; }
    }
}