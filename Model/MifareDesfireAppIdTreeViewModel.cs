using System;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareDesfireAppID.
	/// </summary>
	public class MifareDesfireAppIdTreeViewModel
	{
		public string[] appIDs { get; set; }
		
		public MifareDesfireAppIdTreeViewModel(string[] _appIDs)
		{
			appIDs = _appIDs;
		}
	}
}
