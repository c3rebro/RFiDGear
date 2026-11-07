using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>What the operator answers to the offer to import.</summary>
    internal enum ImportAnswer
    {
        /// <summary>Import the settings.</summary>
        Import,

        /// <summary>Ask again next time. Also what closing the dialog means.</summary>
        NotNow,

        /// <summary>Never ask again.</summary>
        Never
    }

    /// <summary>The two questions of the import as the operator sees them; the texts are already in the page's language.</summary>
    internal interface IImportDialogs
    {
        /// <summary>Asks whether to import. Called on the window's thread.</summary>
        /// <param name="title">The title.</param>
        /// <param name="text">What was found.</param>
        /// <param name="importButton">The text of the button that imports.</param>
        /// <param name="notNowButton">The text of the button that asks again next time.</param>
        /// <param name="neverButton">The text of the button that never asks again.</param>
        /// <returns>The answer.</returns>
        ImportAnswer Ask(string title, string text, string importButton, string notNowButton, string neverButton);

        /// <summary>Shows what would be imported and asks for the last go-ahead. Called on the window's thread.</summary>
        /// <param name="title">The title.</param>
        /// <param name="text">What would be imported.</param>
        /// <returns><c>true</c> to import.</returns>
        bool Confirm(string title, string text);
    }

    /// <summary>
    /// The one-time offer to take over the old assistant's settings, on top of the library's importer. The importer reads
    /// the disk before it asks anything, so it runs on a worker thread; each question goes back to the window's thread
    /// to be shown. Whatever goes wrong, the answer to the page is "nothing was imported".
    /// </summary>
    internal sealed class ImportOfferService : IImportOffer
    {
        private readonly ILegacySettingsImporter _importer;
        private readonly IImportDialogs _dialogs;
        private readonly Dispatcher _dispatcher;

        /// <summary>Creates the service.</summary>
        /// <param name="importer">The library's importer.</param>
        /// <param name="dialogs">Shows the questions.</param>
        /// <param name="dispatcher">The dispatcher of the window; the questions are shown on it.</param>
        public ImportOfferService(ILegacySettingsImporter importer, IImportDialogs dialogs, Dispatcher dispatcher)
        {
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <inheritdoc/>
        public async Task<bool> OfferAsync(LocalizedStrings strings, CancellationToken cancellationToken)
        {
            try
            {
                var prompt = new Prompt(this, strings);
                var offer = await Task.Run(() => _importer.OfferAsync(prompt, cancellationToken), cancellationToken);

                return offer.Outcome == ImportOutcome.Imported;
            }
            catch (Exception)
            {
                // The offer is a convenience; the page works without it, and the library has logged what it could.
                return false;
            }
        }

        private sealed class Prompt : IImportPrompt
        {
            private readonly ImportOfferService _owner;
            private readonly LocalizedStrings _strings;

            public Prompt(ImportOfferService owner, LocalizedStrings strings)
            {
                _owner = owner;
                _strings = strings;
            }

            public Task<ImportDecision> ChooseAsync(IReadOnlyList<LegacySettingsCandidate> candidates, CancellationToken cancellationToken) =>
                _owner._dispatcher.InvokeAsync(() => Ask(candidates), DispatcherPriority.Normal, cancellationToken).Task;

            public Task<bool> ConfirmAsync(LegacyImportPreview preview, CancellationToken cancellationToken) =>
                _owner._dispatcher.InvokeAsync(() => Confirm(preview), DispatcherPriority.Normal, cancellationToken).Task;

            // The newest file is offered; the library lists them newest first.
            private ImportDecision Ask(IReadOnlyList<LegacySettingsCandidate> candidates)
            {
                var newest = candidates[0];
                var text = _strings.Format(Keys.ImportFound, newest.Path, newest.LastWriteUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));

                if (candidates.Count > 1)
                {
                    text += Environment.NewLine + _strings.Format(Keys.ImportMore, candidates.Count - 1);
                }

                var answer = _owner._dialogs.Ask(_strings[Keys.ImportTitle], text, _strings[Keys.ImportButton], _strings[Keys.ImportNotNow], _strings[Keys.ImportNever]);

                switch (answer)
                {
                    case ImportAnswer.Import:
                        return new ImportDecision(ImportChoice.Import, newest.Path);
                    case ImportAnswer.Never:
                        return new ImportDecision(ImportChoice.Never);
                    default:
                        return new ImportDecision(ImportChoice.NotNow);
                }
            }

            private bool Confirm(LegacyImportPreview preview)
            {
                var imported = preview.Items.Count(item => item.Disposition == LegacyElementDisposition.Imported);
                var notTaken = preview.Items.Count(item => item.Disposition != LegacyElementDisposition.Imported && item.Disposition != LegacyElementDisposition.NeverRead);
                var text = _strings.Format(Keys.ImportConfirm, imported, notTaken);

                // The only note the operator needs to read before agreeing; the others are about details of a file.
                if (preview.Notes.Any(note => note.Code == "import.sqlite.unsupported"))
                {
                    text = _strings[Keys.ImportSqliteNote] + Environment.NewLine + text;
                }

                return _owner._dialogs.Confirm(_strings[Keys.ImportTitle], text);
            }
        }
    }
}
