using Elatec.NET;
using RFiDGear.DataAccessLayer;
using System.Collections.Generic;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipUid.
    /// </summary>
    public class GenericChipModel
    {
        public GenericChipModel()
        {

        }

        public GenericChipModel(string uid, CARD_TYPE cardType)
        {
            UID = uid;
            CardType = cardType;
        }

        public GenericChipModel(string uid, CARD_TYPE cardType, string sak, string rats)
        {
            UID = uid;
            CardType = cardType;
            SAK = sak;
            RATS = rats;
        }

        public GenericChipModel(string uid, CARD_TYPE cardType, string sak, string rats, string versionL4)
        {
            UID = uid;
            CardType = cardType;
            SAK = sak;
            RATS = rats;
            VersionL4 = versionL4;
        }

        public string UID { get; set; }
        public CARD_TYPE CardType { get; set; }
        public string SAK { get; set; }
        public string RATS { get; set; }
        public string VersionL4 { get; set; }
        public GenericChipModel Child { get; set; }
        public GenericChipModel GrandChild  {  get; set;  }
    }
}