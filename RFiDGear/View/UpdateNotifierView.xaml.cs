using System.Windows.Input;
using System.Windows;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaktionslogik für Splash.xaml
    /// </summary>
    public partial class UpdateNotifierView : Window
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
