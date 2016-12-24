using System;
using System.Collections.Generic;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipUid.
	/// </summary>
	public class MifareDesfireUidModel
	{
		readonly List<MifareDesfireAppModel> _appList = new List<MifareDesfireAppModel>();

		public List<MifareDesfireAppModel> AppList {
			get { return _appList; }
		}
				
		public MifareDesfireUidModel(string uid)
		{
			uidNumber = uid;
		}
		
		public string uidNumber{ get; set;		}
	}
}
