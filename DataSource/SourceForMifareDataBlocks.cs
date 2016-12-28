using System;
using System.ComponentModel;

namespace RFiDGear.DataSource
{
	/// <summary>
	/// Description of SourceForMifareDataBlocks.
	/// </summary>
	public class SourceForMifareDataBlocks
	{

		byte[] currentMifareClassicSector;
		
		CustomConverter converter = new CustomConverter();
		
		byte blocknSectorData;
		int discarded;
		
		public SourceForMifareDataBlocks(byte[] dataBlock, int indexByte)
		{
			currentMifareClassicSector = dataBlock;
			blocknSectorData = currentMifareClassicSector[indexByte];

		}
		
		[DisplayName("Int")]
		public byte singleByteBlock0AsByte {
			get { return blocknSectorData; }
			set { blocknSectorData = value; }
		}
		
		[DisplayName("Hex")]
		public string singleByteBlock0AsString {
			get { return blocknSectorData.ToString("X2"); }
			set { blocknSectorData = converter.GetBytes(value, out discarded)[0]; }
		}

		[DisplayName("ASCII")]
		public char singleByteBlock0AsChar {
			get {
				if (blocknSectorData < 32 | blocknSectorData > 126)
					return '.';
				else
					return (char)blocknSectorData;
			}
			
			set {
				if ((byte)value < 32 | (byte)value > 126)
					blocknSectorData |= 0;
				else
					blocknSectorData = (byte)value;
			}
			
		}
	}
}
