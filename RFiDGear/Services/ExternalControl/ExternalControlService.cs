#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;

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
        internal ExternalControlService(
            IRFiDGearExtensionHost host,
            Func<Action, Task> dispatchToUi,
            Func<bool> isReaderInitializing,
            string appVersion,
            string pipeName,
            BridgeServerTimings? timings = null,
            TimeSpan? uiTimeout = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _instanceId = Guid.NewGuid().ToString("N");

            var status = new BridgeStatusProvider(
                host,
                _instanceId,
                () => IsStartupComplete,
                isReaderInitializing,
                dispatchToUi,
                uiTimeout ?? TimeSpan.FromSeconds(2));

            // What the host offers in this version of the bridge; a capability appears only when everything behind it is there.
            var options = new BridgeServiceOptions
            {
                AppVersion = appVersion ?? string.Empty,
                InstanceId = _instanceId,
                Capabilities = new HashSet<string>(StringComparer.Ordinal)
                {
                    BridgeCapabilities.Session,
                    BridgeCapabilities.Status,
                    BridgeCapabilities.EventsRunning,
                    BridgeCapabilities.EventsLifecycle,
                },
                IsStartupComplete = () => IsStartupComplete,
                GetStatus = status.GetStatusAsync,
            };

            _server = new BridgeServer(pipeName, options, timings);
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
        /// Gets or sets whether a run that a client started is in flight; the shutdown notice says so. Nothing starts runs over
        /// the bridge yet, so it is always <see langword="false"/> until the execution slice sets it.
        /// </summary>
        internal Func<bool> RunInFlight { get; set; } = () => false;

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

            // The only hook that every way out of the application shares (they all end in Environment.Exit).
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Logger.Information("External control: listening (instance {InstanceId})", _instanceId);

            return true;
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
            _server.StateChanged -= OnServerStateChanged;
            _server.Dispose();
        }

        private void OnIsRunningChanged(object? sender, EventArgs e) =>
            _server.Publish(
                BridgeEvents.RunningChanged,
                durable: false,
                (seq, sessionId, at) => new RunningChangedEvent { Seq = seq, SessionId = sessionId, At = at, IsRunning = _host.Execution.IsRunning });

        private void OnServerStateChanged(object? sender, EventArgs e) => ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
