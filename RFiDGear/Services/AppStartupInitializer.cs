using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

#nullable enable

namespace RFiDGear.Services
{
    /// <summary>
    /// Defines the startup contract for initializing application-wide resources before
    /// the main window is shown.
    /// </summary>
    public interface IAppStartupInitializer
    {
        /// <summary>
        /// Creates the shared startup context, including the Windows event log handle,
        /// single-instance mutex, and parsed command-line arguments.
        /// </summary>
        /// <returns>An initialized <see cref="AppStartupContext"/>.</returns>
        AppStartupContext Initialize();
    }

    /// <summary>
    /// Holds process-wide objects produced during application startup.
    /// </summary>
    public class AppStartupContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppStartupContext"/> class.
        /// </summary>
        /// <param name="eventLog">Event log used for application-level tracing.</param>
        /// <param name="mutex">Mutex ensuring the app runs as a single instance.</param>
        /// <param name="arguments">Command-line arguments supplied to the process.</param>
        public AppStartupContext(EventLog eventLog, Mutex mutex, string[] arguments)
        {
            EventLog = eventLog;
            Mutex = mutex ?? throw new ArgumentNullException(nameof(mutex));
            Arguments = arguments ?? Array.Empty<string>();
        }

        public EventLog? EventLog { get; }

        public Mutex Mutex { get; }

        public string[] Arguments { get; }
    }

    /// <summary>
    /// Default initializer that sets up the Windows event log source, enforces a
    /// single running instance, and captures command-line arguments for downstream
    /// services.
    /// </summary>
    public class AppStartupInitializer : IAppStartupInitializer
    {
        /// <inheritdoc />
        public AppStartupContext Initialize()
        {
            var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "RFiDGear";

            if (!EventLog.SourceExists(assemblyName))
            {
                EventLog.CreateEventSource(new EventSourceCreationData(assemblyName, "Application"));
            }

            var eventLog = new EventLog("Application", ".", assemblyName)
            {
                Source = assemblyName
            };

            var mutex = new Mutex(true, "App", out var isANewInstance);

            if (!isANewInstance)
            {
                Environment.Exit(0);
            }

            return new AppStartupContext(eventLog, mutex, Environment.GetCommandLineArgs());
        }
    }
}
