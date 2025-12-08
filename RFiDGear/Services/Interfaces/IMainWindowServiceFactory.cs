using RFiDGear.Services.Factories;

namespace RFiDGear.Services.Interfaces
{
    public interface IMainWindowServiceFactory
    {
        IReaderInitializer CreateReaderInitializer();
        IUpdateChecker CreateUpdateChecker();
        IPollingScheduler CreatePollingScheduler();
        Commands.ICommandMenuProvider CreateCommandMenuProvider();
        Commands.ICommandMenuBuilder CreateCommandMenuBuilder();
        IStartupConfigurator CreateStartupConfigurator();
        ITimerFactory CreateTimerFactory();
        IAppStartupInitializer CreateAppStartupInitializer();
        ITaskServiceInitializer CreateTaskServiceInitializer();
        IMenuInitializer CreateMenuInitializer();
    }
}
