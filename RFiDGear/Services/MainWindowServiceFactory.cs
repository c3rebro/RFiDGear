using System;

namespace RFiDGear.Services
{
    public class MainWindowServiceFactory : IMainWindowServiceFactory
    {
        private readonly Lazy<IReaderInitializer> readerInitializer;
        private readonly Lazy<IUpdateChecker> updateChecker;
        private readonly Lazy<IPollingScheduler> pollingScheduler;
        private readonly Lazy<Commands.ICommandMenuProvider> commandMenuProvider;
        private readonly Lazy<Commands.ICommandMenuBuilder> commandMenuBuilder;
        private readonly Lazy<IStartupConfigurator> startupConfigurator;
        private readonly Lazy<IMainWindowTimerFactory> timerFactory;

        public MainWindowServiceFactory()
        {
            readerInitializer = new Lazy<IReaderInitializer>(() => new ReaderInitializer());
            updateChecker = new Lazy<IUpdateChecker>(() => new UpdateChecker());
            pollingScheduler = new Lazy<IPollingScheduler>(() => new PollingScheduler());
            commandMenuProvider = new Lazy<Commands.ICommandMenuProvider>(() => new Commands.CommandMenuProvider());
            commandMenuBuilder = new Lazy<Commands.ICommandMenuBuilder>(() => new Commands.CommandMenuBuilder(commandMenuProvider.Value));
            startupConfigurator = new Lazy<IStartupConfigurator>(() => new StartupConfigurator(readerInitializer.Value));
            timerFactory = new Lazy<IMainWindowTimerFactory>(() => new MainWindowTimerFactory());
        }

        public IReaderInitializer CreateReaderInitializer() => readerInitializer.Value;
        public IUpdateChecker CreateUpdateChecker() => updateChecker.Value;
        public IPollingScheduler CreatePollingScheduler() => pollingScheduler.Value;
        public Commands.ICommandMenuProvider CreateCommandMenuProvider() => commandMenuProvider.Value;
        public Commands.ICommandMenuBuilder CreateCommandMenuBuilder() => commandMenuBuilder.Value;
        public IStartupConfigurator CreateStartupConfigurator() => startupConfigurator.Value;
        public IMainWindowTimerFactory CreateMainWindowTimerFactory() => timerFactory.Value;
    }
}
