using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET.Cards
{
    public class BaseChip
    {
        public ChipType ChipType { get; set; }
        public byte[] UID { get; set; }

        public BaseChip()
        {          
        }

        public BaseChip(ChipType chipType, byte[] uid)
        {
            UID = uid;
            ChipType = chipType;
        }

        public string UIDHexString
        {
            get
            {
                return ByteArrayConverter.GetStringFrom(UID);
            }
        }
    }
}
