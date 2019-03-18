using RFiDGear.DataAccessLayer;
using System.Collections.Generic;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipUid.
    /// </summary>
    public class MifareDesfireChipModel
    {
        private readonly List<MifareDesfireAppModel> _appList = new List<MifareDesfireAppModel>();

        public List<MifareDesfireAppModel> AppList
        {
            get { return _appList; }
        }

        public MifareDesfireChipModel()
        {
        }

        public MifareDesfireChipModel(string uid, CARD_TYPE cardType)
        {
            UidNumber = uid;
            CardType = cardType;
        }

        public string UidNumber { get; set; }

        public CARD_TYPE CardType { get; set; }
    }
}