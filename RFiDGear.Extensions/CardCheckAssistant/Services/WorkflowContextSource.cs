using System;
using System.IO;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// The store and the settings of the guided check, read from the saved settings: in test mode the made-up orders
    /// and a <c>Test</c> folder for the reports, otherwise the order system and the report folder of the settings. The
    /// store and the settings that are handed out together belong together, so an order is listed, checked and
    /// finished with the same pair.
    /// <para>
    /// The made-up orders are kept in memory, one set for each window (the library makes a new one for each window): the
    /// same set is handed out every time, also when the test mode is switched off and on again, so an order that was
    /// finished in the window stays finished in it. The order system is reached with a new store for each call, as the
    /// library does it, with the settings as they are now.
    /// </para>
    /// <para>
    /// <see cref="Resolve"/> tells the problems that are to be expected (settings that cannot be used, a report folder
    /// that is not set or not there, a test folder that cannot be made) as a <see cref="Failure"/> with a fixed code and
    /// text. An exception that nobody expects, such as a factory that fails, is not caught here: the workflow's own
    /// catch-all logs it and ends in its faulted state. It reads the settings file, so the workflow calls it on a worker
    /// thread.
    /// </para>
    /// </summary>
    internal sealed class WorkflowContextSource : IWorkflowContextSource
    {
        private readonly ISettingsStore _settings;
        private readonly Func<DatabaseSettings, IOrderStore> _createOrderStore;
        private readonly Func<IOrderStore> _createStubStore;
        private readonly string _testFallbackFolder;
        private readonly object _stubGate = new object();
        private IOrderStore? _stub;

        /// <summary>Creates the source.</summary>
        /// <param name="settings">Where the settings are read from.</param>
        /// <param name="createOrderStore">Makes the store of the order system for the database settings.</param>
        /// <param name="createStubStore">Makes the store of made-up orders; called again only if it failed before.</param>
        /// <param name="testFallbackFolder">The folder the <c>Test</c> folder is made in when the settings name no usable report folder.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public WorkflowContextSource(
            ISettingsStore settings,
            Func<DatabaseSettings, IOrderStore> createOrderStore,
            Func<IOrderStore> createStubStore,
            string testFallbackFolder)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _createOrderStore = createOrderStore ?? throw new ArgumentNullException(nameof(createOrderStore));
            _createStubStore = createStubStore ?? throw new ArgumentNullException(nameof(createStubStore));
            _testFallbackFolder = testFallbackFolder ?? throw new ArgumentNullException(nameof(testFallbackFolder));
        }

        /// <inheritdoc/>
        public Result<WorkflowContext> Resolve()
        {
            SettingsLoad load;

            try
            {
                load = _settings.Load();
            }
            catch (Exception ex) when (IsFileProblem(ex))
            {
                return SettingsUnreadable();
            }

            // A file that was damaged was set aside and the defaults are used (Recovered); one that cannot be read or
            // that a newer version wrote is not touched, and nothing is guessed from it.
            if (load.Status == SettingsLoadStatus.Unreadable)
            {
                return SettingsUnreadable();
            }

            if (load.Status == SettingsLoadStatus.NewerSchema)
            {
                return Fail(FailureKind.InvalidConfiguration, ContextFailureCodes.SettingsNewerSchema, "The settings were written by a newer version.");
            }

            return load.Settings.UseStubJobs ? TestContext(load.Settings) : RealContext(load.Settings);
        }

        private static bool IsFileProblem(Exception ex) =>
            ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException;

        private static Result<WorkflowContext> SettingsUnreadable() =>
            Fail(FailureKind.Io, ContextFailureCodes.SettingsUnreadable, "The settings cannot be read just now.");

        private static Result<WorkflowContext> Fail(FailureKind kind, string code, string message) =>
            Result<WorkflowContext>.Fail(new Failure(kind, code, message));

        private Result<WorkflowContext> TestContext(CardCheckSettings s)
        {
            var root = TestReportFolder.Root(s.Report, _testFallbackFolder);

            try
            {
                Directory.CreateDirectory(root);
            }
            catch (UnauthorizedAccessException)
            {
                return Fail(FailureKind.AccessDenied, ContextFailureCodes.ReportFolderDenied, "The folder for the test reports cannot be made: access denied.");
            }

            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is NotSupportedException)
            {
                // A file in the way, a path that is too long or has a character that is not allowed.
                return Fail(FailureKind.Io, ContextFailureCodes.ReportFolderIo, "The folder for the test reports cannot be made.");
            }

            return Result<WorkflowContext>.Ok(new WorkflowContext(Stub(), SettingsOf(s, new ReportLocation(root, s.Report.CreateSubdirectory), true)));
        }

        // One set of made-up orders for the window. A factory that fails leaves nothing behind, so the next call tries again.
        private IOrderStore Stub()
        {
            lock (_stubGate)
            {
                return _stub ??= _createStubStore();
            }
        }

        private Result<WorkflowContext> RealContext(CardCheckSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.Report.OutputPath))
            {
                return Fail(FailureKind.NotConfigured, ContextFailureCodes.ReportRootNotSet, "No report folder is set.");
            }

            // The path itself (a full path, no forbidden character) is checked where the paths of the reports are made.
            // That the folder is there is checked here, so a missing folder is told before anyone waits for a card.
            if (!Directory.Exists(s.Report.OutputPath))
            {
                return Fail(FailureKind.NotFound, ContextFailureCodes.ReportRootMissing, "The report folder does not exist.");
            }

            var location = new ReportLocation(s.Report.OutputPath, s.Report.CreateSubdirectory);

            return Result<WorkflowContext>.Ok(new WorkflowContext(_createOrderStore(s.Database), SettingsOf(s, location, false)));
        }

        private static WorkflowSettings SettingsOf(CardCheckSettings s, ReportLocation location, bool isTestMode) =>
            new WorkflowSettings(location, s.Report.RemoveTemporaryReports, s.Report.DefaultLanguage, s.DefaultProjectPath, isTestMode);
    }
}
