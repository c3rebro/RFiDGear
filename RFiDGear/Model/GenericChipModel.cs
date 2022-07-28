using Elatec.NET.DataAccessLayer;
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
        public uint FreeMemory { get; set; }

        public string UID { get; set; }

        public CARD_TYPE CardType { get; set; }
    }
}