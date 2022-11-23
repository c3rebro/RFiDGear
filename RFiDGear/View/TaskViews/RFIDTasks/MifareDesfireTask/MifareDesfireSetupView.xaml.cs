/*
 * Created by SharpDevelop.
 * Date: 10.10.2017
 * Time: 22:10
 *
 */

using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaction logic for MifareDesfireSetupView.xaml
    /// </summary>
    public partial class MifareDesfireSetupView : UiWindow
    {
        public MifareDesfireSetupView()
        {
            InitializeComponent();
            this.MaxHeight = (uint)SystemParameters.MaximizedPrimaryScreenHeight - 8;
        }

        private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}