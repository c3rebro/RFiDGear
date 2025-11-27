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
        IMainWindowTimerFactory CreateMainWindowTimerFactory();
    }
}
