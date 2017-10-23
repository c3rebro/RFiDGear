using System;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareDesfireAppID.
	/// </summary>
	public class MifareDesfireAppIdTreeViewModel
	{
		public string appID { get; set; }
		
		public MifareDesfireAppIdTreeViewModel()
		{
			
		}
		
		public MifareDesfireAppIdTreeViewModel(string _appID)
		{
			appID = _appID;
		}
	}
}
