using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RFiDGear.Bridge
{
    /// <summary>
    /// Why the constants of the protocol are public constants. The static analysis rule against public constants (S2339) warns
    /// that a constant is compiled into every assembly that uses it, so that a changed value does not reach a consumer that
    /// is not rebuilt. For these constants that is the point: they are the names and values of the wire protocol, a value never
    /// changes within a protocol major version, and a client that was built against one must keep speaking it. A number of them
    /// are also arguments of attributes, which only accept constants.
    /// </summary>
    internal static class WireConstants
    {
        /// <summary>The justification text of the suppression.</summary>
        internal const string Justification = "The names and values of the wire protocol are compiled into clients on purpose: they never change within a major version, and attribute arguments need constants.";
    }
    /// <summary>
    /// The capabilities of version 1 of the protocol. A client decides what it may call from the list that the host
    /// answers in <c>hello</c>, never from the application version. Later versions add names; none is removed or re-meant
    /// within a major version.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class BridgeCapabilities
    {
        /// <summary>The handshake itself: protocol negotiation and the capability list. Always present.</summary>
        public const string Session = "session";

        /// <summary>The dynamic status: what is possible right now.</summary>
        public const string Status = "status";

        /// <summary>Loading a project file.</summary>
        public const string ProjectLoad = "project.load";

        /// <summary>Starting a run of the task list and getting one final result for it. Starting is idempotent: every request carries an id that makes a repeated request recognizable.</summary>
        public const string ExecutionRun = "execution.run";


        /// <summary>Cancelling a run that was started over the bridge (honoured between tasks).</summary>
        public const string ExecutionCancel = "execution.cancel";

        /// <summary>Asking for the result of a run later, for example after a reconnect.</summary>
        public const string ExecutionResultLookup = "execution.resultLookup";

        /// <summary>
        /// Reading the results of all runs in the order in which they ended, those of automatic mode as well, from a number onward,
        /// and being told when some were dropped from the history in between.
        /// </summary>
        public const string ExecutionResultLog = "execution.resultLog";

        /// <summary>Switching the continuous processing of cards on and off, and reading what it did.</summary>
        public const string AutoMode = "autoMode";

        /// <summary>The events about automatic mode: it was switched on or off, and a card was processed.</summary>
        public const string EventsAutoMode = "events.autoMode";

        /// <summary>
        /// Supplying card keys from the client's own vault: the client registers, and RFiDGear asks it for the key of a task that
        /// names one by reference (see <see cref="IBridgeClientKeys"/>), so that no key is ever stored in a project, a file or a command line.
        /// </summary>
        public const string KeysProvider = "keys.provider";

        /// <summary>
        /// A guarantee, not a method: RFiDGear writes down, before it asks the host to run, that it is about to, and when the run
        /// ended. After RFiDGear has been restarted, a request id it had admitted is still known, as long as it is among the
        /// last <see cref="BridgeProtocol.RequestIdHistory"/> request ids: <c>startRun</c> with it never starts a second run
        /// (<see cref="StartRunResponse.Duplicate"/> is set), and a lookup answers <see cref="RunStates.InterruptedByRestart"/>
        /// for a run that was in flight, or <see cref="RunStates.Completed"/> without a result for one that had ended, whose
        /// result the restart took. Without this capability a restart forgets all of that, and a client that did not get an answer
        /// before the restart cannot know whether the card was touched.
        /// <para>
        /// What it does not promise: <see cref="RunStates.Unknown"/> does not prove that nothing was started. An id is forgotten
        /// when more than <see cref="BridgeProtocol.RequestIdHistory"/> newer ids have been seen, and the journal cannot be
        /// written at all on a machine whose disk refuses it (the capability is then not offered). A client that must not repeat
        /// a card operation asks about the id soon after the doubt arises, and does not send the same job under a new id.
        /// </para>
        /// </summary>
        public const string ExecutionJournal = "execution.journal";

        /// <summary>Events about a run started over the bridge: started and completed.</summary>
        public const string EventsExecution = "events.execution";

        /// <summary>The event that says whether any run, from anywhere, is going on.</summary>
        public const string EventsRunning = "events.running";

        /// <summary>The event that says the reader or the card on it changed.</summary>
        public const string EventsReader = "events.reader";

        /// <summary>The event that says the loaded project changed.</summary>
        public const string EventsProject = "events.project";

        /// <summary>The event that says RFiDGear is shutting down.</summary>
        public const string EventsLifecycle = "events.lifecycle";

        /// <summary>Scanning the reader on request.</summary>
        public const string ReaderScan = "reader.scan";

        /// <summary>Gets every capability of version 1, in the order they are listed here.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            Session,
            Status,
            ProjectLoad,
            ExecutionRun,
            ExecutionCancel,
            ExecutionResultLookup,
            ExecutionResultLog,
            ExecutionJournal,
            AutoMode,
            EventsExecution,
            EventsRunning,
            EventsReader,
            EventsProject,
            EventsAutoMode,
            KeysProvider,
            EventsLifecycle,
            ReaderScan,
        };

        /// <summary>
        /// Gets the capabilities that are guarantees about the behaviour of the methods and not methods of their own. Every other
        /// capability is carried by at least one method or event, so that nothing is advertised that does nothing.
        /// </summary>
        public static IReadOnlyList<string> Guarantees { get; } = new[]
        {
            ExecutionJournal,
        };
    }

    /// <summary>The names of the methods a client calls, as they are written on the wire (JSON-RPC <c>method</c>).</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class BridgeMethods
    {
        /// <summary>The handshake; the first call on every connection.</summary>
        public const string Hello = "hello";

        /// <summary>The dynamic status.</summary>
        public const string GetStatus = "getStatus";

        /// <summary>Loads a project.</summary>
        public const string LoadProject = "loadProject";

        /// <summary>Starts a run.</summary>
        public const string StartRun = "startRun";

        /// <summary>Cancels a run.</summary>
        public const string CancelRun = "cancelRun";

        /// <summary>Looks up the result of a run.</summary>
        public const string GetRunResult = "getRunResult";

        /// <summary>Scans the reader.</summary>
        public const string ScanReader = "scanReader";

        /// <summary>Switches automatic mode on.</summary>
        public const string StartAutoMode = "startAutoMode";

        /// <summary>Switches automatic mode off.</summary>
        public const string StopAutoMode = "stopAutoMode";

        /// <summary>Lists the results of the runs from a number onward.</summary>
        public const string ListRunResults = "listRunResults";

        /// <summary>Registers the client as the supplier of card keys for the life of the connection.</summary>
        public const string RegisterKeyProvider = "registerKeyProvider";
    }

    /// <summary>The names of the calls the host makes on the client, as they are written on the wire (JSON-RPC <c>method</c>).</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class BridgeClientMethods
    {
        /// <summary>Asks the client for a card key.</summary>
        public const string ResolveKey = "resolveKey";
    }

    /// <summary>The names of the notifications the host sends, as they are written on the wire (JSON-RPC <c>method</c>).</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class BridgeEvents
    {
        /// <summary>A run started over the bridge was admitted.</summary>
        public const string RunStarted = "runStarted";

        /// <summary>A run started over the bridge ended. Sent exactly once per run.</summary>
        public const string RunCompleted = "runCompleted";

        /// <summary>A task run, from anywhere, started or ended.</summary>
        public const string RunningChanged = "runningChanged";

        /// <summary>The reader or the card on it changed.</summary>
        public const string ReaderChanged = "readerChanged";

        /// <summary>The loaded project changed.</summary>
        public const string ProjectChanged = "projectChanged";

        /// <summary>RFiDGear is closing. Best effort: its absence proves nothing.</summary>
        public const string ShuttingDown = "shuttingDown";

        /// <summary>Automatic mode was switched on or off.</summary>
        public const string AutoModeChanged = "autoModeChanged";

        /// <summary>Automatic mode processed a card. Sent exactly once per run of the mode.</summary>
        public const string AutoRunCompleted = "autoRunCompleted";

        /// <summary>Gets every event name of version 1.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            RunStarted,
            RunCompleted,
            RunningChanged,
            ReaderChanged,
            ProjectChanged,
            ShuttingDown,
            AutoModeChanged,
            AutoRunCompleted,
        };
    }

    /// <summary>The values of <see cref="ResolveKeyRequest.Algorithm"/>: what kind of key is asked for.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class KeyAlgorithms
    {
        /// <summary>Single DES, 8 bytes.</summary>
        public const string Des = "des";

        /// <summary>Two-key triple DES, 16 bytes.</summary>
        public const string TripleDes2K = "tripleDes2K";

        /// <summary>Three-key triple DES, 24 bytes.</summary>
        public const string TripleDes3K = "tripleDes3K";

        /// <summary>AES-128, 16 bytes.</summary>
        public const string Aes128 = "aes128";

        /// <summary>A MIFARE Classic key, 6 bytes.</summary>
        public const string Classic6Byte = "classic6Byte";
    }

    /// <summary>The values of <see cref="ResolveKeyRequest.Usage"/>: what the key is for.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class KeyUsages
    {
        /// <summary>To authenticate to the card.</summary>
        public const string Authenticate = "authenticate";

        /// <summary>The key that is on the card now, used to authorise changing a key.</summary>
        public const string ChangeKeyCurrent = "changeKeyCurrent";

        /// <summary>The key that is to be written to the card.</summary>
        public const string ChangeKeyNew = "changeKeyNew";
    }

    /// <summary>The values of <see cref="ResolveKeyResponse.Result"/>: how the client answered a request for a key.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class KeyResolveResults
    {
        /// <summary>The key is in <see cref="ResolveKeyResponse.Key"/>.</summary>
        public const string Resolved = "resolved";

        /// <summary>The client has no key for the reference.</summary>
        public const string NotFound = "notFound";

        /// <summary>The client has one but will not hand it out for this request (a locked vault, a refusing person, a reference it does not serve).</summary>
        public const string NotAuthorized = "notAuthorized";

        /// <summary>The client has one, but it is not a usable key.</summary>
        public const string InvalidKey = "invalidKey";

        /// <summary>The client does not serve this kind of key.</summary>
        public const string Unsupported = "unsupported";
    }

    /// <summary>The values of <see cref="AutoModeStatusDto.LastStopReason"/>: why automatic mode last stopped.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class AutoModeStopReasons
    {
        /// <summary>It has not stopped since RFiDGear started, or it is on.</summary>
        public const string None = "none";

        /// <summary>Somebody switched it off: the user, an extension or a client.</summary>
        public const string StopRequested = "stopRequested";

        /// <summary>The reader was lost.</summary>
        public const string ReaderLost = "readerLost";

        /// <summary>Another reader was selected.</summary>
        public const string ReaderChanged = "readerChanged";

        /// <summary>A run ended in a fault or was cancelled, which leaves the card in a state nobody knows.</summary>
        public const string RunAborted = "runAborted";

        /// <summary>A run did not end in time.</summary>
        public const string RunTimedOut = "runTimedOut";

        /// <summary>RFiDGear reported a reason this protocol does not know.</summary>
        public const string Unknown = "unknown";
    }

    /// <summary>The values of <see cref="RunResultDto.Status"/>: how a run ended, as one word.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class RunStatuses
    {
        /// <summary>Every task ran as intended.</summary>
        public const string Success = "success";

        /// <summary>The run finished and some tasks failed on purpose (a check that answers no is an answer, not a failure).</summary>
        public const string CompletedWithIntentionalFailures = "completedWithIntentionalFailures";

        /// <summary>The run finished or was cut short by a failure.</summary>
        public const string Failure = "failure";

        /// <summary>The run was cancelled between two tasks.</summary>
        public const string Cancelled = "cancelled";

        /// <summary>The run did not finish in time.</summary>
        public const string TimedOut = "timedOut";
    }

    /// <summary>The values of <see cref="RunResultDto.FailureReason"/>: why a run did not end the normal way.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class FailureReasons
    {
        /// <summary>The run did not fail.</summary>
        public const string None = "none";

        /// <summary>The run was cancelled.</summary>
        public const string Cancelled = "cancelled";

        /// <summary>The run timed out.</summary>
        public const string TimedOut = "timedOut";

        /// <summary>The run ended in a fault inside RFiDGear.</summary>
        public const string Faulted = "faulted";

        /// <summary>The bridge itself could not finish the run (for example the host answered something unknown).</summary>
        public const string BridgeFault = "bridgeFault";

        /// <summary>The host reported a value this protocol version does not know.</summary>
        public const string Unknown = "unknown";
    }

    /// <summary>The values of the state of a run in <see cref="StartRunResponse.State"/> and the lookups.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class RunStates
    {
        /// <summary>The run was admitted and is in flight.</summary>
        public const string Running = "running";

        /// <summary>
        /// The run ended. Its result is in the answer while the history holds it (the last <see cref="BridgeProtocol.ResultHistory"/>
        /// results of this process); a run whose result is no longer held, or was taken by a restart, is still <c>completed</c>
        /// and has no result.
        /// </summary>
        public const string Completed = "completed";

        /// <summary>
        /// The host does not know the id. It was never started, or it is older than the history of ids that is kept (the last
        /// <see cref="BridgeProtocol.RequestIdHistory"/>), or an RFiDGear without the journal was restarted since. It is not a
        /// proof that nothing was started.
        /// </summary>
        public const string Unknown = "unknown";

        /// <summary>RFiDGear restarted while the run was in flight; what the card went through is not known.</summary>
        public const string InterruptedByRestart = "interruptedByRestart";
    }

    /// <summary>The values of <see cref="ReaderDto.State"/>.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class ReaderStates
    {
        /// <summary>No reader is connected.</summary>
        public const string NoReader = "noReader";

        /// <summary>A reader answered; <see cref="ReaderDto.Credentials"/> lists the cards on it.</summary>
        public const string Ready = "ready";

        /// <summary>A reader is there, but the card could not be read.</summary>
        public const string ReadFailed = "readFailed";

        /// <summary>The reader is in use by a run or another scan.</summary>
        public const string Busy = "busy";

        /// <summary>The host reported a state this protocol version does not know.</summary>
        public const string Unknown = "unknown";
    }

    /// <summary>The values of <see cref="ActiveRunDto.Source"/>: who started the run that is going on.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class RunSources
    {
        /// <summary>A client over this bridge.</summary>
        public const string Bridge = "bridge";

        /// <summary>The user, in the main window, or the start-up arguments.</summary>
        public const string Gui = "gui";

        /// <summary>Automatic mode.</summary>
        public const string AutoMode = "autoMode";

        /// <summary>Somebody else; RFiDGear cannot tell.</summary>
        public const string Other = "other";
    }

    /// <summary>The values of <see cref="ShuttingDownEvent.Reason"/>.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class ShutdownReasons
    {
        /// <summary>The user closed the window.</summary>
        public const string UserClose = "userClose";

        /// <summary>The process is ending for another reason.</summary>
        public const string ProcessExit = "processExit";

        /// <summary>The updater is replacing RFiDGear.</summary>
        public const string Update = "update";
    }

    /// <summary>The values of <c>details.activity</c> of <see cref="BridgeErrorCodes.Busy"/>: what RFiDGear is busy with.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class BusyActivities
    {
        /// <summary>A task run is going on.</summary>
        public const string Run = "run";

        /// <summary>A project is being loaded.</summary>
        public const string Load = "load";

        /// <summary>The project is being edited or saved.</summary>
        public const string Edit = "edit";

        /// <summary>The reader is being scanned.</summary>
        public const string Scan = "scan";

        /// <summary>Something else holds the project or the reader; RFiDGear cannot say what.</summary>
        public const string Other = "other";
    }
}
