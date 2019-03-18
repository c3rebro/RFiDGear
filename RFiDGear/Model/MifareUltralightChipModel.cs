/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 24.04.2018
 * Time: 21:31
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using RFiDGear.DataAccessLayer;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipMifareClassicUid.
    /// </summary>
    [XmlRootAttribute("MifareClassicChipNode", IsNullable = false)]
    public class MifareUltralightChipModel
    {
        private readonly List<MifareUltralightPageModel> _pageList = new List<MifareUltralightPageModel>();

        public List<MifareUltralightPageModel> PageList
        {
            get { return _pageList; }
        }

        public MifareUltralightChipModel()
        {
        }

        public MifareUltralightChipModel(string uid, CARD_TYPE cardType)
        {
            this.CardType = cardType;
            this.UidNumber = uid;
        }

        public string UidNumber { get; set; }

        public CARD_TYPE CardType { get; set; }
    }
}