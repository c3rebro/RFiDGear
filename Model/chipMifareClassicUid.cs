/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 26.11.2016
 * Time: 21:56
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;


namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicUid.
	/// </summary>
	
	public class chipMifareClassicUid
	{
		readonly List<chipMifareClassicSector> _sectorList = new List<chipMifareClassicSector>();

		public List<chipMifareClassicSector> SectorList {
			get { return _sectorList; }
		}
		
		public chipMifareClassicUid(string uid)
		{
			this.uidNumber = uid;
		}
		
		public string uidNumber { get; set; }
	}
}
