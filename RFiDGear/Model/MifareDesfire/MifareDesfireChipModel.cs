using RFiDGear.DataAccessLayer;

using System.Collections.Generic;
using System;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipUID.
    /// </summary>
    public class MifareDesfireChipModel : GenericChipModel
    {
        public uint[] AppIDs 
        {
            get
            {
                var appIDs = new uint[AppList.Count];

                for(var i = 0; i < AppList.Count; i++)
                {
                    appIDs[i] = AppList[i].appID;
                }

                return appIDs;
            }
        }

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
            RATS = genericChip.RATS;
            SAK = genericChip.SAK;
            L4Version = genericChip.VersionL4;
            Child = genericChip.Child;
        }

        public string L4Version { get; set; }
        public uint FreeMemory { get; set; }
    }
}