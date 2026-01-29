using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;
using RFiDGear.Infrastructure;

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
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

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

        /// <summary>
        /// Resolves extension assemblies that are stored in the ProgramData extensions folder.
        /// </summary>
        /// <param name="sender">The domain raising the resolve event.</param>
        /// <param name="args">The resolution arguments.</param>
        /// <returns>The loaded assembly or null if not handled.</returns>
        internal static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args == null || string.IsNullOrWhiteSpace(args.Name))
            {
                return null;
            }

            var assemblyName = new AssemblyName(args.Name);
            var candidatePath = GetExtensionAssemblyPath(MefHelper.Instance.ExtensionsPath, assemblyName);
            if (candidatePath == null)
            {
                return null;
            }

            try
            {
                return Assembly.LoadFrom(candidatePath);
            }
            catch (Exception ex)
            {
                Log.ForContext<App>().Error(
                    ex,
                    "Failed to resolve extension assembly {AssemblyName} from {AssemblyPath}",
                    assemblyName.FullName,
                    candidatePath);
                return null;
            }
        }

        /// <summary>
        /// Gets the extension assembly path for a requested assembly name.
        /// </summary>
        /// <param name="extensionsPath">The extensions directory to probe.</param>
        /// <param name="assemblyName">The assembly name to resolve.</param>
        /// <returns>The full path to the extension assembly, or null if it cannot be resolved.</returns>
        internal static string GetExtensionAssemblyPath(string extensionsPath, AssemblyName assemblyName)
        {
            if (assemblyName == null ||
                string.IsNullOrWhiteSpace(assemblyName.Name) ||
                !assemblyName.Name.StartsWith("RFiDGear.Extensions", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(extensionsPath))
            {
                return null;
            }

            var candidatePath = Path.Combine(extensionsPath, $"{assemblyName.Name}.dll");
            return File.Exists(candidatePath) ? candidatePath : null;
        }
    }
}
