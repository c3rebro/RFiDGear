/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 04/18/2018
 * Time: 21:50
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

namespace VCNEditor.View
{
    /// <summary>
    /// Interaction logic for ExpiryConfigurationDialog.xaml
    /// </summary>
    public partial class ExpiryConfigurationDialog : Window
    {
        public ExpiryConfigurationDialog()
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