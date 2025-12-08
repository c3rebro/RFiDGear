using System;
using System.Threading.Tasks;

namespace RFiDGear.Services
{
    public interface IUpdateScheduler : IDisposable
    {
        void Begin(Func<Task> onUpdateAvailable);
    }
}
