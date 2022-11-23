/*
 * Created by SharpDevelop.
 * Date: 10.10.2017
 * Time: 22:11
 *
 */

using System.Windows;

using Wpf.Ui.Controls;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaction logic for CreateReportTaskView.xaml
    /// </summary>
    public partial class GenericChipTaskView : UiWindow
    {
        public GenericChipTaskView()
        {
            InitializeComponent();
            this.MaxHeight = (uint)SystemParameters.MaximizedPrimaryScreenHeight - 8;
        }
    }
}