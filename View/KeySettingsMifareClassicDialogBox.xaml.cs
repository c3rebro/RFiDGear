using System;
using System.ComponentModel;
using System.Windows.Controls;


namespace RFiDGear.View
{
	/// <summary>
	/// Interaction logic for KeySettingsMifareClassicDialogBox.xaml
	/// </summary>
	public partial class KeySettingsMifareClassicDialogBox
	{
		public KeySettingsMifareClassicDialogBox()
		{
			InitializeComponent();
		}
		
		private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
		{
			e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
		}
	}
}