using System;

namespace RFiDGear
{
	/// <summary>
	/// Description of RFiDReaderSetup.
	/// </summary>
	public class ReaderSetupModel : RFiDAccess
	{
		private RFiDAccess chipReaderWriter;
		
		readonly static string[] readerProviderList = {
			"A3MLGM5600", "Admitto",
			"AxessTMC13", "Deister", "Elatec",
			"Gunnebo", "IdOnDemand",
			"OK5553", "PCSC", "Promag",
			"RFIDeas", "Rpleth", "SCIEL",
			"SmartID", "STidPRG", "N/A"
		};
		
		public ReaderSetupModel(string readerProviderByName) : base(null) {
			if((!String.IsNullOrEmpty(new SettingsReaderWriter()._defaultReaderProvider)) && String.IsNullOrEmpty(readerProviderByName)){
				chipReaderWriter = new RFiDAccess(new SettingsReaderWriter()._defaultReaderProvider);
			}
			
			else if(!String.IsNullOrEmpty(readerProviderByName)){
				chipReaderWriter = new RFiDAccess(readerProviderByName);
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
					if(chipReaderWriter == null)
						chipReaderWriter = new RFiDAccess(value);
					chipReaderWriter = null;
					chipReaderWriter = new RFiDAccess(value);
				}
			}
		}
		
		public string GetChipUID {
			get {

				if(!chipReaderWriter.readChipPublic() && !String.IsNullOrEmpty(chipReaderWriter.currentChipUID))
					return chipReaderWriter.currentChipUID;
				return null;
			}
		}
		
		public string GetChipType {
			get {
				if(!chipReaderWriter.readChipPublic() && !String.IsNullOrEmpty(chipReaderWriter.currentChipType))
					return chipReaderWriter.currentChipType;
				return null;
			}
		}
		
		public string GetReaderName {
			get {
				chipReaderWriter.readChipPublic();
				return chipReaderWriter.currentReaderUnitName ?? "not connected";
			}
		}
	}
}
