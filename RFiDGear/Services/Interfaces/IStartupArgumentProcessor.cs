using System.Collections.Generic;

namespace RFiDGear.Services.Interfaces
{
    public interface IStartupArgumentProcessor
    {
        StartupArgumentResult Process(string[] args);
    }

    public class StartupArgumentResult
    {
        public string ReportOutputPath { get; set; }

        public string ReportTemplateFile { get; set; }

        public string ProjectFilePath { get; set; }

        public bool Autorun { get; set; }

        public bool AutoMode { get; set; }

        /// <summary><see langword="true"/> for <c>EXTERNALCONTROL=1</c>: the external control bridge is on for this run, whatever the setting says.</summary>
        public bool ExternalControl { get; set; }

        public Dictionary<string, string> Variables { get; } = new Dictionary<string, string>();
    }
}
