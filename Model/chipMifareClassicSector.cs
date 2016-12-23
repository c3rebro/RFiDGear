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
	public class chipMifareClassicSector
	{
		
		readonly List<chipMifareClassicDataBlock> mifareClassicBlock = new List<chipMifareClassicDataBlock>();
		
		public chipMifareClassicSector(int sectorNumber)
		{
			this.mifareClassicSectorNumber = sectorNumber;
		}
		
		public IList<chipMifareClassicDataBlock> dataBlock {
			get { return mifareClassicBlock; }
		}
		
		public int mifareClassicSectorNumber { get; set; }
	}
}
