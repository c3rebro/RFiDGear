using System;
using RFiDGear.Services.Interfaces;

namespace RFiDGear.Services.Factories
{
    public class MainWindowServiceFactory : IMainWindowServiceFactory
    {
        private readonly Lazy<IReaderInitializer> readerInitializer;
        private readonly Lazy<IUpdateChecker> updateChecker;
        private readonly Lazy<IPollingScheduler> pollingScheduler;
        private readonly Lazy<Commands.ICommandMenuProvider> commandMenuProvider;
        private readonly Lazy<Commands.ICommandMenuBuilder> commandMenuBuilder;
        private readonly Lazy<IStartupConfigurator> startupConfigurator;
        private readonly Lazy<ITimerFactory> timerFactory;
        private readonly Lazy<IAppStartupInitializer> appStartupInitializer;
        private readonly Lazy<ITaskServiceInitializer> taskServiceInitializer;
        private readonly Lazy<IMenuInitializer> menuInitializer;

        public MainWindowServiceFactory()
        {
            readerInitializer = new Lazy<IReaderInitializer>(() => new ReaderInitializer());
            updateChecker = new Lazy<IUpdateChecker>(() => new UpdateChecker());
            pollingScheduler = new Lazy<IPollingScheduler>(() => new PollingScheduler());
            commandMenuProvider = new Lazy<Commands.ICommandMenuProvider>(() => new Commands.CommandMenuProvider());
            commandMenuBuilder = new Lazy<Commands.ICommandMenuBuilder>(() => new Commands.CommandMenuBuilder(commandMenuProvider.Value));
            startupConfigurator = new Lazy<IStartupConfigurator>(() => new StartupConfigurator(readerInitializer.Value));
            timerFactory = new Lazy<ITimerFactory>(() => new MainWindowTimerFactory());
            appStartupInitializer = new Lazy<IAppStartupInitializer>(() => new AppStartupInitializer());
            taskServiceInitializer = new Lazy<ITaskServiceInitializer>(() => new TaskServiceInitializer());
            menuInitializer = new Lazy<IMenuInitializer>(() => new MenuInitializer());
        }

        public IReaderInitializer CreateReaderInitializer() => readerInitializer.Value;
        public IUpdateChecker CreateUpdateChecker() => updateChecker.Value;
        public IPollingScheduler CreatePollingScheduler() => pollingScheduler.Value;
        public Commands.ICommandMenuProvider CreateCommandMenuProvider() => commandMenuProvider.Value;
        public Commands.ICommandMenuBuilder CreateCommandMenuBuilder() => commandMenuBuilder.Value;
        public IStartupConfigurator CreateStartupConfigurator() => startupConfigurator.Value;
        public ITimerFactory CreateTimerFactory() => timerFactory.Value;
        public IAppStartupInitializer CreateAppStartupInitializer() => appStartupInitializer.Value;
        public ITaskServiceInitializer CreateTaskServiceInitializer() => taskServiceInitializer.Value;
        public IMenuInitializer CreateMenuInitializer() => menuInitializer.Value;
    }
}
