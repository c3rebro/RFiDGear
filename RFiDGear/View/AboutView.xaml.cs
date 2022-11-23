using System.Diagnostics;
using System.Windows.Navigation;
using System.Windows.Input;

using Wpf.Ui.Controls;

namespace RFiDGear.View
{
    /// <summary>
    /// Interaktionslogik für Splash.xaml
    /// </summary>
    public partial class AboutView : UiWindow
    {
        public AboutView()
        {
            InitializeComponent();
        }

        private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://docs.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
