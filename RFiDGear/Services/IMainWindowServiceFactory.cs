namespace RFiDGear.Services
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
