/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 14.12.2016
 * Time: 21:36
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.ComponentModel;
using MahApps.Metro.Controls;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

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