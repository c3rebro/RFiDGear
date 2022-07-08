using RFiDGear.DataAccessLayer;
using System.Collections.Generic;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipUid.
    /// </summary>
    public class MifareDesfireChipModel : GenericChipModel
    {

        public List<MifareDesfireAppModel> AppList
        {
            get { return _appList; }
            set { _appList = value; }
        }
        private List<MifareDesfireAppModel> _appList;

        public MifareDesfireChipModel()
        {
        }

        public MifareDesfireChipModel(string uid, CARD_TYPE cardType)
        {
            UID = uid;
            CardType = cardType;
        }

        //public string UidNumber { get; set; }

        //public CARD_TYPE CardType { get; set; }
    }
}