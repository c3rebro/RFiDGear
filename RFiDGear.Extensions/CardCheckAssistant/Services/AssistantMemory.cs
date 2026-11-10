using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// The one-time offer to take over the old assistant's settings. A seam of the extension's own (no library type in
    /// it). It shows dialogs, so it must be started from the UI thread and it never throws.
    /// </summary>
    internal interface IImportOffer
    {
        /// <summary>Asks the operator, if there is something to ask and it was not answered before.</summary>
        /// <param name="strings">The page's texts; the questions are in the page's language.</param>
        /// <param name="cancellationToken">Cancelled when the window closes.</param>
        /// <returns><c>true</c> if settings were imported.</returns>
        Task<bool> OfferAsync(LocalizedStrings strings, CancellationToken cancellationToken);
    }

    /// <summary>The services of the page that need the support library, or none at all.</summary>
    /// <param name="ImportOffer">The one-time import offer.</param>
    /// <param name="Editor">What the settings UI reads, saves and tests.</param>
    /// <param name="CreateWorkflow">Builds the guided workflow of a window for the host it is opened in; each window has its own.</param>
    internal sealed record AssistantServices(
        IImportOffer ImportOffer,
        IAssistantSettingsEditor Editor,
        Func<IRFiDGearExtensionHost, IGuidedWorkflow> CreateWorkflow)
    {
        /// <summary>
        /// Nothing offered, nothing to edit, and a workflow that says it has nothing to work from: what the page does
        /// without a settings folder.
        /// </summary>
        internal static AssistantServices None { get; } =
            new AssistantServices(new NoImportOffer(), new NoEditor(), host => GuidedWorkflowFactory.CreateUnavailable(host));

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

        private sealed class NoImportOffer : IImportOffer
        {
            public Task<bool> OfferAsync(LocalizedStrings strings, CancellationToken cancellationToken) => Task.FromResult(false);
        }
    }
}
