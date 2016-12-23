/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 25.11.2016
 * Time: 21:18
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicDataBlock.
	/// </summary>
	public class chipMifareClassicDataBlock
	{
		
		public chipMifareClassicDataBlock(int blockNumberDisplayItem)
		{
			this.dataBlockNumber = blockNumberDisplayItem;
		}
		
		public int dataBlockNumber {get; set;}
		
		public byte[] dataBlockContent { get; set; }
	}
}
