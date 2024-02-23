﻿using RFiDGear.DataAccessLayer;

using System.Collections.Generic;
using System;
using Org.BouncyCastle.Bcpg;

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

        public MifareDesfireChipModel(string uid, CARD_TYPE cardType, string sak, string rats)
        {
            UID = uid;
            CardType = cardType;
            SAK = sak;
            RATS = rats;
        }

        public MifareDesfireChipModel(string uid, CARD_TYPE cardType, string sak, string rats, string versionL4)
        {
            UID = uid;
            CardType = cardType;
            SAK = sak;
            RATS = rats;
            VersionL4 = versionL4;
        }

        public MifareDesfireChipModel(MifareDesfireChipModel genericChip)
        {
            UID = genericChip.UID;
            CardType = genericChip.CardType;
            SAK = genericChip.SAK;
            RATS = genericChip.RATS;
            VersionL4 = genericChip.VersionL4;
            Childs = genericChip.Childs;
        }

        public string SAK { get; set; }
        public string RATS { get; set; }
        public string VersionL4 { get; set; }

        public uint FreeMemory { get; set; }
    }
}