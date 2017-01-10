using System;
using System.Collections.Generic;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipUid.
	/// </summary>
	public class MifareDesfireUidTreeViewModel
	{
		readonly List<MifareDesfireAppIdTreeViewModel> _appList = new List<MifareDesfireAppIdTreeViewModel>();

		public List<MifareDesfireAppIdTreeViewModel> AppList {
			get { return _appList; }
		}
				
		public MifareDesfireUidTreeViewModel(string uid)
		{
			uidNumber = uid;
		}
		
		public string uidNumber{ get; set;		}
	}
}
