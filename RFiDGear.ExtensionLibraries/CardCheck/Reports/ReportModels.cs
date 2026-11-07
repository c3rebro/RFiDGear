using System;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>Where the reports of the checks are kept.</summary>
    /// <param name="OutputRoot">The folder reports are written to; must be a full path to an existing folder.</param>
    /// <param name="CreateSubdirectory">Whether each order gets its own folder named after its job number.</param>
    public sealed record ReportLocation(string OutputRoot, bool CreateSubdirectory);

    /// <summary>The files that belong to one order's check.</summary>
    public enum ReportFile
    {
        /// <summary>The report of the first run (is the chip supported, how much memory does it have).</summary>
        Step1,

        /// <summary>The report after the operator's answers, before programming.</summary>
        Step2,

        /// <summary>The report of the programming run.</summary>
        Step3,

        /// <summary>The finished, locked report that goes into the order.</summary>
        Final,

        /// <summary>A finished report fetched from the order for editing, as it is stored.</summary>
        Download,

        /// <summary>The working copy of a downloaded report while it is being edited.</summary>
        Edit,
    }

    /// <summary>
    /// What the check has concluded, which decides which working reports may be removed afterwards.
    /// </summary>
    public enum CleanupScope
    {
        /// <summary>The chip is not supported and the check ended after the first run.</summary>
        NotSupported,

        /// <summary>The chip is recognised but cannot be programmed here; the check ended after the first run.</summary>
        SupportedOnly,

        /// <summary>The chip was programmed and the check went through all steps.</summary>
        Programmed,

        /// <summary>A finished report was fetched for editing and the edit is over.</summary>
        ReEdit,
    }

    /// <summary>
    /// Every file path of one order's check. All of them lie in <see cref="Folder"/>, which lies in
    /// <see cref="Root"/>. They are built by <see cref="IReportFiles.Resolve"/> and by nothing else.
    /// </summary>
    /// <param name="Root">The report output folder.</param>
    /// <param name="Folder">The folder the order's files are in: <paramref name="Root"/> itself, or a folder below it named after the job.</param>
    /// <param name="Step1">Path of the report of the first run.</param>
    /// <param name="Step2">Path of the report after the operator's answers.</param>
    /// <param name="Step3">Path of the report of the programming run.</param>
    /// <param name="Final">Path of the finished report.</param>
    /// <param name="Download">Path of a downloaded report.</param>
    /// <param name="Edit">Path of the working copy of a downloaded report.</param>
    public sealed record OrderReportPaths(
        string Root,
        string Folder,
        string Step1,
        string Step2,
        string Step3,
        string Final,
        string Download,
        string Edit)
    {
        /// <summary>The path of one file.</summary>
        /// <param name="file">Which file.</param>
        /// <returns>Its path.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="file"/> is not a defined value.</exception>
        public string PathOf(ReportFile file) => file switch
        {
            ReportFile.Step1 => Step1,
            ReportFile.Step2 => Step2,
            ReportFile.Step3 => Step3,
            ReportFile.Final => Final,
            ReportFile.Download => Download,
            ReportFile.Edit => Edit,
            _ => throw new ArgumentOutOfRangeException(nameof(file), file, "Unknown report file."),
        };
    }
}
