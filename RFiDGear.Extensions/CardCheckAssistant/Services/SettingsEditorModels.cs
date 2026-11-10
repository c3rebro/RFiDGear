using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>The language a report is made in unless the operator picks another one for a check.</summary>
    internal enum ReportLanguageChoice
    {
        /// <summary>German.</summary>
        German,

        /// <summary>English.</summary>
        English
    }

    /// <summary>A named block of text the operator can put into the notes of a report.</summary>
    /// <param name="Name">The name shown in the list.</param>
    /// <param name="Content">The text that is inserted.</param>
    internal sealed record TextBlockData(string Name, string Content);

    /// <summary>Where the order database is. The connection uses the Windows account of whoever runs the check.</summary>
    /// <param name="Server">The server, <c>host</c> or <c>host\instance</c>.</param>
    /// <param name="Database">The database on that server.</param>
    /// <param name="Table">The order table, with or without schema.</param>
    internal sealed record DatabaseData(string Server, string Database, string Table);

    /// <summary>
    /// What the settings UI shows and edits, as plain values of the extension's own: no type of the support library, so the
    /// view model that works on it names none. Whatever the UI does not edit (the marker of an earlier import, ...)
    /// is not in here and is left alone when the data is saved.
    /// </summary>
    internal sealed record SettingsData
    {
        /// <summary>The folder reports are written to; blank until entered.</summary>
        public string OutputFolder { get; init; } = string.Empty;

        /// <summary>Whether each order gets a folder of its own, named after its job number.</summary>
        public bool OneFolderPerJob { get; init; }

        /// <summary>Whether working reports are removed when a check is finished.</summary>
        public bool RemoveTemporaryReports { get; init; }

        /// <summary>The language of a report unless the operator picks another one.</summary>
        public ReportLanguageChoice ReportLanguage { get; init; } = ReportLanguageChoice.German;

        /// <summary>The project a check runs first; blank until entered.</summary>
        public string DefaultProjectPath { get; init; } = string.Empty;

        /// <summary>The project that reads the memory of the card in the last step; blank until entered.</summary>
        public string MemoryProjectPath { get; init; } = string.Empty;

        /// <summary>Where the order database is.</summary>
        public DatabaseData Database { get; init; } = new DatabaseData(string.Empty, string.Empty, "T_CardCheck");

        /// <summary>Whether the assistant is in test mode and works on stub jobs instead of the order database.</summary>
        public bool UseStubJobs { get; init; }

        /// <summary>The text blocks, in the order shown.</summary>
        public IReadOnlyList<TextBlockData> TextBlocks { get; init; } = new List<TextBlockData>();
    }

    /// <summary>What is wrong with the settings file when the UI opens, if anything.</summary>
    internal enum SettingsProblem
    {
        /// <summary>Nothing.</summary>
        None,

        /// <summary>The file was damaged. It was kept under another name and the defaults are shown.</summary>
        DamagedFileSetAside,

        /// <summary>The file was written by a newer version. It is shown as defaults and cannot be saved over.</summary>
        NewerVersion,

        /// <summary>Another program holds the file just now. The defaults are shown and saving is refused.</summary>
        Unreadable,

        /// <summary>There is no place to keep settings (the user has no application data folder).</summary>
        Unavailable
    }

    /// <summary>The settings as they were read, and what is wrong with them.</summary>
    /// <param name="Data">The values to show.</param>
    /// <param name="Problem">What is wrong with the file, if anything.</param>
    internal sealed record SettingsEditorLoad(SettingsData Data, SettingsProblem Problem);

    /// <summary>How a save ended.</summary>
    internal enum SettingsSaveOutcome
    {
        /// <summary>The settings were saved.</summary>
        Saved,

        /// <summary>The file was written by a newer version and is not overwritten.</summary>
        NewerVersion,

        /// <summary>The file cannot be read just now, so it is not replaced.</summary>
        Unreadable,

        /// <summary>Another window or program held the file for too long.</summary>
        Busy,

        /// <summary>The settings are too large to keep.</summary>
        TooLarge,

        /// <summary>There is no place to keep settings.</summary>
        Unavailable,

        /// <summary>The file could not be written (no right, disk full, ...).</summary>
        Failed
    }

    /// <summary>What a test of the order database found. None of it names the server, the database or the table.</summary>
    internal enum ConnectionTestOutcome
    {
        /// <summary>The server answered and the table has the columns the assistant uses.</summary>
        Succeeded,

        /// <summary>Server or database is empty.</summary>
        NotConfigured,

        /// <summary>The table name is not valid.</summary>
        TableInvalid,

        /// <summary>The server or database name cannot be used as it is.</summary>
        ConnectionInvalid,

        /// <summary>The server cannot be reached.</summary>
        Unreachable,

        /// <summary>The server refused the Windows account.</summary>
        AccessDenied,

        /// <summary>The server answered, but the table or one of its columns was not found.</summary>
        SchemaMismatch,

        /// <summary>The server did not answer in time.</summary>
        Timeout,

        /// <summary>The database driver is not there.</summary>
        DriverMissing,

        /// <summary>The test was cancelled.</summary>
        Cancelled,

        /// <summary>Something else went wrong.</summary>
        Failed
    }

    /// <summary>
    /// Reads, saves and tests the settings the settings UI edits. A seam of the extension's own, like
    /// <see cref="IImportOffer"/>: the support library is behind it. Nothing here throws because of the disk, the
    /// settings file or the server; a missing argument is the caller's mistake and is refused.
    /// </summary>
    internal interface IAssistantSettingsEditor
    {
        /// <summary>Reads the settings. Reads the disk: call it off the UI thread.</summary>
        /// <returns>The values, and what is wrong with the file.</returns>
        SettingsEditorLoad Load();

        /// <summary>
        /// Saves what the operator changed: only the values that differ between the loaded data and the edited data are
        /// written, in one step with reading the settings as they are now. Everything else is kept as the file holds it
        /// now: what the UI does not show, and what another window changed since this one loaded. A value that both
        /// changed takes the edited one. Writes the disk: call it off the UI thread.
        /// </summary>
        /// <param name="baseline">The data as the UI loaded it (or last saved it).</param>
        /// <param name="edited">The data as the operator left it.</param>
        /// <returns>How it ended.</returns>
        SettingsSaveOutcome Save(SettingsData baseline, SettingsData edited);

        /// <summary>
        /// Tries the order database as entered (not as saved): connects with the Windows account and checks that the table
        /// has the columns the assistant uses. Changes nothing.
        /// </summary>
        /// <param name="database">The values to try.</param>
        /// <param name="cancellationToken">Cancels the test.</param>
        /// <returns>What was found.</returns>
        Task<ConnectionTestOutcome> TestConnectionAsync(DatabaseData database, CancellationToken cancellationToken);
    }
}
