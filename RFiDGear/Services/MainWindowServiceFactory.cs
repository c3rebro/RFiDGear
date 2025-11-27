using System;

namespace RFiDGear.Services
{
    public class MainWindowServiceFactory : IMainWindowServiceFactory
    {
        private readonly Lazy<IReaderInitializer> readerInitializer;
        private readonly Lazy<IUpdateChecker> updateChecker;
        private readonly Lazy<IPollingScheduler> pollingScheduler;
        private readonly Lazy<Commands.ICommandMenuProvider> commandMenuProvider;

        public MainWindowServiceFactory()
        {
            readerInitializer = new Lazy<IReaderInitializer>(() => new ReaderInitializer());
            updateChecker = new Lazy<IUpdateChecker>(() => new UpdateChecker());
            pollingScheduler = new Lazy<IPollingScheduler>(() => new PollingScheduler());
            commandMenuProvider = new Lazy<Commands.ICommandMenuProvider>(() => new Commands.CommandMenuProvider());
        }

        public IReaderInitializer CreateReaderInitializer() => readerInitializer.Value;
        public IUpdateChecker CreateUpdateChecker() => updateChecker.Value;
        public IPollingScheduler CreatePollingScheduler() => pollingScheduler.Value;
        public Commands.ICommandMenuProvider CreateCommandMenuProvider() => commandMenuProvider.Value;
    }
}
