/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 21:38
 * 
 */
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace RFiDGear.View
{
	/// <summary>
	/// Interaction logic for TabPageMifareClassicSectorAccessBitsView.xaml
	/// </summary>
	public partial class TabPageMifareClassicSectorAccessBitsView : UserControl
	{
		public TabPageMifareClassicSectorAccessBitsView()
		{
			InitializeComponent();
		}
		
		private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
		{
			e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
		}
	}
}