using RFiDGear.DataAccessLayer;

using System;

namespace RFiDGear
{
	/// <summary>
	/// Description of RFiDReaderSetup.
	/// </summary>
	/// 
	
	public class ReaderModel : RFiDDevice
	{
		private RFiDDevice rfidDevice;
		private SettingsReaderWriter settings = new SettingsReaderWriter();
		
		public ReaderModel(ReaderTypes readerProviderByName = ReaderTypes.PCSC)
		{
			base.ReaderProvider = readerProviderByName;
		}
		
		public ReaderTypes SelectedReader {
			get {
				return settings.defaultSpecification.DefaultReaderProvider;
			}
			set {
				if (settings.defaultSpecification.DefaultReaderProvider != value) {
					if (rfidDevice == null)
						rfidDevice = new RFiDDevice(value);
					else {
						rfidDevice = null;
						rfidDevice = new RFiDDevice(value);
					}
					settings.defaultSpecification.DefaultReaderProvider = value;
					settings.defaultSpecification.DefaultReaderName = rfidDevice.CurrentReaderUnitName;
				}
			}
		}
		
		public CARD_INFO GetChipInfo {
			get {
				return rfidDevice.CardInfo;
			}
		}
		
		public string GetReaderName {
			get {
				if(rfidDevice.IsChipPresent)
					return rfidDevice.CurrentReaderUnitName;
				else
					return "not connected";
			}
		}
	}
}
