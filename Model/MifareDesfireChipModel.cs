using RFiDGear.DataAccessLayer;

using System;
using System.Collections.Generic;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipUid.
	/// </summary>
	public class MifareDesfireChipModel
	{
		readonly List<MifareDesfireAppModel> _appList = new List<MifareDesfireAppModel>();

		public List<MifareDesfireAppModel> AppList {
			get { return _appList; }
		}

		public MifareDesfireChipModel()
		{
			
		}
		
		public MifareDesfireChipModel(string uid, CARD_TYPE cardType)
		{
			uidNumber = uid;
			CardType = cardType;
		}
		
		public string uidNumber{ get; set;		}
		
		public CARD_TYPE CardType { get; set; }
	}
}
