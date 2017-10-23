using RFiDGear.DataAccessLayer;

using System;
using System.Collections.Generic;
using System.Xml.Serialization;


namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicUid.
	/// </summary>
	[XmlRootAttribute("MifareClassicUIDNode", IsNullable = false)]
	public class MifareClassicUidTreeViewModel
	{
		readonly List<MifareClassicSectorTreeViewModel> _sectorList = new List<MifareClassicSectorTreeViewModel>();

		public List<MifareClassicSectorTreeViewModel> SectorList {
			get { return _sectorList; }
		}

		public MifareClassicUidTreeViewModel()
		{
			
		}
		
		public MifareClassicUidTreeViewModel(string uid, CARD_TYPE cardType)
		{
			this.CardType = cardType;
			this.UidNumber = uid;
		}
		
		public string UidNumber { get; set; }
		
		public CARD_TYPE CardType { get; set; }
	}
}
