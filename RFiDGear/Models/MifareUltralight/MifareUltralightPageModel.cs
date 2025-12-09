/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 15.04.2018
 * Time: 00:05
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace RFiDGear.Models
{
    /// <summary>
    /// Description of MifareUltralightPageModel.
    /// </summary>
    public class MifareUltralightPageModel
    {
        public MifareUltralightPageModel()
        {
        }

        public MifareUltralightPageModel(byte[] _data, int _page)
        {
            Data = _data;
            PageNumber = _page;
        }

        public byte[] Data { get; set; }

        public int PageNumber { get; set; }
    }
}
