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
            get => _appList;
            set => _appList = value;
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

        public MifareDesfireChipModel(GenericChipModel genericChip)
        {
            UID = genericChip.UID;
            CardType = genericChip.CardType;
        }

        //TODO: Clean this Mess up: Generic Chip should not have a "free memory" property
        public uint FreeMemory { get; set; }
    }
}