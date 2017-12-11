/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 21:39
 *
 */

using System.ComponentModel;
using System.Windows.Controls;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaction logic for TabPageMifareClassicDataBlockAccessBitsView.xaml
    /// </summary>
    public partial class TabPageMifareClassicKeySetupView : UserControl
    {
        public TabPageMifareClassicKeySetupView()
        {
            InitializeComponent();
        }

        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
        }
    }
}