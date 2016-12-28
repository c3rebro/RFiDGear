using System;
using System.ComponentModel;

namespace RFiDGear.DataSource
{
	/// <summary>
	/// Description of SourceForDESFireFileData.
	/// </summary>
	public class SourceForDESFireFileData
	{
		
		CustomConverter converter = new CustomConverter();
		
		byte data;
		int discarded;
		
		public SourceForDESFireFileData(byte[] readerClass, int index)
		{
			data = readerClass[index];
		}
		
		[DisplayName("Data as Int")]
		public byte singleByteAsByte {
			get { return data; }
			set { data = value; }
		}
		
		[DisplayName("Data as Hex")]
		public string singleByteAsString {
			get { return data.ToString("X2"); }
			set { data = converter.GetBytes(value, out discarded)[0]; }
		}

		[DisplayName("Data as ASCII")]
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
