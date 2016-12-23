/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 18.11.2016
 * Time: 21:15
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RFiDGear
{
	

		
	/// <summary>
	/// Description of Form1.
	/// </summary>
	public partial class MifareDESFireKeySetupForm : Form
	{
		public event DesfireKeySetupChanged desfireKeySetupChanged;
		
		SettingsReaderWriter settings;
		helperClass converter;
		
		public string desFireCardCardMasterKey { get; set; }
		public string desFireCardApplicationMasterKey { get; set; }
		public string desFireCardReadKey { get; set; }
		public string desFireCardWriteKey { get; set; }
		
		public string desFireCardCardMasterKeyType { get; set; }
		public string desFireCardApplicationMasterKeyType { get; set; }
		public string desFireCardReadKeyType { get; set; }
		public string desFireCardWriteKeyType { get; set; }
		
		public MifareDESFireKeySetupForm()
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();
			
			
			settings = new SettingsReaderWriter();
			converter = new helperClass();
			
			settings.readSettings();
			
			desFireCardCardMasterKeyType = settings._defaultDesfireCardCardMasterKeyType;
			desFireCardApplicationMasterKeyType = settings._defaultDesfireCardApplicationMasterKeyType;
			desFireCardReadKeyType = settings._defaultDesfireCardReadKeyType;
			desFireCardWriteKeyType = settings._defaultDesfireCardWriteKeyType;
			
		}
		
		void KeySetupFormLoad(object sender, EventArgs e)
		{
			textBoxCardMasterKey.Text = settings._defaultDesfireCardCardMasterKey;
			comboBoxCardMasterKeyType.SelectedIndex = Array.IndexOf(converter._constDesfireCardKeyType, desFireCardCardMasterKeyType);
			
			textBoxApplicationMasterKey.Text = settings._defaultDesfireCardApplicationMasterKey;
			comboBoxApplicationMasterKeyType.SelectedIndex = Array.IndexOf(converter._constDesfireCardKeyType, desFireCardApplicationMasterKeyType);
			
			textBoxReadKey.Text = settings._defaultDesfireCardReadKey;
			comboBoxReadKeyType.SelectedIndex = Array.IndexOf(converter._constDesfireCardKeyType, desFireCardReadKeyType);
			
			textBoxWriteKey.Text = settings._defaultDesfireCardWriteKey;
			comboBoxWriteKeyType.SelectedIndex = Array.IndexOf(converter._constDesfireCardKeyType, desFireCardWriteKeyType);
		}
		
		void ButtonSaveAndExitClick(object sender, EventArgs e)
		{
			if (converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxCardMasterKey.Text) == KEY_ERROR.KEY_IS_EMPTY
			    || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxApplicationMasterKey.Text) == KEY_ERROR.KEY_IS_EMPTY
			    || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxReadKey.Text) == KEY_ERROR.KEY_IS_EMPTY
			    || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxWriteKey.Text) == KEY_ERROR.KEY_IS_EMPTY)
				MessageBox.Show("empty keys are not allowed", "wrong key settings detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
			else if (converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxCardMasterKey.Text) == KEY_ERROR.KEY_HAS_WRONG_FORMAT
			         || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxApplicationMasterKey.Text) == KEY_ERROR.KEY_HAS_WRONG_FORMAT
			         || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxReadKey.Text) == KEY_ERROR.KEY_HAS_WRONG_FORMAT
			         || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxWriteKey.Text) == KEY_ERROR.KEY_HAS_WRONG_FORMAT)
				MessageBox.Show("key must contain of hex characters only", "wrong key settings detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
			else if (converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxCardMasterKey.Text) == KEY_ERROR.KEY_HAS_WRONG_LENGTH
			         || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxApplicationMasterKey.Text) == KEY_ERROR.KEY_HAS_WRONG_LENGTH
			         || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxReadKey.Text) == KEY_ERROR.KEY_HAS_WRONG_LENGTH
			         || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxWriteKey.Text) == KEY_ERROR.KEY_HAS_WRONG_LENGTH)
				MessageBox.Show("please enter a key of 32 characters length", "wrong key settings detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
			else if (converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxCardMasterKey.Text) == KEY_ERROR.NO_ERROR
			         || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxApplicationMasterKey.Text) == KEY_ERROR.NO_ERROR
			         || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxReadKey.Text) == KEY_ERROR.NO_ERROR
			         || converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxWriteKey.Text) == KEY_ERROR.NO_ERROR) {
				
				converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxCardMasterKey.Text);
				desFireCardCardMasterKey = converter.desFireKeyToEdit;
				
				converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxApplicationMasterKey.Text);
				desFireCardApplicationMasterKey = converter.desFireKeyToEdit;
				
				converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxReadKey.Text);
				desFireCardReadKey = converter.desFireKeyToEdit;
				
				converter.FormatMifareDesfireKeyStringWithSpacesEachByte(textBoxWriteKey.Text);
				desFireCardWriteKey = converter.desFireKeyToEdit;		

				desFireCardCardMasterKeyType = converter._constDesfireCardKeyType[comboBoxCardMasterKeyType.SelectedIndex];
				desFireCardApplicationMasterKeyType = converter._constDesfireCardKeyType[comboBoxApplicationMasterKeyType.SelectedIndex];
				desFireCardWriteKeyType = converter._constDesfireCardKeyType[comboBoxReadKeyType.SelectedIndex];
				desFireCardWriteKeyType = converter._constDesfireCardKeyType[comboBoxWriteKeyType.SelectedIndex];
				
				this.Hide();				
				desfireKeySetupChanged();

			}
		}
		
		void ButtonAbortClick(object sender, EventArgs e)
		{
			this.Hide();
		}
		
		

	}
}
