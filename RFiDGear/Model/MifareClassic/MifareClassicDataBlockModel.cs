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
            DataBlockAccessCondition = new MifareClassicDataBlockAccessConditionModel();
        }

        public MifareClassicDataBlockModel(
            uint _cx,
            SectorTrailer_DataBlock _blockNumber,
            AccessCondition_MifareClassicSectorTrailer _readDataBlock,
            AccessCondition_MifareClassicSectorTrailer _writeDataBlock,
            AccessCondition_MifareClassicSectorTrailer _incDataBlock,
            AccessCondition_MifareClassicSectorTrailer _decDataBlock)
        {
            DataBlockAccessCondition = new MifareClassicDataBlockAccessConditionModel();

            Cx = _cx;
            DataBlockNumberSectorBased = (int)_blockNumber;

            Read_DataBlock = _readDataBlock;
            Write_DataBlock = _writeDataBlock;

            Increment_DataBlock = _incDataBlock;
            Decrement_DataBlock = _decDataBlock;

        }

        public MifareClassicDataBlockModel(int _dataBlockNumberChipBased, int _dataBlockNumberSectorBased)
        {
            DataBlockAccessCondition = new MifareClassicDataBlockAccessConditionModel();

            DataBlockNumberChipBased = _dataBlockNumberChipBased;
            DataBlockNumberSectorBased = _dataBlockNumberSectorBased;
        }

        public int DataBlockNumberChipBased { get; set; }

        public int DataBlockNumberSectorBased { get; set; }

        public byte[] Data { get; set; }

        public MifareClassicDataBlockAccessConditionModel DataBlockAccessCondition { get; set; }

        public AccessCondition_MifareClassicSectorTrailer Read_DataBlock { get => DataBlockAccessCondition.Read_DataBlock; set => DataBlockAccessCondition.Read_DataBlock = value; }
        public AccessCondition_MifareClassicSectorTrailer Write_DataBlock { get => DataBlockAccessCondition.Write_DataBlock; set => DataBlockAccessCondition.Write_DataBlock = value; }
        public AccessCondition_MifareClassicSectorTrailer Increment_DataBlock { get => DataBlockAccessCondition.Increment_DataBlock; set => DataBlockAccessCondition.Increment_DataBlock = value; }
        public AccessCondition_MifareClassicSectorTrailer Decrement_DataBlock { get => DataBlockAccessCondition.Decrement_DataBlock; set => DataBlockAccessCondition.Decrement_DataBlock = value; }

        public bool IsAuthenticated { get; set; }
        public uint Cx { get => DataBlockAccessCondition.Cx; set => DataBlockAccessCondition.Cx = value; }
    }
}