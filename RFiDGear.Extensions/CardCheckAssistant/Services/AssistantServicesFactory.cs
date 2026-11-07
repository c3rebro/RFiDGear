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

        /// <summary>Creates the services for one window.</summary>
        /// <param name="dispatcher">The dispatcher of the window, on which the import questions are shown.</param>
        /// <returns>The services; remembering and offering nothing if the user has no application data folder.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static AssistantServices Create(Dispatcher dispatcher)
        {
            try
            {
                var log = CardCheckServices.CreateLog();
                var root = Environment.GetEnvironmentVariable(AppDataOverrideVariable);
                var overridden = !string.IsNullOrWhiteSpace(root);

                var store = overridden
                    ? CardCheckServices.CreateSettingsStore(Path.Combine(root!, "settings"), log)
                    : CardCheckServices.CreateSettingsStore(log);
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
