using System;

namespace RFiDGear
{
	/// <summary>
	/// Description of RFiDReaderSetup.
	/// </summary>
	/// 
	
	public class ReaderSetupModel
	{
		private RFiDDevice rfidDevice;
		
		readonly static string[] readerProviderList = {
			"Admitto","AxessTMC13", "Deister",
			"Gunnebo", "IdOnDemand","PCSC",
			"Promag","RFIDeas", "Rpleth",
			"SCIEL","SmartID", "STidPRG",
			"N/A"
		};
		
		public ReaderSetupModel(string readerProviderByName)
		{
			if ((!String.IsNullOrEmpty(new SettingsReaderWriter()._defaultReaderProvider)) && String.IsNullOrEmpty(readerProviderByName)) {
				rfidDevice = new RFiDDevice(new SettingsReaderWriter()._defaultReaderProvider);
			} else if (!String.IsNullOrEmpty(readerProviderByName)) {
				rfidDevice = new RFiDDevice(readerProviderByName);
			}
			else {
				rfidDevice = new RFiDDevice("PCSC");
			}
		}
		
		public string[] ReaderList {
			get { return readerProviderList; }
		}
		
		public string SelectedReader {
			get {
				if (!String.IsNullOrEmpty(new SettingsReaderWriter()._defaultReaderProvider)) {
					return new SettingsReaderWriter()._defaultReaderProvider;
				}

				return "N/A";
			}
			set {
				if (new SettingsReaderWriter()._defaultReaderProvider != value && value != "N/A") {
					if (rfidDevice == null)
						rfidDevice = new RFiDDevice(value);
					else {
						rfidDevice = null;
						rfidDevice = new RFiDDevice(value);
					}
					new SettingsReaderWriter().saveSettings(rfidDevice.CurrentReaderUnitName, value);
				}
			}
		}
		
		public string GetChipUID {
			get {

				if (!rfidDevice.IsChipPresent && !String.IsNullOrEmpty(rfidDevice.currentChipUID))
					return rfidDevice.currentChipUID;
				return null;
			}
		}
		
		public string GetChipType {
			get {
				if (!rfidDevice.IsChipPresent && !String.IsNullOrEmpty(rfidDevice.currentChipType))
					return rfidDevice.currentChipType;
				return null;
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
