using System;
using System.Windows.Input;
using System.Linq;

namespace RFiDGear.View
{
	/// <summary>
	/// Initialize Reader
	/// The folllowing Readers are supportet by the Class
	/// 
	/// 	new PCSCReaderProvider(),
	///		new A3MLGM5600ReaderProviderClass(),
	///		new AdmittoReaderProviderClass(),
	///		new AxessTMC13ReaderProviderClass(),
	///		new DeisterReaderProviderClass(),
	///		new ElatecReaderProviderClass(),
	///		new GunneboReaderProviderClass(),
	///		new IdOnDemandReaderProviderClass(),
	///		new KeyboardReaderProviderClass(),
	///		new OK5553ReaderProviderClass(),
	///		new PCSCReaderProviderClass(),
	///		new PromagReaderProviderClass(),
	///		new RFIDeasReaderProviderClass(),
	///		new RplethReaderProviderClass(),
	///		new SCIELReaderProviderClass(),
	///		new SmartIDReaderProviderClass(),
	///		new STidPRGReaderProviderClass()
	/// 
	/// </summary>
	public partial class ReaderSetupDialogBox
	{
		/*
		// global (cross-class) Instances go here ->
		SettingsReaderWriter settings;
		
		RFiDAccess rfidAccess;
		
		// global (this class only) variables go here ->
		ResourceManager res_man;
		CultureInfo cul;
		*/
		public ReaderSetupDialogBox()
		{
			InitializeComponent();
/*			
			settings = newSettings;
			res_man = resMan;
			cul = culInfo;
			
			btnConnectToReader.Content="Connect";
			btnAbortAndExit.Content="Cancel";
			tooltipbtnConnect.Text = res_man.GetString("toolTipButtonCardReaderEstablishConnection", cul);
			tooltipComboboxSelectReader.Text = res_man.GetString("toolTipSelectReaderComboBox", cul);
			
			// File Handles go here ->
			settings.readSettings();
			
			// neccessary Class Instances go here ->
			
			// Language Ressources go here ->
			labelReaderConResult.Content = res_man.GetString("textBoxCardStatus", cul);

			this.ShowInTaskbar = false;
			
			if (settings.DefaultReader != "") {
				comboboxSelectReader.Items.Add(settings.DefaultReader);
				comboboxSelectReader.SelectedItem = settings.DefaultReader;
			}
			
			rfidAccess = new RFiDAccess(settings);
*/
		}
	
  
		private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			DragMove();
		}
/*	
		void ButtonCardReaderEstablishConnectionClick(object sender, EventArgs e)
		{
			rfidAccess.readChipPublic();
		}
		
		void SelectReaderComboBoxSelectedIndexChanged(object sender, EventArgs e)
		{
		}
		
		void ButtonSaveAndCloseCardReaderSettingsClick(object sender, EventArgs e)
		{
			this.Hide();
		}
		
		void ButtonDiscardCardReaderSettingsClick(object sender, EventArgs e)
		{
			
		}
		
		public void DialogWindowClosed(object sender, EventArgs e){
			this.Hide();
		}
*/		
	}
}