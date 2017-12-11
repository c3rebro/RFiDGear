/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 04.08.2013
 * Time: 22:23
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using RFiDGear;
using LibLogicalAccess;
using System.Drawing;
using System.Windows.Forms;
using System.Resources;
using System.Reflection;
using System.Globalization;

namespace RFiDGear
{
	/// <summary>
	/// Description of EditWindow.
	/// </summary>
	
	public partial class EditClassicCardSectorTrailerForm : Form
	{
		byte[] sectortrailer;
		string keyA;
		string keyB;
		string accessBits;
		//public event sectorTrailerUpdate InvokeMethod;
		
		ResourceManager res_man;
		CultureInfo cul;
		
		SettingsReaderWriter settings = new SettingsReaderWriter();
		
		SectorAccessBits sab;
		
		public EditClassicCardSectorTrailerForm(byte[] sectorTrailer, CultureInfo cultureInfo, ResourceManager resManager)
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();
			
			cul = cultureInfo;
			res_man = resManager;
			
			groupBoxEditClassicCardAccessBits.Text = res_man.GetString("groupBoxEditClassicCardAccessBits", cul);
			groupBoxChangeKeys.Text = res_man.GetString("groupBoxChangeKeys", cul);
			groupBoxAccessRights.Text = res_man.GetString("groupBoxAccessRights", cul);
			checkBoxAllowChanges.Text = res_man.GetString("checkBoxAllowChanges", cul);
			buttonExitAndSave.Text = res_man.GetString("buttonExitAndSave", cul);
			buttonExitWithoutSave.Text = res_man.GetString("buttonExitWithoutSave", cul);
			labelEditWndSectorTrailer.Text = res_man.GetString("labelEditWndSectorTrailer", cul);
			
			sectortrailer = new byte[16];
			
			int i = 0;
			
			foreach (byte sectorTrailerBytes in sectorTrailer) {
				if (i > 48)
					sectortrailer[i - 48] = sectorTrailerBytes;
				i++;
			}	
		}
		
		void EditClassicCardWindowLoad(object sender, EventArgs e)
		{
						
			helperClass convert = new helperClass();
			MifareClassicAccessBits ab = new MifareClassicAccessBits();
			
			byte[] _accessBitsNonInv = new byte[8];
			byte[] _accessBitsInv = new byte[2];
			int discarded = 0;
			
			
			keyA = convert.HexToString(sectortrailer);
			keyA = keyA.Remove(12, keyA.Length - 12);
			
			textBoxEditKeyAClassicCardEditSettings.Text = keyA;
			
			keyB = convert.HexToString(sectortrailer);
			keyB = keyB.Remove(0, 20);
			
			textBoxEditKeyBClassicCardEditSettings.Text = keyB;
			
			accessBits = convert.HexToString(sectortrailer);
			accessBits = accessBits.Remove(0, 12);
			accessBits = accessBits.Remove(8);
			
			textBoxAccessBits.Text = accessBits;
			
			_accessBitsNonInv = convert.GetBytes(accessBits, out discarded);
			
			checkBoxAllowChanges.Checked = false;
			ab.decodeSectorTrailer(_accessBitsNonInv);
			
			comboBoxEditSectorTrailer.SelectedItem = ab.DecodedSectorTrailerAccessBits;
			comboBoxEditBlock0.SelectedItem = ab.DecodedDataBlock0AccessBits;
			comboBoxEditBlock1.SelectedItem = ab.DecodedDataBlock1AccessBits;
			comboBoxEditBlock2.SelectedItem = ab.DecodedDataBlock2AccessBits;
		}
		
		void ButtonExitAndSaveClick(object sender, EventArgs e)
		{
			MifareClassicAccessBits ab = new MifareClassicAccessBits();
			helperClass convert = new helperClass();
			
			int discarded = 0;
			
			ab.encodeSectorTrailer(comboBoxEditSectorTrailer.SelectedItem.ToString(), 3);
			ab.encodeSectorTrailer(comboBoxEditBlock2.SelectedItem.ToString(), 2);
			ab.encodeSectorTrailer(comboBoxEditBlock1.SelectedItem.ToString(), 1);
			ab.encodeSectorTrailer(comboBoxEditBlock0.SelectedItem.ToString(), 0);
			ab.encodeSectorTrailer("", 4);

			if (!ab.decodeSectorTrailer(textBoxAccessBits.Text)) {
				this.Hide();
				sectortrailer = convert.GetBytes(ab.SectorTrailerAccessBits, out discarded);
				
			} else {
				MessageBox.Show("sector trailer incorrect", "error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			
			
		}
		
		void ButtonExitWithoutSaveClick(object sender, EventArgs e)
		{
			this.Hide();
		}
		

		void ComboBoxEditSectorTrailerDropdownItemSelected(object sender, CustomEventsComboBox.DropdownItemSelectedEventArgs e)
		{
				string[] tooltips = new string[20];
			
			tooltips[0] = string.Format("Read Key A = not allowed; Write Key A = using Key A; \n" +
			"Read Access Conditions = using Key A; Write Access Conditions = not Allowed; \n" +
			"Read Key B = using Key A; Write Key B = using Key A");

			tooltips[1] = string.Format("Read Key A = not allowed; Write Key A = using Key B; \n" +
			"Read Access Conditions = using Key A or B; Write Access Conditions = not Allowed; \n" +
			"Read Key B = not allowed; Write Key B = using Key B");
			
			tooltips[2] = string.Format("Read Key A = not allowed; Write Key A = not allowed; \n" +
			"Read Access Conditions = using Key A; Write Access Conditions = not Allowed; \n" +
			"Read Key B = using Key A; Write Key B = not allowed");
			
			tooltips[3] = string.Format("Read Key A = not allowed; Write Key A = using Key A; \n" +
			"Read Access Conditions = using Key A; Write Access Conditions = using Key A; \n" +
			"Read Key B = using Key A; Write Key B = using Key A");
			
			tooltips[4] = string.Format("Read Key A = not allowed; Write Key A = not allowed; \n" +
			"Read Access Conditions = using Key A or B; Write Access Conditions = not Allowed; \n" +
			"Read Key B = not allowed; Write Key B = not allowed");
			
			tooltips[5] = string.Format("Read Key A = not allowed; Write Key A = using Key B; \n" +
			"Read Access Conditions = using Key A or B; Write Access Conditions = using Key B; \n" +
			"Read Key B = not allowed; Write Key B = using Key B");
			
			tooltips[6] = string.Format("Read Key A = not allowed; Write Key A = not allowed; \n" +
			"Read Access Conditions = using Key A or B; Write Access Conditions = not Allowed; \n" +
			"Read Key B = not allowed; Write Key B = not allowed");
			
			tooltips[7] = string.Format("Read Key A = not allowed; Write Key A = not allowed; \n" +
			"Read Access Conditions = using Key A or B; Write Access Conditions = using Key B; \n" +
			"Read Key B = not allowed; Write Key B = not allowed");
			
			if (e.SelectedItem < 0 || e.Scrolled)
				toolTip1.Hide(comboBoxEditSectorTrailer);
			else
				toolTip1.Show(tooltips[e.SelectedItem], comboBoxEditSectorTrailer, e.Bounds.Location.X + 150, e.Bounds.Location.Y + 10);
		
		}
		
		void CheckBoxAllowChangesCheckedChanged(object sender, EventArgs e)
		{
			if (checkBoxAllowChanges.Checked) {
				textBoxEditKeyAClassicCardEditSettings.Enabled = true;
				textBoxEditKeyBClassicCardEditSettings.Enabled = true;
				textBoxAccessBits.Enabled = true;
				comboBoxEditSectorTrailer.Enabled = true;
				comboBoxEditBlock2.Enabled = true;
				comboBoxEditBlock1.Enabled = true;
				comboBoxEditBlock0.Enabled = true;
			} else {
				textBoxEditKeyAClassicCardEditSettings.Enabled = false;
				textBoxEditKeyBClassicCardEditSettings.Enabled = false;
				textBoxAccessBits.Enabled = false;
				comboBoxEditSectorTrailer.Enabled = false;
				comboBoxEditBlock2.Enabled = false;
				comboBoxEditBlock1.Enabled = false;
				comboBoxEditBlock0.Enabled = false;
			}
		}
		
		void TextBoxAccessBitsTextChanged(object sender, EventArgs e)
		{
			MifareClassicAccessBits ab = new MifareClassicAccessBits();
			textBoxAccessBits.Text = textBoxAccessBits.Text.ToUpper();
			if (ab.decodeSectorTrailer(textBoxAccessBits.Text) || textBoxAccessBits.TextLength > 8) {
				textBoxAccessBits.BackColor = System.Drawing.Color.Purple;
			} else
				textBoxAccessBits.BackColor = System.Drawing.Color.White;
		}
		
		void ComboBoxEditSectorTrailerSelectedIndexChanged(object sender, EventArgs e)
		{
			if (comboBoxEditSectorTrailer.Enabled) {
				MifareClassicAccessBits ab = new MifareClassicAccessBits();
				
				string[] validBlockItems = new string[8] {
					"AB | AB | AB | AB",	"AB | B  | N  | N",
					"AB | N  | N  | N",		"AB | B  | B  | AB",
					"AB | N  | N  | AB",	"B  | N  | N  | N",
					"B  | B  | N  | N",		"N  | N  | N  | N"
				};
				
				string[] invalidBlockItems = new string[4] {
					"A  | A  | A  | A",		"A  | N  | N  | N",
					"A  | N  | N  | A",		"N  | N  | N  | N"
				};
				
				ab.encodeSectorTrailer(comboBoxEditSectorTrailer.SelectedItem.ToString(), 3);
				ab.encodeSectorTrailer(comboBoxEditBlock2.SelectedItem.ToString(), 2);
				ab.encodeSectorTrailer(comboBoxEditBlock1.SelectedItem.ToString(), 1);
				ab.encodeSectorTrailer(comboBoxEditBlock0.SelectedItem.ToString(), 0);
				ab.encodeSectorTrailer("", 4);
				
				textBoxAccessBits.Text = ab.SectorTrailerAccessBits;
				
				if (comboBoxEditSectorTrailer.SelectedIndex == 0 || comboBoxEditSectorTrailer.SelectedIndex == 2 || comboBoxEditSectorTrailer.SelectedIndex == 4) {
					this.comboBoxEditSectorTrailer.DropdownItemSelected -= new RFiDGear.CustomEventsComboBox.DropdownItemSelectedEventHandler(this.ComboBoxEditSectorTrailerDropdownItemSelected);
					this.comboBoxEditSectorTrailer.DrawItem -= new DrawItemEventHandler(this.ComboBoxEditSectorTrailerSelectedIndexChanged);
					
					this.comboBoxEditBlock0.SelectedIndexChanged -= new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					this.comboBoxEditBlock1.SelectedIndexChanged -= new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					this.comboBoxEditBlock2.SelectedIndexChanged -= new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					
					comboBoxEditBlock0.Items.Clear();
					comboBoxEditBlock1.Items.Clear();
					comboBoxEditBlock2.Items.Clear();
					
					for (int i = 0; i < invalidBlockItems.Length; i++) {
						comboBoxEditBlock0.Items.Add(invalidBlockItems[i]);
						comboBoxEditBlock1.Items.Add(invalidBlockItems[i]);
						comboBoxEditBlock2.Items.Add(invalidBlockItems[i]);
					}
					
					comboBoxEditBlock0.SelectedIndex = 0;
					comboBoxEditBlock1.SelectedIndex = 0;
					comboBoxEditBlock2.SelectedIndex = 0;
					
					this.comboBoxEditBlock0.SelectedIndexChanged += new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					this.comboBoxEditBlock1.SelectedIndexChanged += new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					this.comboBoxEditBlock2.SelectedIndexChanged += new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					
					//this.comboBoxEditSectorTrailer.DropdownItemSelected += new newComboBox.MyComboBox.DropdownItemSelectedEventHandler(this.ComboBoxEditSectorTrailerDropdownItemSelected);
					this.comboBoxEditSectorTrailer.SelectedIndexChanged += new System.EventHandler(this.ComboBoxEditSectorTrailerSelectedIndexChanged);
					
				} else if (comboBoxEditSectorTrailer.SelectedIndex == 1 || comboBoxEditSectorTrailer.SelectedIndex == 3 || comboBoxEditSectorTrailer.SelectedIndex == 5 || comboBoxEditSectorTrailer.SelectedIndex == 6 || comboBoxEditSectorTrailer.SelectedIndex == 7) {
					
					//this.comboBoxEditSectorTrailer.DropdownItemSelected -= new newComboBox.MyComboBox.DropdownItemSelectedEventHandler(this.ComboBoxEditSectorTrailerDropdownItemSelected);
					this.comboBoxEditSectorTrailer.SelectedIndexChanged -= new System.EventHandler(this.ComboBoxEditSectorTrailerSelectedIndexChanged);
					
					this.comboBoxEditBlock0.SelectedIndexChanged -= new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					this.comboBoxEditBlock1.SelectedIndexChanged -= new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					this.comboBoxEditBlock2.SelectedIndexChanged -= new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					
					comboBoxEditBlock0.Items.Clear();
					comboBoxEditBlock1.Items.Clear();
					comboBoxEditBlock2.Items.Clear();
					
					for (int i = 0; i < validBlockItems.Length; i++) {
						comboBoxEditBlock0.Items.Add(validBlockItems[i]);
						comboBoxEditBlock1.Items.Add(validBlockItems[i]);
						comboBoxEditBlock2.Items.Add(validBlockItems[i]);
					}
					
					comboBoxEditBlock0.SelectedIndex = 0;
					comboBoxEditBlock1.SelectedIndex = 0;
					comboBoxEditBlock2.SelectedIndex = 0;

					this.comboBoxEditBlock0.SelectedIndexChanged += new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					this.comboBoxEditBlock1.SelectedIndexChanged += new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					this.comboBoxEditBlock2.SelectedIndexChanged += new System.EventHandler(this.ComboBoxEditDataBlockSelectedIndexChanged);
					
					//this.comboBoxEditSectorTrailer.DropdownItemSelected += new newComboBox.MyComboBox.DropdownItemSelectedEventHandler(this.ComboBoxEditSectorTrailerDropdownItemSelected);
					this.comboBoxEditSectorTrailer.SelectedIndexChanged += new System.EventHandler(this.ComboBoxEditSectorTrailerSelectedIndexChanged);
				}
			}
		}
		
		void ComboBoxEditDataBlockSelectedIndexChanged(object sender, EventArgs e)
		{
			if (comboBoxEditSectorTrailer.Enabled) {
				MifareClassicAccessBits ab = new MifareClassicAccessBits();
				
				string[] validBlockItems = new string[8] {
					"AB | AB | AB | AB",	"AB | B  | N  | N",
					"AB | N  | N  | N",		"AB | B  | B  | AB",
					"AB | N  | N  | AB",	"B  | N  | N  | N",
					"B  | B  | N  | N",		"N  | N  | N  | N"
				};
				
				string[] invalidBlockItems = new string[4] {
					"A  | A  | A  | A",		"A  | N  | N  | N",
					"A  | N  | N  | A",		"N  | N  | N  | N"
				};
				
				ab.encodeSectorTrailer(comboBoxEditSectorTrailer.SelectedItem.ToString(), 3);
				ab.encodeSectorTrailer(comboBoxEditBlock2.SelectedItem.ToString(), 2);
				ab.encodeSectorTrailer(comboBoxEditBlock1.SelectedItem.ToString(), 1);
				ab.encodeSectorTrailer(comboBoxEditBlock0.SelectedItem.ToString(), 0);
				ab.encodeSectorTrailer("", 4);
				
				textBoxAccessBits.Text = ab.SectorTrailerAccessBits;
			}
		}
		
		public byte[] SectorTrailer {
			get{ return sectortrailer; }
			set{ sectortrailer = value; }
		}
		
		public SectorAccessBits getSectorAccessBits {
			get{ return sab; }
		}

	}
}

