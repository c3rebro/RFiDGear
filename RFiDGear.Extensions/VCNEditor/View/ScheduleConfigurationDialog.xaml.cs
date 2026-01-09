/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 08/28/2017
 * Time: 23:14
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using RFiDGear.Extensions.VCNEditor.ViewModel;

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace RFiDGear.Extensions.VCNEditor.View
{
    /// <summary>
    /// Interaction logic for SceduleConfigurationDialog.xaml
    /// </summary>
    public partial class ScheduleConfigurationDialog : Window
    {
        public ScheduleConfigurationDialog()
        {
            InitializeComponent();
        }

        void TextBoxStartTimeMouseWheelUsed(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta <= 0)
            {
                startTimeTextBox.Text = e.Delta.ToString() + ":MouseWheel";
            }
            else
            {
                startTimeTextBox.Text = e.Delta.ToString() + ":MouseWheel";
            }
        }

        void TextBoxEndTimeMouseWheelUsed(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta <= 0)
            {
                endTimeTextBox.Text = e.Delta.ToString() + ":MouseWheel";
            }
            else
            {
                endTimeTextBox.Text = e.Delta.ToString() + ":MouseWheel";
            }
        }
    }
}