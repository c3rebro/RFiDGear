using System;
using System.ComponentModel;
using System.Windows.Controls;


namespace RFiDGear.View
{
	/// <summary>
	/// Interaction logic for MifareAuthSettingsDialogBox.xaml
	/// </summary>
	public partial class MifareAuthSettingsDialogBox
	{
		public MifareAuthSettingsDialogBox()
		{
			InitializeComponent();
		}
		
		private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
		{
			e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
		}
	}
}