/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 25.04.2018
 * Time: 23:22
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using RFiDGear.DataAccessLayer;
using RFiDGear.DataAccessLayer.AccessControl;

using System;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of MifareClassicDataBlockAccessConditionModel.
    /// </summary>
    public class MifareClassicDataBlockAccessConditionModel
    {
        public MifareClassicDataBlockAccessConditionModel()
        {
        }

        public MifareClassicDataBlockAccessConditionModel(
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

        public MifareClassicDataBlockAccessConditionModel(int _dataBlockNumberChipBased, int _dataBlockNumberSectorBased)
        {
            DataBlockNumberChipBased = _dataBlockNumberChipBased;
            DataBlockNumberSectorBased = _dataBlockNumberSectorBased;
        }

        public int DataBlockNumberChipBased { get; set; }

        public int DataBlockNumberSectorBased { get; set; }

        public byte[] Data { get; set; }

        public AccessCondition_MifareClassicSectorTrailer Read_DataBlock { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Write_DataBlock { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Increment_DataBlock { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Decrement_DataBlock { get; set; }

        public bool IsAuthenticated { get; set; }
        public uint Cx { get; set; }
    }
}
