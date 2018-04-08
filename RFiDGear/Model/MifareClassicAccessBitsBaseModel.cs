using RFiDGear.DataAccessLayer;

using System;
using System.Collections.ObjectModel;
using System.Security;
using LibLogicalAccess;

namespace RFiDGear
{
	/// <summary>
	/// Description of mifareClassicAccessBits.
	/// </summary>
	public class MifareClassicAccessBitsBaseModel
	{
		private string[] accessConditionDisplayItem = {"using Key A","using Key B","Key A or B","not Allowed"};
		
		private SectorAccessBits sab;
		
		private string decodedSectorTrailerAccessBits;
		private string decodedBlock0AccessBits;
		private string decodedBlock1AccessBits;
		private string decodedBlock2AccessBits;
		
		private uint C1;
		private uint C2;
		private uint C3;
		
		private uint _C1;
		private uint _C2;
		private uint _C3;
		
		private byte[] st;
		
		public string sectorKeyAKey {get; set; }
		public string sectorKeyBKey {get; set; }
		public string sectorAccessBitsAsString {get; set; }
		public byte[] sectorAccessBitsAsByte {get; set; }
		
		public static readonly string[] dataBlockABs = new string[8] {
			"A,A,A,A",
			"",
			"A,N,N,N",
			"",
			"A,N,N,A",
			"",
			"",
			"N,N,N,N"
		};
		
		public static readonly string[] dataBlockAB = new string[8] {
			"AB,AB,AB,AB",
			"AB,B,N,N",
			"AB,N,N,N",
			"AB,B,B,AB",
			"AB,N,N,AB",
			"B,N,N,N",
			"B,B,N,N",
			"N,N,N,N"
		};
		
		public static readonly string[] sectorTrailerAB = new string[8] {
			"N,A,A,N,A,A",
			"N,B,AB,N,N,B",
			"N,N,A,N,A,N",
			"N,N,AB,N,N,N",
			"N,A,A,A,A,A",
			"N,N,AB,B,N,N",
			"N,B,AB,B,N,B",
			"N,N,AB,N,N,N"
		};
		
		public string DecodedSectorTrailerAccessBits {
			get{ return decodedSectorTrailerAccessBits; }
			set{ decodedSectorTrailerAccessBits = value; }
		}
		public string SectorTrailerAccessBits {
			get{ return sectorAccessBitsAsString; }
			set{ sectorAccessBitsAsString = value;}
		}
		public string DecodedDataBlock0AccessBits {
			get{ return decodedBlock0AccessBits; }
			set{ decodedBlock0AccessBits = value; }
		}
		public string DecodedDataBlock1AccessBits {
			get{ return decodedBlock1AccessBits; }
			set{ decodedBlock1AccessBits = value; }
		}
		public string DecodedDataBlock2AccessBits {
			get{ return decodedBlock2AccessBits; }
			set{ decodedBlock2AccessBits = value; }
		}
		public SectorAccessBits LibLogicalAccessAB{
			get {return sab;}
			set {sab = value;}
		}
		
		#region methods and constructor
		public MifareClassicAccessBitsBaseModel()
		{
			st = new byte[4] { 0x00, 0x00, 0x00, 0xC3 };
			sab = new SectorAccessBits();
		}
		

		
		#endregion
	}
}
