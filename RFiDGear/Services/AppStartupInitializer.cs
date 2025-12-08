using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace RFiDGear.Services
{
    public interface IAppStartupInitializer
    {
        AppStartupContext Initialize();
    }

    public class AppStartupContext
    {
        public AppStartupContext(EventLog eventLog, Mutex mutex, string[] arguments)
        {
            EventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
            Mutex = mutex ?? throw new ArgumentNullException(nameof(mutex));
            Arguments = arguments ?? Array.Empty<string>();
        }

        public EventLog EventLog { get; }

        public Mutex Mutex { get; }

        public string[] Arguments { get; }
    }

    public class AppStartupInitializer : IAppStartupInitializer
    {
        public AppStartupContext Initialize()
        {
            var assemblyName = Assembly.GetEntryAssembly().GetName().Name;

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
