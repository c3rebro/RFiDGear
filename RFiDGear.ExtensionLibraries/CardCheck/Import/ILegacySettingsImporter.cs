using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Takes the settings of the old Card Check Assistant over into this version, once, when the operator agrees.
    /// The old files are only ever read: never changed, moved or deleted. Credentials (user name, password, port) are
    /// not taken over and not even held. Values that the new settings already have are kept; only settings that are
    /// still at their defaults are filled in.
    /// </summary>
    public interface ILegacySettingsImporter
    {
        /// <summary>
        /// Looks for settings files of the old assistant in the places it kept them: its own folder in the user's
        /// application data and the private folder of a packaged (Store style) install.
        /// </summary>
        /// <returns>The files found, newest first; empty if there are none. Never throws.</returns>
        IReadOnlyList<LegacySettingsCandidate> Discover();

        /// <summary>
        /// Works out what importing a file would do, without writing anything.
        /// </summary>
        /// <param name="sourcePath">The file to read.</param>
        /// <returns>The preview, or why the file cannot be used (missing, too large, not a settings file, damaged).</returns>
        Result<LegacyImportPreview> Preview(string sourcePath);

        /// <summary>
        /// Imports a previewed file: checks that the file is still the one that was previewed, merges its values into
        /// the current settings and saves them with a marker that the import was done. Refused if the import was
        /// already done or declined.
        /// </summary>
        /// <param name="preview">The preview of the file to import.</param>
        /// <returns>Success, or why nothing was written.</returns>
        Result Apply(LegacyImportPreview preview);

        /// <summary>
        /// The whole question: looks for old settings, asks the operator what to do, shows what would be imported and
        /// asks for confirmation, then imports. Nothing is asked if the question was answered before or there is
        /// nothing to import. Never throws because of a file or the prompt.
        /// </summary>
        /// <param name="prompt">The operator's side of the conversation.</param>
        /// <param name="cancellationToken">Cancels the offer.</param>
        /// <returns>How it ended.</returns>
        Task<ImportOffer> OfferAsync(IImportPrompt prompt, CancellationToken cancellationToken);
    }
}
