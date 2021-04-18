/*
 * Created by SharpDevelop.
 * Date: 10.10.2017
 * Time: 22:11
 *
 */

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaction logic for CreateReportTaskView.xaml
    /// </summary>
    public partial class CommonTaskView : Window
    {
        public CommonTaskView()
        {
            InitializeComponent();
        }

        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
        }
    }
}