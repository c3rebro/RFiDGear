/*
 * Created by SharpDevelop.
 * User: c3rebro
 * Date: 02.03.2018
 * Time: 23:21
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace RFiDGear.Extensions.VCNEditor.Model
{
    /// <summary>
    /// Description of AccessProfile.
    /// </summary>
    public class AccessProfile
    {
        public AccessProfile()
        {
            AccessProfileAsBytes = new byte[4];
            MainListWords = new byte[16];
        }

        public bool IsLastProfile { get; set; }

        public int ProfileType { get { return AccessProfileAsBytes[0] & 0x03; } }

        public byte[] AccessProfileAsBytes { get; set; }

        public byte[] MainListWords { get; set; }

        public int MainListWordsCount { get { return (int)MainListWords.Length / 2; } }

        public byte[] WeekSchedules { get; set; }

        public int WeekSchedulesCount { get { return (int)(WeekSchedules != null ? WeekSchedules.Length / 2 : 0); } }

        public string ProfileDesc { get { return string.Format("{0}:{1}:{2}", ProfileType, ByteArrayHelper.Extensions.ByteArrayConverter.GetStringFrom(AccessProfileAsBytes), ByteArrayHelper.Extensions.ByteArrayConverter.GetStringFrom(MainListWords)); } }
    }
}
