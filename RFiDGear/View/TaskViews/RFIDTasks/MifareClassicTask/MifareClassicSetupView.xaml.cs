/*
 * Created by SharpDevelop.
 * Date: 10.10.2017
 * Time: 22:11
 *
 */

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

using Wpf.Ui.Controls;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaction logic for MifareClassicSetupView.xaml
    /// </summary>
    public partial class MifareClassicSetupView : UiWindow
    {
        public MifareClassicSetupView()
        {
            InitializeComponent();
            this.MaxHeight = (uint)SystemParameters.MaximizedPrimaryScreenHeight - 8;
        }

        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
        }
    }
}