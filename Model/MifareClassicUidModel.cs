using System;
using System.Collections.Generic;


namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicUid.
	/// </summary>
	
	public class MifareClassicUidModel
	{
		readonly List<MifareClassicSectorModel> _sectorList = new List<MifareClassicSectorModel>();

		public List<MifareClassicSectorModel> SectorList {
			get { return _sectorList; }
		}
		
		public MifareClassicUidModel(string uid, CARD_TYPE cardType)
		{
			this.CardType = cardType;
			this.UidNumber = uid;
		}
		
		public string UidNumber { get; set; }
		
		public CARD_TYPE CardType { get; set; }
	}
}
