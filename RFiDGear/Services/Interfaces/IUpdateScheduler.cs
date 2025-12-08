using System;
using System.Threading.Tasks;

namespace RFiDGear.Services.Interfaces
{
    public interface IUpdateScheduler : IDisposable
    {
        void Begin(Func<Task> onUpdateAvailable);
    }
}
