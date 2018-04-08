using RFiDGear.DataAccessLayer;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipMifareClassicUid.
    /// </summary>
    [XmlRootAttribute("MifareClassicChipNode", IsNullable = false)]
    public class MifareClassicChipModel
    {
        private readonly List<MifareClassicSectorModel> _sectorList = new List<MifareClassicSectorModel>();

        public List<MifareClassicSectorModel> SectorList
        {
            get { return _sectorList; }
        }

        public MifareClassicChipModel()
        {
        }

        public MifareClassicChipModel(string uid, CARD_TYPE cardType)
        {
            this.CardType = cardType;
            this.UidNumber = uid;
        }

        public string UidNumber { get; set; }

        public CARD_TYPE CardType { get; set; }
    }
}