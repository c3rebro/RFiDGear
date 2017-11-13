using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicSector.
	/// </summary>
	[XmlRootAttribute("MifareClassicSectorNode", IsNullable = false)]
	public class MifareClassicSectorModel
	{
		private string keyA;
		//private byte[] accessBitsAsByte;
		private string accessBitsAsString;
		private string keyB;
		private bool isAuthenticated;
		
		private uint cx;
		
		private Access_Condition read_KeyA;
		private Access_Condition write_KeyA;
		private Access_Condition read_Access_Condition;
		private Access_Condition write_Access_Condition;
		private Access_Condition read_KeyB;
		private Access_Condition write_KeyB;
		
		private ObservableCollection<MifareClassicDataBlockModel> mifareClassicBlock;
		
		public MifareClassicSectorModel()
		{
			mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();
		}
		
		public MifareClassicSectorModel(short _c1x, short _c2x, short _c3x)
		{
			mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();
			
			cx = 0;
			cx |= (uint)_c3x;
			cx <<= 1;
			cx |= (uint)_c2x;
			cx <<= 1;
			cx |= (uint)_c1x;
			
			read_KeyA = Access_Condition.NotApplicable;
			write_KeyA = Access_Condition.Allowed_With_KeyA;
			read_Access_Condition = Access_Condition.Allowed_With_KeyA;
			write_Access_Condition = Access_Condition.Allowed_With_KeyA;
			read_KeyB = Access_Condition.Allowed_With_KeyA;
			write_KeyB = Access_Condition.Allowed_With_KeyA;
			//this = sectorTrailer_AccessBits[(int)cx];
		}
		
		public MifareClassicSectorModel(uint _cx,
			Access_Condition _readKeyA = Access_Condition.NotApplicable,
			Access_Condition _writeKeyA = Access_Condition.Allowed_With_KeyA,
			Access_Condition _readAccessCondition = Access_Condition.Allowed_With_KeyA,
			Access_Condition _writeAccessCondition = Access_Condition.Allowed_With_KeyA,
			Access_Condition _readKeyB = Access_Condition.Allowed_With_KeyA,
			Access_Condition _writeKeyB = Access_Condition.Allowed_With_KeyA)
		{
			
			mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();
			
			cx = _cx;
			
			read_KeyA =  _readKeyA;
			write_KeyA = _writeKeyA;
			
			read_Access_Condition = _readAccessCondition;
			write_Access_Condition = _writeAccessCondition;
			
			read_KeyB = _readKeyB;
			write_KeyB = _writeKeyB;
		}
		
		public MifareClassicSectorModel(int _sectorNumber)
		{
			mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();
			
			sectorNumber = _sectorNumber;
		}
		
		public MifareClassicSectorModel(
			int _sectorNumber = 0,
			string _keyA = "ff ff ff ff ff ff", // optional parameter: if not specified use transport configuration
			string _accessBitsAsString = "FF0780C3", // optional parameter: if not specified use transport configuration
			string _keyB = "ff ff ff ff ff ff") // optional parameter: if not specified use transport configuration
		{
			mifareClassicBlock = new ObservableCollection<MifareClassicDataBlockModel>();
			
			sectorNumber = _sectorNumber;
			keyA = _keyA;
			accessBitsAsString = _accessBitsAsString;
			
			keyB = _keyB;
		}
		
		public ObservableCollection<MifareClassicDataBlockModel> DataBlock {
			get { return mifareClassicBlock; }
			set { mifareClassicBlock = value; }
		}
		
		private int sectorNumber;
		
		public int SectorNumber { get { return sectorNumber; } set { sectorNumber = value; }}
		public string KeyA { get { return keyA; } set { keyA = value; }}
		public string AccessBitsAsString { get { return accessBitsAsString; } set { accessBitsAsString = value; }}

		//public byte[] AccessBitsAsByte { get { return accessBitsAsByte; }}
		public string KeyB { get { return keyB; } set { keyB = value; }}
				
		public Access_Condition Read_KeyA { get { return read_KeyA; } set { read_KeyA = value; }}
		public Access_Condition Write_KeyA { get { return write_KeyA; } set { write_KeyA = value; }}
		public Access_Condition Read_Access_Condition { get { return read_Access_Condition; } set { read_Access_Condition = value; }}
		public Access_Condition Write_Access_Condition { get { return write_Access_Condition; } set { write_Access_Condition = value; }}
		public Access_Condition Read_KeyB { get { return read_KeyB; } set { read_KeyB = value; }}
		public Access_Condition Write_KeyB { get { return write_KeyB; } set { write_KeyB = value; }}
		
		public bool IsAuthenticated { get { return isAuthenticated; } set { isAuthenticated = value; }}
		public uint Cx { get { return cx; } set { cx = value; }}
		
	}
	
	
//	public struct Sector_AccessCondition
//	{
//		public Sector_AccessCondition(
//			Access_Condition _readKeyA = Access_Condition.NotApplicable,
//			Access_Condition _writeKeyA = Access_Condition.Allowed_With_KeyA,
//			Access_Condition _readAccessCondition = Access_Condition.Allowed_With_KeyA,
//			Access_Condition _writeAccessCondition = Access_Condition.Allowed_With_KeyA,
//			Access_Condition _readKeyB = Access_Condition.Allowed_With_KeyA,
//			Access_Condition _writeKeyB = Access_Condition.Allowed_With_KeyA)
//		{
//			cx = 0;
//			
//			read_KeyA =  _readKeyA;
//			write_KeyA = _writeKeyA;
//			
//			read_Access_Condition = _readAccessCondition;
//			write_Access_Condition = _writeAccessCondition;
//			
//			read_KeyB = _readKeyB;
//			write_KeyB = _writeKeyB;
//		}
//		
//		public Sector_AccessCondition(short _c1x, short _c2x, short _c3x)
//		{
//			cx = 0;
//			cx |= (uint)_c3x;
//			cx <<= 1;
//			cx |= (uint)_c2x;
//			cx <<= 1;
//			cx |= (uint)_c1x;
//			
//			read_KeyA = Access_Condition.NotApplicable;
//			write_KeyA = Access_Condition.Allowed_With_KeyA;
//			read_Access_Condition = Access_Condition.Allowed_With_KeyA;
//			write_Access_Condition = Access_Condition.Allowed_With_KeyA;
//			read_KeyB = Access_Condition.Allowed_With_KeyA;
//			write_KeyB = Access_Condition.Allowed_With_KeyA;
//			//this = sectorTrailer_AccessBits[(int)cx];
//		}
//		
//		private uint cx;
//		
//		private Access_Condition read_KeyA;
//		private Access_Condition write_KeyA;
//		private Access_Condition read_Access_Condition;
//		private Access_Condition write_Access_Condition;
//		private Access_Condition read_KeyB;
//		private Access_Condition write_KeyB;
//		
//		public Access_Condition Read_KeyA { get { return read_KeyA; }}
//		public Access_Condition Write_KeyA { get { return write_KeyA; }}
//		public Access_Condition Read_Access_Condition { get { return read_Access_Condition; }}
//		public Access_Condition Write_Access_Condition { get { return write_Access_Condition; }}
//		public Access_Condition Read_KeyB { get { return read_KeyB; }}
//		public Access_Condition Write_KeyB { get { return write_KeyB; }}
//		
//		public uint Cx { get { return cx; } set { cx = value; }}
//	}
}
