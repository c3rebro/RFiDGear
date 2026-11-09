using System;
using System.Collections.Generic;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Where the order database is. There is deliberately no user name, password or port: the connection uses the
    /// Windows account of the person running the check, as the old assistant effectively did.
    /// </summary>
    public sealed record DatabaseSettings
    {
        /// <summary>The server, for example <c>SQLSRV01</c> or <c>SQLSRV01\INSTANCE</c>; empty until entered.</summary>
        public string Server { get; init; } = string.Empty;

        /// <summary>The database on that server; empty until entered.</summary>
        public string Database { get; init; } = string.Empty;

        /// <summary>The order table, as a bare name or with schema, for example <c>dbo.T_CardCheck</c>.</summary>
        public string Table { get; init; } = "T_CardCheck";
    }

    /// <summary>How reports are kept and which language a check uses unless the operator chooses another.</summary>
    public sealed record ReportSettings
    {
        /// <summary>The folder reports are written to; empty until entered.</summary>
        public string OutputPath { get; init; } = string.Empty;

        /// <summary>Whether each order gets its own folder named after its job number.</summary>
        public bool CreateSubdirectory { get; init; }

        /// <summary>Whether working reports are removed when a check is finished.</summary>
        public bool RemoveTemporaryReports { get; init; }

        /// <summary>The language of the check unless the operator picks another one.</summary>
        public ReportLanguage DefaultLanguage { get; init; } = ReportLanguage.German;
    }

    /// <summary>A named block of text the operator can insert into the notes of a report.</summary>
    public sealed record TextTemplate
    {
        /// <summary>The name shown in the list.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>The text that is inserted.</summary>
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// Which settings of the old assistant were imported, and from which file, so the offer is made once.
    /// </summary>
    public sealed record LegacyImportMarker
    {
        /// <summary>SHA-256 of the old settings file that was imported, in hex; empty if the import was declined.</summary>
        public string SourceSha256 { get; init; } = string.Empty;

        /// <summary>Whether the operator chose never to import; nothing was imported then.</summary>
        public bool Declined { get; init; }

        /// <summary>When it was imported (UTC).</summary>
        public DateTimeOffset ImportedAtUtc { get; init; }
    }

    /// <summary>
    /// Everything the Card Check Assistant remembers between runs. A new value of this type is built to change
    /// something; an instance never changes after it was created.
    /// </summary>
    public sealed record CardCheckSettings
    {
        /// <summary>Where the order database is.</summary>
        public DatabaseSettings Database { get; init; } = new DatabaseSettings();

        /// <summary>How reports are kept.</summary>
        public ReportSettings Report { get; init; } = new ReportSettings();

        /// <summary>
        /// Whether the assistant is in test mode: it works on made-up stub orders and does not use the order database.
        /// Off unless the operator switches it on.
        /// </summary>
        public bool UseStubJobs { get; init; }

        /// <summary>The project run for a check unless another one is chosen; empty until entered.</summary>
        public string DefaultProjectPath { get; init; } = string.Empty;

        /// <summary>The project last used from outside the default one; empty if none.</summary>
        public string CustomProjectPath { get; init; } = string.Empty;

        /// <summary>The folder the project picker opened last, so the next pick starts there.</summary>
        public string LastProjectFolder { get; init; } = string.Empty;

        /// <summary>The folder the report picker opened last.</summary>
        public string LastReportFolder { get; init; } = string.Empty;

        /// <summary>
        /// The text blocks for the report notes, in the order they are shown. The list that is assigned is copied, so
        /// changing it afterwards changes nothing here, and the list handed out cannot be changed either: an instance
        /// of this type never changes, and neither does any copy made with <c>with</c>.
        /// </summary>
        public IReadOnlyList<TextTemplate> TextTemplates
        {
            get => _textTemplates;
            init => _textTemplates = value == null ? Array.Empty<TextTemplate>() : Array.AsReadOnly(System.Linq.Enumerable.ToArray(value));
        }

        private readonly IReadOnlyList<TextTemplate> _textTemplates = Array.Empty<TextTemplate>();

        /// <summary>The import of the old assistant's settings, once it was done; otherwise <c>null</c>.</summary>
        public LegacyImportMarker? LegacyImport { get; init; }
    }
}
