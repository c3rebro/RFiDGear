/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 04/10/2018
 * Time: 20:55
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using RFiDGear.Infrastructure.AccessControl;

namespace RFiDGear.Models
{
    /// <summary>
    /// Description of chipMifareClassicDataBlock.
    /// </summary>
    public class MifareClassicMADModel
    {
        public MifareClassicMADModel()
        {
        }

        public MifareClassicMADModel(byte[] _cardContent, int _madAppId)
        {
            Data = _cardContent;
            MADApp = _madAppId;
        }

        public MifareClassicMADModel(
            uint _cx,
            AccessCondition_MifareClassicSectorTrailer _readDataBlock,
            AccessCondition_MifareClassicSectorTrailer _writeDataBlock,
            AccessCondition_MifareClassicSectorTrailer _incDataBlock,
            AccessCondition_MifareClassicSectorTrailer _decDataBlock)
        {
            Cx = _cx;

            Read_DataBlock = _readDataBlock;
            Write_DataBlock = _writeDataBlock;

            Increment_DataBlock = _incDataBlock;
            Decrement_DataBlock = _decDataBlock;

        }

        public MifareClassicMADModel(int _madAppId, int _sectorNumber)
        {
            MADApp = _madAppId;
            SectorNumber = _sectorNumber;
        }

        public int MADApp { get; set; }

        public int SectorNumber { get; set; }

        public byte[] Data { get; set; }

        public AccessCondition_MifareClassicSectorTrailer Read_DataBlock { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Write_DataBlock { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Increment_DataBlock { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Decrement_DataBlock { get; set; }

        public bool IsAuthenticated { get; set; }
        public uint Cx { get; set; }
    }
}