using System;
using LibLogicalAccess;

namespace RFiDGear
{
	/// <summary>
	/// Description of mifareClassicAccessBits.
	/// </summary>
	public class MifareClassicAccessBitsModel
	{
		private SectorAccessBits sab;
		
		private string[] accessConditionDisplayItem = {"using Key A","using Key B","Key A or B","not Allowed"};
		private string sectorTrailerString;
		private byte[] sectorTrailerByte;
		
		private string decodedSectorTrailerAccessBits;
		private string decodedBlock0AccessBits;
		private string decodedBlock1AccessBits;
		private string decodedBlock2AccessBits;
		
		private readonly string[] dataBlockABs = new string[8] {
			"A,A,A,A",
			"",
			"A,N,N,N",
			"",
			"A,N,N,A",
			"",
			"",
			"N,N,N,N"
		};
		
		private readonly string[] dataBlockAB = new string[8] {
			"AB,AB,AB,AB",
			"AB,B,N,N",
			"AB,N,N,N",
			"AB,B,B,AB",
			"AB,N,N,AB",
			"B,N,N,N",
			"B,B,N,N",
			"N,N,N,N"
		};
		
		private readonly string[] sectorTrailerAB = new string[8] {
			"N,A,A,N,A,A",
			"N,B,AB,N,N,B",
			"N,N,A,N,A,N",
			"N,N,AB,N,N,N",
			"N,A,A,A,A,A",
			"N,N,AB,B,N,N",
			"N,B,AB,B,N,B",
			"N,N,AB,N,N,N"
		};
		
		private uint C1;
		private uint C2;
		private uint C3;
		
		private uint _C1;
		private uint _C2;
		private uint _C3;
		
		private byte[] st;
		
		public MifareClassicAccessBitsModel()
		{
			st = new byte[4] { 0x00, 0x00, 0x00, 0xC3 };
			sab = new SectorAccessBits();
		}
		
		public bool decodeSectorTrailer(byte[] st)
		{
			bool isTransportConfiguration;
			
			uint tmpAccessBitCx;
			
			if (checkSectorTrailerCorrectness(st))
				return true;

			#region getAccessBitsForSectorTrailer
			
			C1 = st[1];
			C2 = st[2];
			
			C1 &= 0xF0;
			C1 >>= 7;
			C1 &= 0x01;
			
			sab.d_sector_trailer_access_bits.c1 = (short)C1;
			
			C2 >>= 2;
			
			tmpAccessBitCx = C2;
			tmpAccessBitCx >>= 1;
			tmpAccessBitCx &= 0x01;
			
			sab.d_sector_trailer_access_bits.c2 = (short)tmpAccessBitCx;
			
			C1 |= C2;
			C2 >>= 3;
			
			tmpAccessBitCx = C2;
			tmpAccessBitCx >>= 2;
			tmpAccessBitCx &= 0x01;
			
			sab.d_sector_trailer_access_bits.c3 = (short)tmpAccessBitCx;
			
			C1 &= 0x03;
			C1 |= C2;
			C1 &= 0x07;
			
			if(C1 == 4)
				isTransportConfiguration = true;
			else
				isTransportConfiguration = false;
			
			decodedSectorTrailerAccessBits = sectorTrailerAB[C1];
			
			#endregion

			#region getAccessBitsForDataBlock2

			C1 = st[1];
			C2 = st[2];
			
			C1 &= 0xF0;
			C1 >>= 6;
			C1 &= 0x01;
			
			sab.d_data_block2_access_bits.c1 = (short)C1;
			
			C2 >>= 1;
			
			tmpAccessBitCx = C2;
			tmpAccessBitCx >>= 1;
			tmpAccessBitCx &= 0x01;
			
			sab.d_data_block2_access_bits.c2 = (short)tmpAccessBitCx;
			
			C1 |= C2;
			//C2 &= 0xF8;
			C2 >>= 3;
			
			tmpAccessBitCx = C2;
			tmpAccessBitCx >>= 2;
			tmpAccessBitCx &= 0x01;
			
			sab.d_data_block2_access_bits.c3 = (short)tmpAccessBitCx;
			
			C1 &= 0x03;
			C1 |= C2;
			C1 &= 0x07;
			
			if(isTransportConfiguration)
				decodedBlock2AccessBits = dataBlockABs[C1];
			else
				decodedBlock2AccessBits = dataBlockAB[C1];

			#endregion

			#region getAccessBitsForDataBlock1
			
			C1 = st[1];
			C2 = st[2];
			
			C1 &= 0xF0;
			C1 >>= 5;
			C1 &= 0x01;
			
			sab.d_data_block1_access_bits.c1 = (short)C1;
			
			C1 |= C2;
			
			tmpAccessBitCx = C2;
			tmpAccessBitCx >>= 1;
			tmpAccessBitCx &= 0x01;
			
			sab.d_data_block1_access_bits.c2 = (short)tmpAccessBitCx;
			
			C2 >>= 3;
			
			tmpAccessBitCx = C2;
			tmpAccessBitCx >>= 2;
			tmpAccessBitCx &= 0x01;
			
			sab.d_data_block1_access_bits.c3 = (short)tmpAccessBitCx;
			
			C1 &= 0x03;
			C1 |= C2;
			C1 &= 0x07;
			
			if(isTransportConfiguration)
				decodedBlock1AccessBits = dataBlockABs[C1];
			else
				decodedBlock1AccessBits = dataBlockAB[C1];
			
			#endregion
			
			#region getAccessBitsForDataBlock0
			
			C1 = st[1];
			C2 = st[2];
			
			C1 &= 0xF0;
			C1 >>= 4;
			C1 &= 0x01;
			
			tmpAccessBitCx = C1;
			tmpAccessBitCx &= 0x01;
			
			sab.d_data_block0_access_bits.c1 = (short)C1;
			
			tmpAccessBitCx = C2;
			tmpAccessBitCx &= 0x01;
			
			sab.d_data_block0_access_bits.c2 = (short)C1;
			
			C2 <<= 1;
			C1 |= C2;
			C2 >>= 3;
			
			tmpAccessBitCx = C2;
			tmpAccessBitCx >>= 2;
			tmpAccessBitCx &= 0x01;
			
			sab.d_data_block0_access_bits.c3 = (short)C1;
			
			C2 &= 0xFC;
			C1 &= 0x03;
			C1 |= C2;
			C1 &= 0x07;
			
			if(isTransportConfiguration)
				decodedBlock0AccessBits = dataBlockABs[C1];
			else
				decodedBlock0AccessBits = dataBlockAB[C1];
			
			#endregion
			
			return false;
		}
		public bool decodeSectorTrailer(string st)
		{
			
			CustomConverter convert = new CustomConverter();
			
			byte[] _bytes = new byte[255];
			int discarded = 0;
			
			_bytes = convert.GetBytes(st, out discarded);
			
			if (!decodeSectorTrailer(_bytes))
				return false;
			else
				return true;
		}
		
		public bool encodeSectorTrailer(string cond, int type)
		{
			
			CustomConverter convert = new CustomConverter();
			
			if (cond == sectorTrailerAB[1] || cond == dataBlockAB[1] || cond == dataBlockABs[1] ||
			    cond == sectorTrailerAB[3] || cond == dataBlockAB[3] || cond == dataBlockABs[3] ||
			    cond == sectorTrailerAB[5] || cond == dataBlockAB[5] || cond == dataBlockABs[5] ||
			    cond == sectorTrailerAB[7] || cond == dataBlockAB[7] || cond == dataBlockABs[7])
				C1 = 0x01;
			else
				C1 = 0x00;
			
			if (cond == sectorTrailerAB[2] || cond == dataBlockAB[2] || cond == dataBlockABs[2] ||
			    cond == sectorTrailerAB[3] || cond == dataBlockAB[3] || cond == dataBlockABs[3] ||
			    cond == sectorTrailerAB[6] || cond == dataBlockAB[6] || cond == dataBlockABs[6] ||
			    cond == sectorTrailerAB[7] || cond == dataBlockAB[7] || cond == dataBlockABs[7])
				C2 = 0x01;
			else
				C2 = 0x00;
			
			if (cond == sectorTrailerAB[4] || cond == dataBlockAB[4] || cond == dataBlockABs[4] ||
			    cond == sectorTrailerAB[5] || cond == dataBlockAB[5] || cond == dataBlockABs[5] ||
			    cond == sectorTrailerAB[6] || cond == dataBlockAB[6] || cond == dataBlockABs[6] ||
			    cond == sectorTrailerAB[7] || cond == dataBlockAB[7] || cond == dataBlockABs[7])
				C3 = 0x01;
			else
				C3 = 0x00;
			
			switch (type) {
				case 0:
					{
						C1 <<= 4;
						st[1] |= (byte)C1;
						st[2] |= (byte)C2;
						C3 <<= 4;
						st[2] |= (byte)C3;
					}
					break;
					
				case 1:
					{
						C1 <<= 5;
						st[1] |= (byte)C1;
						C2 <<= 1;
						st[2] |= (byte)C2;
						C3 <<= 5;
						st[2] |= (byte)C3;
					}
					break;
					
				case 2:
					{
						C1 <<= 6;
						st[1] |= (byte)C1;
						C2 <<= 2;
						st[2] |= (byte)C2;
						C3 <<= 6;
						st[2] |= (byte)C3;
					}
					break;
					
				case 3:
					{
						C1 <<= 7;
						st[1] |= (byte)C1;
						C2 <<= 3;
						st[2] |= (byte)C2;
						C3 <<= 7;
						st[2] |= (byte)C3;
					}
					break;
					
				default:
					{
						sectorTrailerByte = buildSectorTrailerInvNibble(st);
						sectorTrailerString = convert.HexToString(sectorTrailerByte);
					}
					break;
			}

			return false;
		}
		
		byte[] buildSectorTrailerInvNibble(byte[] st)
		{
			byte[] new_st = new byte[st.Length];

			_C3 = st[2];
			_C3 ^= 0xFF;
			_C3 >>= 4;
			_C3 &= 0x0F;
			
			new_st[1] = st[1];
			new_st[1] &= 0xF0;
			new_st[1] |= (byte)_C3;
			
			_C2 = st[2];
			_C2 ^= 0xFF;
			_C2 &= 0x0F;
			
			new_st[0] = (byte)_C2;
			new_st[0] <<= 4;
			
			_C1 = st[1];
			_C1 ^= 0xFF;
			_C1 >>= 4;
			_C1 &= 0x0F;
			
			new_st[0] &= 0xF0;
			new_st[0] |= (byte)_C1;
			
			new_st[1] |= st[1];
			new_st[2] |= st[2];
			
			return new_st;
		}
		
		public bool checkSectorTrailerCorrectness(byte[] st)
		{
			_C2 = st[0];
			_C2 &= 0xF0;
			_C2 >>= 4;
			
			C2 = st[2];
			C2 &= 0x0F;
			C2 |= 0xF0;
			C2 ^= 0xFF;
			
			if (C2 != _C2)
				return true;
			else {
				_C1 = st[0];
				_C1 &= 0x0F;
				
				
				C1 = st[1];
				C1 &= 0xF0;
				C1 >>= 4;
				C1 |= 0xF0;
				C1 ^= 0xFF;
				
				if (C1 != _C1)
					return true;
				else {
					_C3 = st[1];
					_C3 &= 0x0F;
					
					C3 = st[2];
					C3 &= 0xF0;
					C3 >>= 4;
					C3 |= 0xF0;
					C3 ^= 0xFF;
					
					if (C3 != _C3)
						return true;
					else
						return false;
				}
			}
			
		}
		
		public bool checkSectorTrailerCorrectness(string stString)
		{
			CustomConverter convert = new CustomConverter();
			byte[] st = new byte[255];
			int discarded = 0;
			
			st = convert.GetBytes(stString, out discarded);
			
			if(!checkSectorTrailerCorrectness(st))
				return false;
			else
				return true;
		}
		
		public string DecodedSectorTrailerAccessBits {
			get{ return decodedSectorTrailerAccessBits; }
			set{ decodedSectorTrailerAccessBits = value; }
		}
		public string SectorTrailerAccessBits {
			get{ return sectorTrailerString; }
			set{ sectorTrailerString = value;}
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
		public SectorAccessBits GetSetAccessBits{
			get {return sab;}
			set {sab = value;}
		}
		public string[] GetSectorTrailerAccessConditions{
			get { return sectorTrailerAB;}
		}
		public string[] GetDataBlockAccessConditions{
			get { return dataBlockAB;}
		}
		public string[] GetShortDataBlockAccessConditions{
			get { return dataBlockABs;}
		}
	}
}
