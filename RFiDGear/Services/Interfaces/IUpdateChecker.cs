using System.Threading.Tasks;

namespace RFiDGear.Services.Interfaces
{
    public interface IUpdateChecker
    {
        bool UpdateAvailable { get; }
        string UpdateInfoText { get; }
        bool AllowUpdate { get; set; }
        Task StartMonitoringAsync();
        Task StopMonitoringAsync();
        Task ApplyUpdateAsync();
    }
}
