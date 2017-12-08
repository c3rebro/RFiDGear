using LibLogicalAccess;

using System;

namespace RFiDGear
{
	/// <summary>
	/// Description of DesfireDataContent.
	/// </summary>
	public class MifareDesfireFileModel
	{
		
		byte data;
		int discarded;
		
		public MifareDesfireFileModel()
		{
			
		}
		
		public MifareDesfireFileModel(byte _fileID)
		{
			FileID = _fileID;
		}
		
		public MifareDesfireFileModel(byte[] cardContent, int arIndex)
		{
			data = cardContent[arIndex];
		}
		
		public byte FileID { get; set; }
		
		public FileSetting DesfireFileSetting { get; set; }
		
		public byte singleByteAsByte {
			get { return data; }
			set { data = value; }
		}
		
		public string singleByteAsString {
			get { return data.ToString("X2"); }
			set { data = CustomConverter.GetBytes(value, out discarded)[0]; }
		}

		public char singleByteAsChar {
			get {
				if (data < 32 | data > 126)
					return '.';
				else
					return (char)data;
			}
			
			set {
				if ((byte)value < 32 | (byte)value > 126)
					data |= 0;
				else
					data = (byte)value;
			}
		}
	}
}
