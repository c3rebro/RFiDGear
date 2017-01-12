using System;
using System.Collections.Generic;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicSector.
	/// </summary>
	public class MifareClassicSectorTreeViewModel
	{
		
		readonly List<MifareClassicDataBlockTreeViewModel> mifareClassicBlock = new List<MifareClassicDataBlockTreeViewModel>();
		
		public MifareClassicAccessBitsBaseModel sectorAccessBits { get; set; }
		
		public MifareClassicSectorTreeViewModel(int sectorNumber)
		{
			sectorAccessBits = new MifareClassicAccessBitsBaseModel();
			this.mifareClassicSectorNumber = sectorNumber;
		}
		
		public IList<MifareClassicDataBlockTreeViewModel> dataBlock {
			get { return mifareClassicBlock; }
		}
		
		public int mifareClassicSectorNumber { get; set; }
		
	}
}
