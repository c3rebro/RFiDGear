using System;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicDataBlock.
	/// </summary>
	public class MifareClassicDataBlockModel
	{
	
		public MifareClassicDataBlockModel(int blockNumberDisplayItem)
		{
			this.dataBlockNumber = blockNumberDisplayItem;
		}
		
		public int dataBlockNumber {get; set;}
		
		public byte[] dataBlockContent { get; set; }
	}
}
