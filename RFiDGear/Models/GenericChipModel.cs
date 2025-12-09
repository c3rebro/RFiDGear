using Elatec.NET;
using RFiDGear.Infrastructure;
using System.Collections.Generic;

namespace RFiDGear.Models
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

        public GenericChipModel(GenericChipModel chip)
        {
            this.UID = chip.UID;
            this.CardType = chip.CardType;
        }

        public string UID { get; set; }
        public CARD_TYPE CardType { get; set; }

        public bool? HasChilds => Childs?.Count > 0;
        
        public List<GenericChipModel> Childs { get; set; }
    }
}