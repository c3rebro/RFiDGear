using System;
using System.Windows;
using RFiDGear.Services;
using RFiDGear.ViewModel;

namespace RFiDGear
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            Current.MainWindow = mainWindow;

            var services = new MainWindowServiceFactory();
            mainWindow.DataContext = new MainWindowViewModel(services);

            mainWindow.Show();
        }
    }
}