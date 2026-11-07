using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Views;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// Builds the services of the page that need the support library. Only called after <see cref="ComponentCheck"/> has
    /// shown that the library can be used, and in a method of its own that is never inlined.
    /// </summary>
    internal static class AssistantServicesFactory
    {
        /// <summary>
        /// Names a folder that replaces the user's application data for the page's settings and for the search for the
        /// old assistant's files. The test run sets it, so no test reads or writes the settings of whoever runs it; it can
        /// also be used to try the page against a copy of someone's files.
        /// </summary>
        internal const string AppDataOverrideVariable = "RFIDGEAR_CARDCHECK_APPDATA";

        /// <summary>
        /// Whether the one-time offer to take over the old WinUI assistant's settings is made. It is off since Slice 3.5:
        /// the settings are built from scratch and no backward compatibility is wanted for now. The importer, its dialogs and
        /// their tests are kept; Slice 6 decides whether to switch it on again for the cutover or to remove the code. While
        /// it is off nothing is read from the old assistant's folder and no marker is written to the settings.
        /// </summary>
        private const bool OfferLegacyImport = false;

        /// <summary>Creates the services for one window.</summary>
        /// <param name="dispatcher">The dispatcher of the window, on which the import questions are shown.</param>
        /// <returns>The services; remembering and offering nothing if the user has no application data folder.</returns>
        internal static AssistantServices Create(Dispatcher dispatcher) => Create(dispatcher, OfferLegacyImport);

        /// <summary>Creates the services for one window, with the import offer switched on or off.</summary>
        /// <param name="dispatcher">The dispatcher of the window, on which the import questions are shown.</param>
        /// <param name="offerLegacyImport">Whether the page offers to import the old assistant's settings.</param>
        /// <returns>The services; remembering and offering nothing if the user has no application data folder.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static AssistantServices Create(Dispatcher dispatcher, bool offerLegacyImport)
        {
            try
            {
                var log = CardCheckServices.CreateLog();
                var root = Environment.GetEnvironmentVariable(AppDataOverrideVariable);
                var overridden = !string.IsNullOrWhiteSpace(root);

                var store = overridden
                    ? CardCheckServices.CreateSettingsStore(Path.Combine(root!, "settings"), log)
                    : CardCheckServices.CreateSettingsStore(log);

                if (!offerLegacyImport)
                {
                    return new AssistantServices(new SettingsMemory(store), AssistantServices.None.ImportOffer);
                }

                var importer = overridden
                    ? CardCheckServices.CreateLegacyImporter(store, root!, log)
                    : CardCheckServices.CreateLegacyImporter(store, log);

                return new AssistantServices(new SettingsMemory(store), new ImportOfferService(importer, new ImportDialogs(), dispatcher));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                // No application data folder, or a folder name that is no path: the page works without remembering.
                return AssistantServices.None;
            }
        }
    }
}
