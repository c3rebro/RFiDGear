﻿/*
 * Created by SharpDevelop.
 * Date: 10/10/2017
 * Time: 20:24
 *
 */

using System.Windows;
using System.Windows.Input;
using System.Windows;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaction logic for SetupDialogBoxView.xaml
    /// </summary>
    public partial class SetupView : Window
    {
        public SetupView()
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