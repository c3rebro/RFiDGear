/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 25.04.2018
 * Time: 22:37
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using RFiDGear.DataAccessLayer;

using System;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of MifareClassicSectorAccessConditionModel.
    /// </summary>
    public class MifareClassicSectorAccessConditionModel
    {

        public MifareClassicSectorAccessConditionModel()
        {
        }

        public MifareClassicSectorAccessConditionModel(short _cx,
                                                       AccessCondition_MifareClassicSectorTrailer _readKeyA,
                                                       AccessCondition_MifareClassicSectorTrailer _writeKeyA,
                                                       AccessCondition_MifareClassicSectorTrailer _readAccessCondition,
                                                       AccessCondition_MifareClassicSectorTrailer _writeAccessCondition,
                                                       AccessCondition_MifareClassicSectorTrailer _readKeyB,
                                                       AccessCondition_MifareClassicSectorTrailer _writeKeyB)
        {
            Cx = _cx;

            Read_KeyA = _readKeyA;
            Write_KeyA = _writeKeyA;

            Read_AccessCondition_MifareClassicSectorTrailer = _readAccessCondition;
            Write_AccessCondition_MifareClassicSectorTrailer = _writeAccessCondition;

            Read_KeyB = _readKeyB;
            Write_KeyB = _writeKeyB;
        }


        public AccessCondition_MifareClassicSectorTrailer Read_KeyA { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Write_KeyA { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Read_AccessCondition_MifareClassicSectorTrailer { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Write_AccessCondition_MifareClassicSectorTrailer { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Read_KeyB { get; set; }
        public AccessCondition_MifareClassicSectorTrailer Write_KeyB { get; set; }

        public bool IsAuthenticated { get; set; }
        public short Cx { get; set; }

    }
}
