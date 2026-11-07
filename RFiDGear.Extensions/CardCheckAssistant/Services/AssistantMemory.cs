using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Extensions.CardCheckAssistant.Localization;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>What the page starts from, as remembered by an earlier window or imported from the old assistant.</summary>
    /// <param name="DefaultProjectPath">The project to prefill; blank if there is none.</param>
    /// <param name="LastProjectFolder">The folder the picker opens in when no project is entered; blank if unknown.</param>
    internal sealed record AssistantDefaults(string DefaultProjectPath, string LastProjectFolder)
    {
        /// <summary>Nothing is remembered.</summary>
        internal static AssistantDefaults None { get; } = new AssistantDefaults(string.Empty, string.Empty);
    }

    /// <summary>
    /// What the page remembers between windows. A seam of the extension's own (no library type in it), so the view model
    /// is tested without files. Both calls read or write the disk: call them off the UI thread. Neither throws.
    /// </summary>
    internal interface IAssistantSettings
    {
        /// <summary>Reads what the page prefills from.</summary>
        /// <returns>The remembered values; <see cref="AssistantDefaults.None"/> if there are none or they cannot be read.</returns>
        AssistantDefaults Load();

        /// <summary>Remembers the folder of the project that was last used. Settings that cannot be changed are left alone.</summary>
        /// <param name="folder">The folder.</param>
        void RememberProjectFolder(string folder);
    }

    /// <summary>
    /// The one-time offer to take over the old assistant's settings. A seam of the extension's own, like
    /// <see cref="IAssistantSettings"/>. It shows dialogs, so it must be started from the UI thread and it never throws.
    /// </summary>
    internal interface IImportOffer
    {
        /// <summary>Asks the operator, if there is something to ask and it was not answered before.</summary>
        /// <param name="strings">The page's texts; the questions are in the page's language.</param>
        /// <param name="cancellationToken">Cancelled when the window closes.</param>
        /// <returns><c>true</c> if settings were imported, so the page should read its defaults again.</returns>
        Task<bool> OfferAsync(LocalizedStrings strings, CancellationToken cancellationToken);
    }

    /// <summary>The services of the page that need the support library, or none at all.</summary>
    /// <param name="Settings">What the page remembers.</param>
    /// <param name="ImportOffer">The one-time import offer.</param>
    /// <param name="Editor">What the settings UI reads, saves and tests.</param>
    internal sealed record AssistantServices(IAssistantSettings Settings, IImportOffer ImportOffer, IAssistantSettingsEditor Editor)
    {
        /// <summary>Nothing remembered, nothing offered, nothing to edit: what the page does without a settings folder.</summary>
        internal static AssistantServices None { get; } = new AssistantServices(new NoSettings(), new NoImportOffer(), new NoEditor());

        private sealed class NoEditor : IAssistantSettingsEditor
        {
            public SettingsEditorLoad Load() => new SettingsEditorLoad(new SettingsData(), SettingsProblem.Unavailable);

            // Like the real editor, it refuses a missing argument and does not start what is already cancelled.
            public SettingsSaveOutcome Save(SettingsData baseline, SettingsData edited) =>
                baseline == null || edited == null
                    ? throw new ArgumentNullException(baseline == null ? nameof(baseline) : nameof(edited))
                    : SettingsSaveOutcome.Unavailable;

            public Task<ConnectionTestOutcome> TestConnectionAsync(DatabaseData database, CancellationToken cancellationToken) =>
                database == null
                    ? throw new ArgumentNullException(nameof(database))
                    : Task.FromResult(cancellationToken.IsCancellationRequested ? ConnectionTestOutcome.Cancelled : ConnectionTestOutcome.Failed);
        }

        private sealed class NoSettings : IAssistantSettings
        {
            public AssistantDefaults Load() => AssistantDefaults.None;

            public void RememberProjectFolder(string folder)
            {
                // Nothing is kept.
            }
        }

        private sealed class NoImportOffer : IImportOffer
        {
            public Task<bool> OfferAsync(LocalizedStrings strings, CancellationToken cancellationToken) => Task.FromResult(false);
        }
    }
}
