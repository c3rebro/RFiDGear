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

        /// <summary>
        /// Gets or sets the report target of the arguments as it was typed (<c>REPORTTARGETPATH</c>, <c>REPORTOUTPUTPATH</c>), with
        /// its <c>?</c>, <c>??</c> or <c>???</c> not yet replaced. <see cref="ReportOutputPath"/> is that pattern resolved once, for the
        /// moment the arguments were read; a report that is made for every card resolves the pattern again for each run.
        /// </summary>
        public string ReportOutputPattern { get; set; }

        public string ReportTemplateFile { get; set; }

        public string ProjectFilePath { get; set; }

        public bool Autorun { get; set; }

        public bool AutoMode { get; set; }

        /// <summary><see langword="true"/> for <c>EXTERNALCONTROL=1</c>: the external control bridge is on for this run, whatever the setting says.</summary>
        public bool ExternalControl { get; set; }

        public Dictionary<string, string> Variables { get; } = new Dictionary<string, string>();
    }
}
