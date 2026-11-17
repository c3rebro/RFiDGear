/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 24.04.2018
 * Time: 22:37
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RFiDGear.ViewModel.TaskSetupViewModels;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaction logic for MifareUltralightSetupView.xaml
    /// </summary>
    public partial class MifareUltralightSetupView : Window
    {
        public MifareUltralightSetupView()
        {
            InitializeComponent();
            this.MaxHeight = (uint)SystemParameters.MaximizedPrimaryScreenHeight - 8;

            // The buttons are off while an operation runs, and so is the close button of the window: a write that waits for its key
            // would reach the card after the dialog was gone.
            Closing += (_, e) => e.Cancel = DataContext is MifareUltralightSetupViewModel { IsRunning: true };
        }
    }
}