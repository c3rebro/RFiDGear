using RFiDGear.DataAccessLayer;

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
            Cx = _cx;
            DataBlockNumberSectorBased = (int)_blockNumber;

            Read_DataBlock = _readDataBlock;
            Write_DataBlock = _writeDataBlock;

            Increment_DataBlock = _incDataBlock;
            Decrement_DataBlock = _decDataBlock;

        }

        public MifareClassicDataBlockModel(int _dataBlockNumberChipBased, int _dataBlockNumberSectorBased)
        {
            DataBlockNumberChipBased = _dataBlockNumberChipBased;
            DataBlockNumberSectorBased = _dataBlockNumberSectorBased;
        }

        public int DataBlockNumberChipBased { get; set; }
        
        public int DataBlockNumberSectorBased { get; set; }

        public byte[] Data { get; set; }

		public byte ParentSectorNumber { get; set; }
		
		public AccessCondition_MifareClassicSectorTrailer Read_DataBlock { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Write_DataBlock { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Increment_DataBlock { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Decrement_DataBlock { get; set; }

        public bool IsAuthenticated { get; set; }
        public uint Cx { get; set; }
    }
}