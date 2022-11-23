using System.Windows.Input;
using Wpf.Ui.Controls;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaktionslogik für Splash.xaml
    /// </summary>
    public partial class UpdateNotifierView : UiWindow
    {
        public UpdateNotifierView()
        {
            InitializeComponent();
        }

        private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
