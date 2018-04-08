using System;
using System.ComponentModel;

namespace RFiDGear.DataSource
{
	/// <summary>
	/// Description of MifareClassicDataBlockDataGridModel.
	/// </summary>
	public class MifareClassicDataModel
	{
		
		byte[] currentMifareClassicSector;
		
		
		byte blocknSectorData;
		int discarded;		
		
		public MifareClassicDataModel(byte[] dataBlock, int indexByte)
		{
			currentMifareClassicSector = dataBlock;
			blocknSectorData = currentMifareClassicSector[indexByte];
		}
		
		//[DisplayName("Int")]
		public byte singleByteBlock0AsByte {
			get { return blocknSectorData; }
			set { blocknSectorData = value;

			}
		}
		
		//[DisplayName("Hex")]
		public string singleByteBlock0AsString {
			get { return blocknSectorData.ToString("X2"); }
			set { blocknSectorData = CustomConverter.GetBytes(value, out discarded)[0];

			}
		}

		//[DisplayName("ASCII")]
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
		
		//[DisplayName("Binary")]
		public string singleByteBlock0AsBinary {
			get { return Convert.ToString(blocknSectorData, 2).PadLeft(8, '0'); }
			set { blocknSectorData = Convert.ToByte(value,2);}
			
		}
	}
}
