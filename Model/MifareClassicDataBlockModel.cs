using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using System;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicDataBlock.
	/// </summary>
	public class MifareClassicDataBlockModel
	{
		public MifareClassicDataBlockModel()
		{
			
		}
		
		public MifareClassicDataBlockModel(
			uint _cx,
			SectorTrailer_DataBlock _blockNumber,
			AccessCondition_MifareClassicSectorTrailer _readDataBlock,
			AccessCondition_MifareClassicSectorTrailer _writeDataBlock,
			AccessCondition_MifareClassicSectorTrailer _incDataBlock,
			AccessCondition_MifareClassicSectorTrailer _decDataBlock)
		{
			cx = _cx;
			blockNumber = _blockNumber;
			
			read_DataBlock =  _readDataBlock;
			write_DataBlock = _writeDataBlock;
			
			increment_DataBlock = _incDataBlock;
			decrement_DataBlock = _decDataBlock;
			
			ab = new LibLogicalAccess.SectorAccessBits();
			
			switch(blockNumber)
			{
				case SectorTrailer_DataBlock.Block0:
					ab.d_data_block0_access_bits.c1 = 0;
					ab.d_data_block0_access_bits.c2 = 0;
					ab.d_data_block0_access_bits.c3 = 0;
					break;
				case SectorTrailer_DataBlock.Block1:
					ab.d_data_block1_access_bits.c1 = 0;
					ab.d_data_block1_access_bits.c2 = 0;
					ab.d_data_block1_access_bits.c3 = 0;
					break;
				case SectorTrailer_DataBlock.Block2:
					ab.d_data_block2_access_bits.c1 = 0;
					ab.d_data_block2_access_bits.c2 = 0;
					ab.d_data_block2_access_bits.c3 = 0;
					break;
			}
			
		}
		
		private LibLogicalAccess.SectorAccessBits ab;
		
		private uint cx;
		private SectorTrailer_DataBlock blockNumber;
		private bool isAuthenticated;
		
		private AccessCondition_MifareClassicSectorTrailer read_DataBlock;
		private AccessCondition_MifareClassicSectorTrailer write_DataBlock;
		private AccessCondition_MifareClassicSectorTrailer increment_DataBlock;
		private AccessCondition_MifareClassicSectorTrailer decrement_DataBlock;

		public MifareClassicDataBlockModel(int _dataBlockNumber)
		{
			dataBlockNumber = _dataBlockNumber;
		}
		
		public int dataBlockNumber {get; set;}
		
		public byte[] Data { get; set; }
		
		public AccessCondition_MifareClassicSectorTrailer Read_DataBlock { get { return read_DataBlock; } set { read_DataBlock = value; }}
		public AccessCondition_MifareClassicSectorTrailer Write_DataBlock { get { return write_DataBlock; } set { write_DataBlock = value; }}
		public AccessCondition_MifareClassicSectorTrailer Increment_DataBlock { get { return increment_DataBlock; } set { increment_DataBlock = value; }}
		public AccessCondition_MifareClassicSectorTrailer Decrement_DataBlock { get { return decrement_DataBlock; } set { decrement_DataBlock = value; }}
		
		public LibLogicalAccess.SectorAccessBits SectorAccessBits { get { return ab; } set { ab = value; }}
		public int BlockNumber { get { return dataBlockNumber; } set { dataBlockNumber = value; }}
		public bool IsAuthenticated { get { return isAuthenticated; } set { isAuthenticated = value; }}
		public uint Cx { get { return cx; } set { cx = value; }}
	}
}
