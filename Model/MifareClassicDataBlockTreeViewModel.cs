using System;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicDataBlock.
	/// </summary>
	public class MifareClassicDataBlockTreeViewModel
	{
	
		public MifareClassicDataBlockTreeViewModel(int blockNumberDisplayItem)
		{
			this.dataBlockNumber = blockNumberDisplayItem;
		}
		
		public int dataBlockNumber {get; set;}
		
		public byte[] dataBlockContent { get; set; }
	}
}
