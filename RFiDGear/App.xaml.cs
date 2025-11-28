using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RFiDGear
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear", "log");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "log-.txt");

        static App()
        {
            ConfigureLogging();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);
        }

        private static void ConfigureLogging()
        {
            Directory.CreateDirectory(LogDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.File(
                    LogFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true)
                .CreateLogger();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.ForContext<App>().Fatal(e.Exception, "Unhandled dispatcher exception");
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.ForContext<App>().Fatal(ex, "Unhandled domain exception");
            }
            else
            {
                Log.ForContext<App>().Fatal("Unhandled domain exception: {ExceptionObject}", e.ExceptionObject);
            }
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.ForContext<App>().Fatal(e.Exception, "Unobserved task exception");
            e.SetObserved();
        }
    }
}
