using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using System;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipMifareClassicDataBlock.
	/// </summary>
	public class MifareClassicDataBlockTreeViewModel
	{
		public MifareClassicDataBlockTreeViewModel(
			uint _cx,
			Data_Block _blockNumber,
			Access_Condition _readDataBlock,
			Access_Condition _writeDataBlock,
			Access_Condition _incDataBlock,
			Access_Condition _decDataBlock)
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
				case Data_Block.Block0:
					ab.d_data_block0_access_bits.c1 = 0;
					ab.d_data_block0_access_bits.c2 = 0;
					ab.d_data_block0_access_bits.c3 = 0;
					break;
				case Data_Block.Block1:
					ab.d_data_block1_access_bits.c1 = 0;
					ab.d_data_block1_access_bits.c2 = 0;
					ab.d_data_block1_access_bits.c3 = 0;
					break;
				case Data_Block.Block2:
					ab.d_data_block2_access_bits.c1 = 0;
					ab.d_data_block2_access_bits.c2 = 0;
					ab.d_data_block2_access_bits.c3 = 0;
					break;
			}
			
		}
		
		public MifareClassicDataBlockTreeViewModel(Data_Block _blockNumber, short _c1x = 0, short _c2x = 0, short _c3x = 0)
		{
			blockNumber = _blockNumber;
			cx = 0;
			cx |= (uint)_c3x;
			cx <<= 1;
			cx |= (uint)_c2x;
			cx <<= 1;
			cx |= (uint)_c1x;
			
			read_DataBlock = Access_Condition.NotApplicable;
			write_DataBlock = Access_Condition.NotApplicable;
			increment_DataBlock = Access_Condition.NotApplicable;
			decrement_DataBlock = Access_Condition.NotApplicable;
			//this = dataBlock_AccessBits[(int)cx];
			
			ab = new LibLogicalAccess.SectorAccessBits();
			
			switch(blockNumber)
			{
				case Data_Block.Block0:
					ab.d_data_block0_access_bits.c1 = _c1x;
					ab.d_data_block0_access_bits.c2 = _c2x;
					ab.d_data_block0_access_bits.c3 = _c3x;
					break;
				case Data_Block.Block1:
					ab.d_data_block1_access_bits.c1 = _c1x;
					ab.d_data_block1_access_bits.c2 = _c2x;
					ab.d_data_block1_access_bits.c3 = _c3x;
					break;
				case Data_Block.Block2:
					ab.d_data_block2_access_bits.c1 = _c1x;
					ab.d_data_block2_access_bits.c2 = _c2x;
					ab.d_data_block2_access_bits.c3 = _c3x;
					break;
			}
		}
		
		private LibLogicalAccess.SectorAccessBits ab;
		
		private uint cx;
		private Data_Block blockNumber;
		
		private Access_Condition read_DataBlock;
		private Access_Condition write_DataBlock;
		private Access_Condition increment_DataBlock;
		private Access_Condition decrement_DataBlock;
		
		public MifareClassicDataBlockTreeViewModel()
		{
			
		}
		
		public MifareClassicDataBlockTreeViewModel(int blockNumberDisplayItem)
		{
			this.dataBlockNumber = blockNumberDisplayItem;
		}
		
		public int dataBlockNumber {get; set;}
		
		public byte[] dataBlockContent { get; set; }
		
		public Access_Condition Read_DataBlock { get { return read_DataBlock; } set { read_DataBlock = value; }}
		public Access_Condition Write_DataBlock { get { return write_DataBlock; } set { write_DataBlock = value; }}
		public Access_Condition Increment_DataBlock { get { return increment_DataBlock; } set { increment_DataBlock = value; }}
		public Access_Condition Decrement_DataBlock { get { return decrement_DataBlock; } set { decrement_DataBlock = value; }}
		
		public LibLogicalAccess.SectorAccessBits SectorAccessBits { get { return ab; } set { ab = value; }}
		public uint BlockNumber { get { return (uint)blockNumber; } set { blockNumber = (Data_Block)Enum.Parse(typeof(Data_Block), Enum.GetName(typeof(Data_Block),value)); }}
		public uint Cx { get { return cx; } set { cx = value; }}
	}
}
