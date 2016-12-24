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
		
		public MifareClassicUidModel(string uid)
		{
			this.uidNumber = uid;
		}
		
		public string uidNumber { get; set; }
	}
}
