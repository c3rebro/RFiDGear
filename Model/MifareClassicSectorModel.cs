using System;
using System.Collections.Generic;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicSector.
	/// </summary>
	public class MifareClassicSectorModel
	{
		
		readonly List<MifareClassicDataBlockModel> mifareClassicBlock = new List<MifareClassicDataBlockModel>();
		
		public MifareClassicSectorModel(int sectorNumber)
		{
			this.mifareClassicSectorNumber = sectorNumber;
		}
		
		public IList<MifareClassicDataBlockModel> dataBlock {
			get { return mifareClassicBlock; }
		}
		
		public int mifareClassicSectorNumber { get; set; }
	}
}
