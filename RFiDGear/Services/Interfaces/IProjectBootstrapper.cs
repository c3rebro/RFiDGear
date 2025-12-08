using System;
using System.Threading.Tasks;

namespace RFiDGear.Services.Interfaces
{
    public interface IProjectBootstrapper
    {
        Task BootstrapAsync(ProjectBootstrapRequest request);
    }

    public class ProjectBootstrapRequest
    {
        public string ProjectFilePath { get; set; }

        public bool Autorun { get; set; }

        public Func<object> CreateSplashScreen { get; set; }

        public Action<object> AddDialog { get; set; }

        public Action RemoveSplash { get; set; }

        public Func<string, Task> OpenProjectAsync { get; set; }

        public Func<Task> ResetTaskStatusAsync { get; set; }

        public Func<Task> ReadChipAsync { get; set; }

        public Func<Task> WriteOnceAsync { get; set; }

        public Action<string> UpdateDateTime { get; set; }

        public Action<string> SetCurrentReader { get; set; }

        public Action<int> SetReaderPort { get; set; }

        public Action<System.Globalization.CultureInfo> SetCulture { get; set; }
    }
}
