namespace RFiDGear.Model
{
    /// <summary>
    /// Description of chipMifareDesfireAppID.
    /// </summary>
    public class MifareDesfireAppModel
    {
        public uint appID { get; set; }

        public MifareDesfireAppModel()
        {
        }

        public MifareDesfireAppModel(uint _appID)
        {
            appID = _appID;
        }
    }
}