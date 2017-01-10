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
		
		public MifareClassicAccessBitsModel sectorAccessBits { get; set; }
		
		public MifareClassicSectorTreeViewModel(int sectorNumber)
		{
			sectorAccessBits = new MifareClassicAccessBitsModel();
			this.mifareClassicSectorNumber = sectorNumber;
		}
		
		public IList<MifareClassicDataBlockTreeViewModel> dataBlock {
			get { return mifareClassicBlock; }
		}
		
		public int mifareClassicSectorNumber { get; set; }
		
	}
}
