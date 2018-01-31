using LibLogicalAccess;

namespace RFiDGear
{
    /// <summary>
    /// Description of DesfireDataContent.
    /// </summary>
    public class MifareDesfireFileModel
    {
        public MifareDesfireFileModel()
        {
        }

        public MifareDesfireFileModel(byte[] _cardContent, byte _fileID = 0)
        {
            Data = _cardContent;
            FileID = _fileID;
        }

        public byte[] Data { get; set; }
        
        public byte FileID { get; set; }

        public FileSetting DesfireFileSetting { get; set; }
    }
}