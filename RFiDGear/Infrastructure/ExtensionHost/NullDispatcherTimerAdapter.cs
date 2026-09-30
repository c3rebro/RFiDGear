using RFiDGear.Services.TaskExecution;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// No-op <see cref="IDispatcherTimerAdapter"/> used by <see cref="HeadlessExtensionHost"/>
    /// where no WPF dispatcher is available.
    /// </summary>
    internal sealed class NullDispatcherTimerAdapter : IDispatcherTimerAdapter
    {
        public bool IsEnabled { get; set; }
        public object Tag { get; set; }
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
