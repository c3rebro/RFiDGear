using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>A settings file of the old Card Check Assistant that was found on this computer.</summary>
    /// <param name="Path">The full path of the file.</param>
    /// <param name="Kind">Where it was found: <c>Unpackaged</c> or <c>Packaged</c> (the Microsoft Store style install).</param>
    /// <param name="LastWriteUtc">When the file was last changed (UTC).</param>
    /// <param name="Length">The size of the file in bytes.</param>
    public sealed record LegacySettingsCandidate(string Path, string Kind, DateTime LastWriteUtc, long Length);

    /// <summary>What happened to one element of the old settings file.</summary>
    public enum LegacyElementDisposition
    {
        /// <summary>The value is taken over (unless the new settings already have one).</summary>
        Imported,

        /// <summary>The element is there but empty.</summary>
        Empty,

        /// <summary>The element is there but its value cannot be used.</summary>
        Invalid,

        /// <summary>A setting that does not exist in this version (theme, updates, the reader volume, ...).</summary>
        NotUsed,

        /// <summary>A credential. It is skipped while reading and never taken into a setting, kept or logged.</summary>
        NeverRead,

        /// <summary>Not taken over because of another setting (database settings of an SQLite configuration).</summary>
        Skipped,

        /// <summary>An element this version does not know.</summary>
        Unknown,
    }

    /// <summary>One element of the old file and what became of it. Never contains a value.</summary>
    /// <param name="Element">The element name, for example <c>SelectedDBServerName</c>.</param>
    /// <param name="Disposition">What became of it.</param>
    public sealed record LegacyImportItem(string Element, LegacyElementDisposition Disposition);

    /// <summary>Something the operator should know about the import. Fixed text, never a value.</summary>
    /// <param name="Code">A stable identifier such as <c>import.sqlite.unsupported</c>.</param>
    /// <param name="Element">The element it is about; empty if it is about the file as a whole.</param>
    /// <param name="Message">Fixed text that is safe to show.</param>
    public sealed record ImportNote(string Code, string Element, string Message);

    /// <summary>
    /// What importing a file would do, worked out without changing anything. <see cref="Proposed"/> is the new
    /// settings with the imported values filled in; nothing has been written.
    /// </summary>
    /// <param name="SourcePath">The file that was read.</param>
    /// <param name="SourceSha256">SHA-256 of the file's bytes in hex; applying checks that the file is still the same.</param>
    /// <param name="Proposed">The settings as they would be saved.</param>
    /// <param name="Items">Every element of the file and what became of it, in file order.</param>
    /// <param name="Notes">What the operator should know, for example that SQLite is not supported.</param>
    public sealed record LegacyImportPreview(
        string SourcePath,
        string SourceSha256,
        CardCheckSettings Proposed,
        IReadOnlyList<LegacyImportItem> Items,
        IReadOnlyList<ImportNote> Notes);

    /// <summary>What the operator decided when asked about the old settings.</summary>
    public enum ImportChoice
    {
        /// <summary>Ask again next time.</summary>
        NotNow,

        /// <summary>Import the file named in the decision.</summary>
        Import,

        /// <summary>Do not import and do not ask again.</summary>
        Never,
    }

    /// <summary>The answer to the question about the old settings.</summary>
    /// <param name="Choice">What the operator chose.</param>
    /// <param name="SourcePath">For <see cref="ImportChoice.Import"/>: the file to import, one of the candidates or one the operator picked.</param>
    public sealed record ImportDecision(ImportChoice Choice, string? SourcePath = null);

    /// <summary>How an offer to import ended.</summary>
    public enum ImportOutcome
    {
        /// <summary>The old settings were imported.</summary>
        Imported,

        /// <summary>No old settings were found; nothing was asked.</summary>
        NothingToImport,

        /// <summary>The question was answered before (imported or declined); nothing was asked.</summary>
        AlreadyHandled,

        /// <summary>The operator chose not to import now; nothing was written.</summary>
        NotNow,

        /// <summary>The operator chose never to import; this was remembered.</summary>
        Declined,

        /// <summary>The operator was shown what would be imported and said no; nothing was written.</summary>
        Cancelled,

        /// <summary>The settings cannot be written (a newer version's file, a file held by another program).</summary>
        Unavailable,

        /// <summary>The import was tried and failed; see <see cref="ImportOffer.Failure"/>.</summary>
        Failed,
    }

    /// <summary>The result of an offer to import.</summary>
    /// <param name="Outcome">How it ended.</param>
    /// <param name="Notes">What the operator should know (only after an import).</param>
    /// <param name="Failure">Why it failed (only for <see cref="ImportOutcome.Failed"/> and <see cref="ImportOutcome.Unavailable"/>).</param>
    public sealed record ImportOffer(ImportOutcome Outcome, IReadOnlyList<ImportNote> Notes, Failure? Failure = null)
    {
        internal static ImportOffer Of(ImportOutcome outcome) => new ImportOffer(outcome, Array.Empty<ImportNote>());
    }

    /// <summary>
    /// The questions the import asks the operator. The extension implements this with its own dialogs; the library
    /// never shows anything itself. Implementations run their dialogs on the thread they need.
    /// </summary>
    public interface IImportPrompt
    {
        /// <summary>Asks whether and what to import.</summary>
        /// <param name="candidates">The settings files found, newest first. Never empty: nothing is asked when none was found.</param>
        /// <param name="cancellationToken">Cancels the question.</param>
        /// <returns>The operator's decision.</returns>
        Task<ImportDecision> ChooseAsync(IReadOnlyList<LegacySettingsCandidate> candidates, CancellationToken cancellationToken);

        /// <summary>Shows what importing the chosen file would do and asks for confirmation.</summary>
        /// <param name="preview">What would happen; nothing has been written.</param>
        /// <param name="cancellationToken">Cancels the question.</param>
        /// <returns><c>true</c> to import, <c>false</c> to leave everything as it is.</returns>
        Task<bool> ConfirmAsync(LegacyImportPreview preview, CancellationToken cancellationToken);
    }
}
