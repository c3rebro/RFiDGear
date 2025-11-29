/*
 * Created by SharpDevelop.
 * Date: 10.10.2017
 * Time: 22:11
 *
 */

using System.Windows;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaction logic for CreateReportTaskView.xaml
    /// </summary>
    public partial class GenericChipTaskView : Window
    {
        public GenericChipTaskView()
        {
            InitializeComponent();
            this.MaxHeight = (uint)SystemParameters.MaximizedPrimaryScreenHeight - 8;
        }
    }
}