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
		
		public MifareClassicAccessBitsModel sectorAccessBits { get; set; }
		
		public MifareClassicSectorModel(int sectorNumber)
		{
			sectorAccessBits = new MifareClassicAccessBitsModel();
			this.mifareClassicSectorNumber = sectorNumber;
		}
		
		public IList<MifareClassicDataBlockModel> dataBlock {
			get { return mifareClassicBlock; }
		}
		
		public int mifareClassicSectorNumber { get; set; }
		
	}
}
