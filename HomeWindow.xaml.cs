/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 11/23/2016
 * Time: 22:24
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using MahApps.Metro.Controls;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Resources;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace RFiDGear
{
	
	//	public delegate void TreeViewNodeMouseAction(object sender, TreeNodeMouseClickEventArgs e);
	

	
	
	/// <summary>
	/// Description of MainForm.
	/// </summary>
	/// 
	/// <summary>
	/// Interaction logic for HomeWindow.xaml
	/// </summary>
	public partial class HomeWindow
	{
		/*
		ResourceManager resManager;
		CultureInfo cultureInfo;
		
		SettingsReaderWriter settings;
		RFiDAccess rfidAccess;
		//ReaderSetupDialogBox readerSetup;
		
		 */
		public HomeWindow()
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();
			/*
			settings = new SettingsReaderWriter();
			
			settings.readSettings();
			
			if (settings.defaultLanguage == "german"){
				radioButtonGerman.IsChecked = true;
				cultureInfo = new CultureInfo("de");
			}
			
			else{
				radioButtonEnglish.IsChecked = true;
				cultureInfo = new CultureInfo("en");
			}
			
			
			// neccessary Class Instances go here ->
			resManager = new ResourceManager("RFiDGear.Resources.Manifest", System.Reflection.Assembly.GetExecutingAssembly());

			rfidAccess = new RFiDAccess(settings);
			//desfireKeySetupForm = new MifareDESFireKeySetupForm();
			
			//uidNodes = new List<RFiDGear.DataModel.chipMifareClassicTreeViewContext>();
			 */

		}
		

		private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			DragMove();
		}
		
		private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
		{
			e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
		}
		
		/**************************************************/
		//////////////////Global Variables//////////////////
		/**************************************************/
		/*
	
		bool isAuth;
		
		string selectedLanguage;

		string defaultClassicCardKeyA = "FFFFFFFFFFFF" + '\n';
		string defaultClassicCardKeyB = "FFFFFFFFFFFF" + '\n';
		
		int treeCount;
		
		List<string> knownUIDs = new List<string>();
		
		List<DataModel.chipMifareClassicUid> uidViewModelList = new List<RFiDGear.DataModel.chipMifareClassicUid>();
		
		//BindingSource cardDataSource = new BindingSource();
		//BindingSource classicCardKeySource = new BindingSource();
		//BindingSource desfireDataSource = new BindingSource();





		
		void MenuItemExitAppClick(object sender, EventArgs e)
		{
			App.Current.Shutdown();
		}
		
		void MenuItemReaderSettingsClick(object sender, EventArgs e)
		{
			readerSetup = new ReaderSetupDialogBox(settings, resManager, cultureInfo);
			readerSetup.Show();
		}

		//********************************************************
		//Function Name: selectreadercomboboxSelectionChanged
		//Input(Parameter) : sender, e
		//OutPutParameter:-------
		//Description:Selecting The Reader
		//********************************************************
		private void SelectLanguageRadioButtonClick(object sender, EventArgs e)
		{
			if ((bool)radioButtonEnglish.IsChecked) {
				selectedLanguage = "english";
				settings.saveSettings(null, null, null, selectedLanguage);
				System.Windows.MessageBox.Show("The Software need's to be restarted in order to apply the changes", "Restart required", MessageBoxButton.OK, MessageBoxImage.Information);
			} else {
				selectedLanguage = "german";
				settings.saveSettings(null, null, null, selectedLanguage);
				System.Windows.MessageBox.Show("Die Software muss neu gesartet werden damit die Änderungen wirksam werden.", "Neustart der Software erforderlich", MessageBoxButton.OK, MessageBoxImage.Information);
			}
		}
		
		//********************************************************
		//Function Name: selectreadercomboboxSelectionChanged
		//Input(Parameter) : sender, e
		//OutPutParameter:-------
		//Description:Selecting The Reader
		//********************************************************
		void cardReaderSetupToolStripMenuItemClick(object sender, EventArgs e)
		{
			readerSetup.Show();
		}
		
		void reDrawTreeView()
		{
			helperClass converter = new helperClass();
			
			//RFiDGear.DataModel.chipMifareClassicTreeViewContext newNode = new RFiDGear.DataModel.chipMifareClassicTreeViewContext(rfidAccess.currentChip.ChipIdentifier);
			if (!knownUIDs.Contains(rfidAccess.currentChip.ChipIdentifier)) {
				
				knownUIDs.Add(rfidAccess.currentChip.ChipIdentifier);
				
				var uidDataModel = new DataModel.chipMifareClassicUid(knownUIDs[knownUIDs.Count-1]);
				
				uidViewModelList.Add(uidDataModel);
				
				var viewModel = new ViewModel.TreeViewMainModel(uidViewModelList.ToArray(), (CARD_TYPE)Array.IndexOf(converter._constCardType, rfidAccess.currentChip.Type));
				treeView.DataContext = viewModel;
				
				//new TreeNode Creation for CardType MiFare Classic Card 1K
				if (rfidAccess.currentChip.Type == "Mifare1K") {

				}

				//new TreeNode Creation for CardType MiFare Classic Card 4K
				else if (rfidAccess.currentChip.Type == "Mifare4K") {

				}

				//new TreeNode Creation for CardType MiFare DESFire
				else if (rfidAccess.currentChip.Type == "DESFireEV1") {
					
				}
			}
		}
		
		public void reDrawDataGrid()
		{
			
		}

		void contextMenuStripEditClassicCardDefaultKeyClick(object sender, EventArgs e)
		{
			
			settings.readSettings();
			
		}

		void contextMenuStripEditClassicCardSectorTrailerClick(object sender, EventArgs e)
		{
		}
		
		void contextMenuStripReadMifareClassicCardClick(object sender, EventArgs e)
		{
			helperClass converter = new helperClass();
			
			rfidAccess.usedClassicCardKeyA = defaultClassicCardKeyA.Replace("\n", "");
			rfidAccess.usedClassicCardKeyB = defaultClassicCardKeyB.Replace("\n", "");
			
			if (!(rfidAccess.readChipPublic() || rfidAccess.readMiFareClassicChipData())) {
				if (rfidAccess.currentChip.Type == "Mifare1K") {
					
					
					
					//********************************************************
					//Function Name:
					//Input(Parameter) :-------
					//OutPutParameter:-------
					//Description:
					//********************************************************
				}
				
				isAuth = true;
				
			} else {
				isAuth = false;
			}
		}
		
		//********************************************************
		//Function Name:
		//Input(Parameter) :-------
		//OutPutParameter:-------
		//Description:
		//********************************************************
		void contextMenuStripReadDesfireCardAppIDsClick(object sender, EventArgs e)
		{
			helperClass converter = new helperClass();
			
			if (!(rfidAccess.readChipPublic() || rfidAccess.getMiFareDESFireChipAppIDs())) {
				reDrawTreeView();
			}

		}
		
		
		void contextMenuStripAuthToDesfireCardAppIDClick(object sender, EventArgs e)
		{
			rfidAccess.readMiFareDESFireChipFile(0, 1);
			reDrawDataGrid();
		}
		
		void contextMenuStripDeleteSelectedAppClick(object sender, EventArgs e)
		{
			rfidAccess.writeMiFareDESFireChipFile(0, 1);
		}
		
		void contextMenuStripAuthToDesfireCardMasterAppClick(object sender, EventArgs e)
		{
		}

		//********************************************************
		//Function Name:
		//Input(Parameter) :-------
		//OutPutParameter:-------
		//Description:
		//********************************************************
		void BeendenToolStripMenuItemClick(object sender, EventArgs e)
		{
			if (rfidAccess.currentChip != null)
				rfidAccess.currentReader.DisconnectFromReader();
			Environment.Exit(0);
		}
		
		//********************************************************
		//Function Name:
		//Input(Parameter) :-------
		//OutPutParameter:-------
		//Description:
		//********************************************************
		void readCardPublicToolStripMenuItemClick(object sender, EventArgs e)
		{
			rfidAccess.readChipPublic();
			reDrawTreeView();
		}

		//********************************************************
		//Function Name:
		//Input(Parameter) :-------
		//OutPutParameter:-------
		//Description:
		//********************************************************
		void ApplyChangesToolStripMenuItemClick(object sender, EventArgs e)
		{

			settings.saveSettings();
		}
		
		
		

		
		void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			if (rfidAccess.currentChip != null)
				rfidAccess.currentReader.DisconnectFromReader();
			Environment.Exit(0);
		}
		 */
	}
	
}