/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 25.11.2016
 * Time: 21:16
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
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
