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

        /// <summary>Creates the service that reads and writes the form fields of the check reports (PDF files).</summary>
        /// <param name="log">Where an unexpected fault is noted (by type only).</param>
        /// <returns>A new service; it keeps no state.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="log"/> is null.</exception>
        public static IReportService CreateReportService(ICardCheckLog log) => new ReportService(log);

        /// <summary>
        /// Creates the access to the order table of the external order system (SQL Server, the Windows account of the
        /// person running the check). The settings are taken as they are now; after they change, create a new one.
        /// </summary>
        /// <param name="settings">Where the order database and its table are.</param>
        /// <param name="log">Where an unexpected fault is noted (by type only).</param>
        /// <returns>A new store; it opens a connection for each call and keeps none.</returns>
        /// <exception cref="System.ArgumentNullException">An argument is null.</exception>
        public static IOrderStore CreateOrderStore(DatabaseSettings settings, ICardCheckLog log) =>
            new OrderStore(settings, new SqlServerConnectionFactory(settings), log);

        /// <summary>
        /// Creates a stand-in for the order table that keeps a few made-up orders in memory, for the assistant's test
        /// mode: a check can be tried with only a reader and a card, and nothing is read from or written to an order
        /// database. The orders, and every change made to them, last as long as the returned store.
        /// </summary>
        /// <param name="log">Where the creation of the store is noted (a count only).</param>
        /// <returns>A new store that holds the stub orders; create one for each window.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="log"/> is null.</exception>
        public static IOrderStore CreateStubOrderStore(ICardCheckLog log)
        {
            if (log == null)
            {
                throw new System.ArgumentNullException(nameof(log));
            }

            var seed = StubOrders.CreateSeed();
            log.Information("The stub order store was created with {Count} stub orders; no order database is used", seed.Count);

            return new InMemoryOrderStore(seed);
        }

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

        /// <summary>
        /// Creates the importer for the settings of the old Card Check Assistant. It looks for the old files in the
        /// user's application data folder and merges what it imports into the given store.
        /// </summary>
        /// <param name="store">The settings the values are merged into.</param>
        /// <param name="log">Where counts are noted (never a value, a path or a credential).</param>
        /// <returns>A new importer.</returns>
        /// <exception cref="System.InvalidOperationException">The user has no application data folder.</exception>
        /// <exception cref="System.ArgumentNullException">An argument is null.</exception>
        public static ILegacySettingsImporter CreateLegacyImporter(ISettingsStore store, ICardCheckLog log) =>
            new LegacySettingsImporter(store, LocalAppData(), log);

        /// <summary>Creates an importer that looks for the old files below the given application data folder.</summary>
        /// <param name="store">The settings the values are merged into.</param>
        /// <param name="localAppData">The folder that contains the old assistant's folders.</param>
        /// <param name="log">Where counts are noted.</param>
        /// <returns>A new importer.</returns>
        /// <exception cref="System.ArgumentNullException">An argument is null.</exception>
        public static ILegacySettingsImporter CreateLegacyImporter(ISettingsStore store, string localAppData, ICardCheckLog log) =>
            new LegacySettingsImporter(store, localAppData, log);

        /// <summary>The folder of the settings file: the user's application data, then the assistant's own folder.</summary>
        internal static string DefaultSettingsFolder() =>
            System.IO.Path.Combine(LocalAppData(), "RFiDGear", "Extensions", "CardCheckAssistant");

        private static string LocalAppData()
        {
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);

            return string.IsNullOrWhiteSpace(appData)
                ? throw new System.InvalidOperationException("The user has no application data folder.")
                : appData;
        }
    }
}
