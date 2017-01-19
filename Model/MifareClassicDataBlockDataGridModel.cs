using System;
using System.ComponentModel;

namespace RFiDGear.DataSource
{
	/// <summary>
	/// Description of MifareClassicDataBlockDataGridModel.
	/// </summary>
	public class MifareClassicDataBlockDataGridModel : INotifyPropertyChanged
	{
		
		byte[] currentMifareClassicSector;
		
		CustomConverter converter = new CustomConverter();
		
		byte blocknSectorData;
		int discarded;
		
		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion // INotifyPropertyChanged Members
		
		
		public MifareClassicDataBlockDataGridModel(byte[] dataBlock, int indexByte)
		{
			currentMifareClassicSector = dataBlock;
			blocknSectorData = currentMifareClassicSector[indexByte];
		}
		
		[DisplayName("Int")]
		public byte singleByteBlock0AsByte {
			get { return blocknSectorData; }
			set { blocknSectorData = value;
				OnPropertyChanged("singleByteBlock0AsBinary");
				OnPropertyChanged("singleByteBlock0AsString");
				OnPropertyChanged("singleByteBlock0AsChar");
			}
		}
		
		[DisplayName("Hex")]
		public string singleByteBlock0AsString {
			get { return blocknSectorData.ToString("X2"); }
			set { blocknSectorData = converter.GetBytes(value, out discarded)[0];
				OnPropertyChanged("singleByteBlock0AsByte");
				OnPropertyChanged("singleByteBlock0AsBinary");
				OnPropertyChanged("singleByteBlock0AsChar");
			}
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
				
				OnPropertyChanged("singleByteBlock0AsBinary");
				OnPropertyChanged("singleByteBlock0AsString");
				OnPropertyChanged("singleByteBlock0AsByte");
			}
			
		}
		
		[DisplayName("Binary")]
		public string singleByteBlock0AsBinary {
			get { return Convert.ToString(blocknSectorData, 2).PadLeft(8, '0'); }
			set { blocknSectorData = Convert.ToByte(value,2);
				OnPropertyChanged("singleByteBlock0AsChar");
				OnPropertyChanged("singleByteBlock0AsString");
				OnPropertyChanged("singleByteBlock0AsByte");}
			
		}
	}
}
