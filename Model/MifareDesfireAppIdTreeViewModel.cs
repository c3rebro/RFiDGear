using System;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareDesfireAppID.
	/// </summary>
	public class MifareDesfireAppIdTreeViewModel
	{
		public uint appID { get; set; }
		
		public MifareDesfireAppIdTreeViewModel()
		{
			
		}
		
		public MifareDesfireAppIdTreeViewModel(uint _appID)
		{
			appID = _appID;
		}
	}
}
