namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// The way in for the extension. Everything else in the library is an interface or a plain value type, and
    /// its implementations are internal; this class hands out the ones an extension window needs. Later changes
    /// add the services here; none of them keeps process-wide state, so each window creates its own.
    /// </summary>
    public static class CardCheckServices
    {
        /// <summary>Creates the log that writes to the host's Serilog pipeline.</summary>
        /// <returns>A log that follows the host's current logger.</returns>
        public static ICardCheckLog CreateLog() => new SerilogCardCheckLog();

        /// <summary>Creates the service that knows where the files of an order's check are and removes working files.</summary>
        /// <param name="log">Where a file that could not be removed is noted.</param>
        /// <returns>A new service; it keeps no state.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="log"/> is null.</exception>
        public static IReportFiles CreateReportFiles(ICardCheckLog log) => new ReportFiles(log);

        /// <summary>
        /// Creates the store for the assistant's settings, in its own folder below the user's application data
        /// (<c>%LocalAppData%\RFiDGear\Extensions\CardCheckAssistant</c>), apart from the host's own files.
        /// </summary>
        /// <param name="log">Where status is noted (never a value of a setting).</param>
        /// <returns>A new store.</returns>
        /// <exception cref="System.InvalidOperationException">The user has no application data folder.</exception>
        /// <exception cref="System.ArgumentNullException"><paramref name="log"/> is null.</exception>
        public static ISettingsStore CreateSettingsStore(ICardCheckLog log) => new SettingsStore(DefaultSettingsFolder(), log);

        /// <summary>Creates a store whose settings file is in the given folder.</summary>
        /// <param name="folder">The folder; created on the first save.</param>
        /// <param name="log">Where status is noted (never a value of a setting).</param>
        /// <returns>A new store.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="folder"/> is empty.</exception>
        /// <exception cref="System.ArgumentNullException"><paramref name="log"/> is null.</exception>
        public static ISettingsStore CreateSettingsStore(string folder, ICardCheckLog log) => new SettingsStore(folder, log);

        /// <summary>Creates the state of one check in one window. Create one per window; they share nothing.</summary>
        /// <param name="log">Where a fault in a change handler is noted.</param>
        /// <returns>A new, empty session.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="log"/> is null.</exception>
        public static IWorkflowSession CreateWorkflowSession(ICardCheckLog log) => new WorkflowSession(log);

        /// <summary>The folder of the settings file: the user's application data, then the assistant's own folder.</summary>
        internal static string DefaultSettingsFolder()
        {
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrWhiteSpace(appData))
            {
                throw new System.InvalidOperationException("The user has no application data folder.");
            }

            return System.IO.Path.Combine(appData, "RFiDGear", "Extensions", "CardCheckAssistant");
        }
    }
}
