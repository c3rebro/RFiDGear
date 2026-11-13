#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;

using Serilog;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// The external control bridge as the application has it: the pipe server, the status the clients read and the events the
    /// host gives them. The application creates it when the setting or the start-up argument asks for it, tells it when start-up
    /// has finished, and shuts it down on the way out. It never touches the main window itself: whatever it needs from the
    /// window arrives as a delegate.
    /// </summary>
    internal sealed class ExternalControlService : IDisposable
    {
        /// <summary>How long a shutdown waits for the last notification to leave, at most.</summary>
        internal static readonly TimeSpan FlushTime = TimeSpan.FromMilliseconds(300);

        // Above the flush: the whole shutdown is given this long, then the caller goes on and the process ends anyway.
        private static readonly TimeSpan ShutdownBudget = TimeSpan.FromMilliseconds(500);

        private static ILogger Logger => Log.ForContext<ExternalControlService>();

        private readonly IRFiDGearExtensionHost _host;
        private readonly BridgeServer _server;
        private readonly RunSupervisor? _supervisor;
        private readonly string _instanceId;
        private int _startupComplete;
        private int _started;
        private int _shutdown;
        private int _disposed;

        /// <summary>Creates the service; <see cref="Start"/> opens the pipe.</summary>
        /// <param name="host">The host service of RFiDGear.</param>
        /// <param name="dispatchToUi">Runs an action on the thread of the main window.</param>
        /// <param name="isReaderInitializing">Says whether the reader has not been looked at yet; called on the thread of the main window.</param>
        /// <param name="appVersion">The version of RFiDGear, as text.</param>
        /// <param name="pipeName">The name of the pipe (<see cref="PipeNames.ForCurrentUser"/>).</param>
        /// <param name="timings">The time limits of a connection; the defaults when <see langword="null"/>.</param>
        /// <param name="uiTimeout">How long <c>getStatus</c> waits for the main window; two seconds when <see langword="null"/>.</param>
        /// <param name="busyActivity">Says what the application is busy with (one of <see cref="BusyActivities"/>), for the hint in a "busy" error; may return <see langword="null"/>.</param>
        /// <param name="admissionTimeout">How long <c>startRun</c> waits for the main window to take a request; five seconds when <see langword="null"/>.</param>
        internal ExternalControlService(
            IRFiDGearExtensionHost host,
            Func<Action, Task> dispatchToUi,
            Func<bool> isReaderInitializing,
            string appVersion,
            string pipeName,
            BridgeServerTimings? timings = null,
            TimeSpan? uiTimeout = null,
            Func<string?>? busyActivity = null,
            TimeSpan? admissionTimeout = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _instanceId = Guid.NewGuid().ToString("N");

            // Runs can only be offered when the execution host can start a run under an id the bridge chose.
            if (host.Execution is IPreassignedRunExecution execution)
            {
                _supervisor = new RunSupervisor(
                    host,
                    execution,
                    new RunRegistry(),
                    dispatchToUi,
                    (name, durable, create) => _server!.Publish(name, durable, create),
                    () => Volatile.Read(ref _shutdown) != 0,
                    () => busyActivity?.Invoke(),
                    admissionTimeout);
            }

            var supervisor = _supervisor;
            var status = new BridgeStatusProvider(
                host,
                _instanceId,
                () => IsStartupComplete,
                isReaderInitializing,
                dispatchToUi,
                uiTimeout ?? TimeSpan.FromSeconds(2),
                () => supervisor?.LastResultSeq ?? 0,
                () => supervisor?.ActiveRun);
            var operations = new BridgeHostOperations(host, busyActivity);

            // What the host offers in this version of the bridge; a capability appears only when everything behind it is there.
            var capabilities = new HashSet<string>(StringComparer.Ordinal)
            {
                BridgeCapabilities.Session,
                BridgeCapabilities.Status,
                BridgeCapabilities.ProjectLoad,
                BridgeCapabilities.ReaderScan,
                BridgeCapabilities.EventsRunning,
                BridgeCapabilities.EventsProject,
                BridgeCapabilities.EventsReader,
                BridgeCapabilities.EventsLifecycle,
            };

            if (supervisor != null)
            {
                capabilities.Add(BridgeCapabilities.ExecutionRun);
                capabilities.Add(BridgeCapabilities.ExecutionCancel);
                capabilities.Add(BridgeCapabilities.ExecutionResultLookup);
                capabilities.Add(BridgeCapabilities.EventsExecution);
            }

            var options = new BridgeServiceOptions
            {
                AppVersion = appVersion ?? string.Empty,
                InstanceId = _instanceId,
                Capabilities = capabilities,
                IsStartupComplete = () => IsStartupComplete,
                GetStatus = status.GetStatusAsync,
                LoadProject = operations.LoadProjectAsync,
                ScanReader = operations.ScanReaderAsync,
                StartRun = supervisor == null ? null : supervisor.StartAsync,
                CancelRun = supervisor == null ? null : request => Task.FromResult(supervisor.Cancel(request)),
                GetRunResult = supervisor == null ? null : supervisor.GetResultAsync,
            };

            _server = new BridgeServer(pipeName, options, timings);
            RunInFlight = () => supervisor?.HasActiveRun ?? false;
        }

        /// <summary>Raised when a client connects or leaves, or the server stops. Any thread.</summary>
        internal event EventHandler? ConnectionChanged;

        /// <summary>Gets the id of this process as clients see it.</summary>
        internal string InstanceId => _instanceId;

        /// <summary>Gets whether a client is connected.</summary>
        internal bool IsConnected => _server.HasSession;

        /// <summary>Gets where the pipe server stands.</summary>
        internal BridgeServerState ServerState => _server.State;

        /// <summary>Gets whether the application has said that it has finished starting.</summary>
        internal bool IsStartupComplete => Volatile.Read(ref _startupComplete) != 0;

        /// <summary>
        /// Gets or sets whether a run that a client started is in flight: the shutdown notice says so, and the application asks
        /// before it closes while one is. It reads the supervisor of the runs; a test may replace it.
        /// </summary>
        internal Func<bool> RunInFlight { get; set; }

        /// <summary>
        /// Opens the pipe and subscribes to the host. If the pipe cannot be created (the name is taken, access is denied) the
        /// service stays off and the application goes on without it.
        /// </summary>
        /// <returns><see langword="true"/> when the pipe is listening.</returns>
        internal bool Start()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                return _server.State == BridgeServerState.Listening || _server.State == BridgeServerState.SessionActive;
            }

            _server.StateChanged += OnServerStateChanged;

            if (!_server.Start())
            {
                return false;
            }

            // The host raises this synchronously from inside the start and the end of a run: the handler only queues.
            _host.Execution.IsRunningChanged += OnIsRunningChanged;
            _host.Project.ProjectChanged += OnProjectChanged;
            _host.Reader.ReaderStateChanged += OnReaderStateChanged;

            // The only hook that every way out of the application shares (they all end in Environment.Exit).
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Logger.Information("External control: listening (instance {InstanceId})", _instanceId);

            return true;
        }

        /// <summary>
        /// Says what the host is busy with, for the hint in a "busy" error. The state is read at one moment and may have moved on
        /// when the client reads it; it is a hint, not a promise.
        /// </summary>
        /// <param name="coordinator">The coordinator of the operations on the project and the reader.</param>
        /// <returns>One of <see cref="BusyActivities"/>, or <see langword="null"/> when nothing the coordinator knows of is going on.</returns>
        internal static string? ActivityOf(ExtensionOperationCoordinator coordinator)
        {
            ArgumentNullException.ThrowIfNull(coordinator);

            if (coordinator.IsExecuting)
            {
                return BusyActivities.Run;
            }

            if (coordinator.IsLoading)
            {
                return BusyActivities.Load;
            }

            if (coordinator.IsMutating)
            {
                return BusyActivities.Edit;
            }

            return coordinator.IsScanning ? BusyActivities.Scan : null;
        }

        /// <summary>Says that the application has finished starting: the methods other than <c>hello</c> and <c>getStatus</c> work from now on.</summary>
        internal void MarkStartupComplete() => Volatile.Write(ref _startupComplete, 1);

        /// <summary>
        /// Tells the client that RFiDGear is closing, waits a moment for the notice to leave, and closes the pipe. Safe to call
        /// again and from any thread; it returns within a fraction of a second whatever the client does, and it never waits for
        /// the main window.
        /// </summary>
        /// <param name="reason">Why, one of <see cref="ShutdownReasons"/>.</param>
        internal void Shutdown(string reason)
        {
            if (Interlocked.Exchange(ref _shutdown, 1) != 0)
            {
                return;
            }

            var work = Task.Run(() =>
            {
                try
                {
                    _server.Publish(
                        BridgeEvents.ShuttingDown,
                        durable: true,
                        (seq, sessionId, at) => new ShuttingDownEvent { Seq = seq, SessionId = sessionId, At = at, Reason = reason, RunInFlight = RunInFlight() });
                    _server.TryFlush(FlushTime);
                }
                catch (Exception e)
                {
                    Logger.Warning("External control: the shutdown notice failed ({ExceptionType})", e.GetType().FullName);
                }
                finally
                {
                    _server.Stop();
                }
            });

            if (!work.Wait(ShutdownBudget))
            {
                Logger.Warning("External control: the shutdown took longer than allowed and was left behind");
            }
        }

        /// <summary>The handler of <see cref="AppDomain.ProcessExit"/>; public to the tests, which cannot raise that event.</summary>
        /// <param name="sender">Not used.</param>
        /// <param name="e">Not used.</param>
        internal void OnProcessExit(object? sender, EventArgs e) => Shutdown(ShutdownReasons.ProcessExit);

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            _host.Execution.IsRunningChanged -= OnIsRunningChanged;
            _host.Project.ProjectChanged -= OnProjectChanged;
            _host.Reader.ReaderStateChanged -= OnReaderStateChanged;
            _supervisor?.Detach();
            _server.StateChanged -= OnServerStateChanged;
            _server.Dispose();
        }

        // Raised on the thread that changed the project (the window's), after the change: the path and the count are read
        // here, because the count belongs to the live task list and must not be read from the thread that sends. Nothing may
        // escape into the host that raised the event.
        private void OnProjectChanged(object? sender, EventArgs e)
        {
            string? path;
            int taskCount;

            try
            {
                path = _host.Project.CurrentPath;
                taskCount = _host.Project.TaskCount;
            }
            catch (Exception ex)
            {
                Logger.Warning("External control: the project could not be read for a notice ({ExceptionType})", ex.GetType().FullName);

                return;
            }

            _server.Publish(
                BridgeEvents.ProjectChanged,
                durable: false,
                (seq, sessionId, at) => new ProjectChangedEvent { Seq = seq, SessionId = sessionId, At = at, Path = path, TaskCount = taskCount });
        }

        // The picture of the reader is taken when the notice is sent, so a client always gets the latest one.
        private void OnReaderStateChanged(object? sender, EventArgs e) =>
            _server.Publish(
                BridgeEvents.ReaderChanged,
                durable: false,
                (seq, sessionId, at) => new ReaderChangedEvent { Seq = seq, SessionId = sessionId, At = at, Reader = BridgeMapper.ReadReader(_host.Reader) });

        private void OnIsRunningChanged(object? sender, EventArgs e)
        {
            // Raised synchronously by whoever changed the state, on that thread: only a run that the bridge is admitting right now
            // is the bridge's. Asked later, "which run is going on" could name a run of the bridge whose task has not completed yet
            // while someone else already runs.
            var own = _supervisor?.OwnerOfThisRaise();

            _server.Publish(
                BridgeEvents.RunningChanged,
                durable: false,
                (seq, sessionId, at) =>
                {
                    // The picture of the moment of sending: is a run going on, and is it still the one that was started by the bridge.
                    var running = _host.Execution.IsRunning;
                    var owner = own != null && running && _supervisor!.IsHostRunning(own.RunId!) ? own : null;

                    return new RunningChangedEvent { Seq = seq, SessionId = sessionId, At = at, IsRunning = running, OwnRequestId = owner?.RequestId, OwnRunId = owner?.RunId };
                });
        }

        private void OnServerStateChanged(object? sender, EventArgs e) => ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
