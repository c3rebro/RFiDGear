#nullable enable
using System;
using System.Threading;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Services.AutoMode;

using Serilog;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// Automatic mode as a client sees it: switch it on and off, read what it is doing, and be told about every card it
    /// processed. It adds nothing to the mode itself (<see cref="AutoModeController"/> is the one state machine, which the menu
    /// and the <c>AUTOMODE=1</c> argument drive as well); it asks the host to start it, remembers whether it was a client that
    /// did, and turns what the mode reports into results and events.
    /// <list type="bullet">
    /// <item>A client does not take over the mode of the user: a start while it was switched on by somebody else is refused.</item>
    /// <item>The result of every run of the mode is numbered in the same sequence as the runs clients start, kept for
    /// <c>listRunResults</c>, and sent once as <c>autoRunCompleted</c>; none is lost with an event that was dropped.</item>
    /// <item>The result of the run that stops the mode comes before the notice that the mode stopped: the host raises them in
    /// that order on one thread, and the events keep it.</item>
    /// <item>A mode that a client started with <c>stopOnDisconnect</c> ends with the connection that started it.</item>
    /// </list>
    /// </summary>
    internal sealed class AutoModeBridge
    {
        private static ILogger Logger => Log.ForContext<AutoModeBridge>();

        // The start of a client that is in effect: which connection, and what it agreed for the end of that connection.
        private sealed record ClientStart(string SessionId, bool StopOnDisconnect);

        // The start that this thread is making, for the length of the call. The host raises "state changed" synchronously inside
        // the call, on this thread, so a handler can tell the start of a client from a start of the user that happens at the same
        // moment on another thread.
        [ThreadStatic]
        private static ClientStart? s_starting;

        private readonly IRFiDGearExtensionHost _host;
        private readonly IAutoModeControl _control;
        private readonly RunRegistry _registry;
        private readonly Action<string, bool, BridgeEventFactory> _publish;
        private readonly Func<bool> _isShuttingDown;
        private readonly Func<string?> _busyActivity;
        private readonly object _gate = new object();
        private ClientStart? _clientStart;
        private int _attached;

        /// <summary>Creates the bridge of automatic mode.</summary>
        /// <param name="host">The host service of RFiDGear.</param>
        /// <param name="control">The same automatic mode host, as the one that says why a start was refused.</param>
        /// <param name="registry">The table that numbers and keeps the results, which outlives the connections.</param>
        /// <param name="publish">Hands an event to the client that is connected (name, whether it is durable, the maker of the event).</param>
        /// <param name="isShuttingDown">Says whether RFiDGear is closing.</param>
        /// <param name="busyActivity">Says what the application is busy with, for the hint in a "busy" refusal.</param>
        internal AutoModeBridge(
            IRFiDGearExtensionHost host,
            IAutoModeControl control,
            RunRegistry registry,
            Action<string, bool, BridgeEventFactory> publish,
            Func<bool> isShuttingDown,
            Func<string?> busyActivity)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _control = control ?? throw new ArgumentNullException(nameof(control));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
            _isShuttingDown = isShuttingDown ?? throw new ArgumentNullException(nameof(isShuttingDown));
            _busyActivity = busyActivity ?? throw new ArgumentNullException(nameof(busyActivity));
        }

        /// <summary>
        /// Gets whether a run is going on in a mode that a client switched on. The application asks before it closes while one is,
        /// and the shutdown notice says so.
        /// </summary>
        internal bool HasRunInFlight
        {
            get
            {
                lock (_gate)
                {
                    return _clientStart != null && _host.AutoMode.IsAutoModeEnabled && _host.Execution.IsRunning;
                }
            }
        }

        /// <summary>Starts to listen to the host. Safe to call again.</summary>
        internal void Attach()
        {
            if (Interlocked.Exchange(ref _attached, 1) != 0)
            {
                return;
            }

            // The host raises both synchronously, on the thread that made the change: the handlers only number, queue and return.
            _host.AutoMode.AutoModeStateChanged += OnStateChanged;
            _host.AutoMode.AutoModeRunCompleted += OnRunCompleted;
        }

        /// <summary>Stops listening to the host.</summary>
        internal void Detach()
        {
            if (Interlocked.Exchange(ref _attached, 0) == 0)
            {
                return;
            }

            _host.AutoMode.AutoModeStateChanged -= OnStateChanged;
            _host.AutoMode.AutoModeRunCompleted -= OnRunCompleted;
        }

        /// <summary>Switches automatic mode on for a client.</summary>
        /// <param name="sessionId">The connection of the client.</param>
        /// <param name="request">Whether the mode ends with the connection.</param>
        /// <returns>That the mode is on, and whether this bridge had switched it on before.</returns>
        internal StartAutoModeResponse Start(string sessionId, StartAutoModeRequest request)
        {
            if (request == null)
            {
                throw BridgeFault.InvalidRequest("request");
            }

            if (_isShuttingDown())
            {
                throw BridgeFault.ShuttingDown();
            }

            var mine = new ClientStart(sessionId, request.StopOnDisconnect);
            AutoModeStartResult result;

            s_starting = mine;

            try
            {
                result = _control.TryStart();
            }
            finally
            {
                s_starting = null;
            }

            switch (result)
            {
                case AutoModeStartResult.Started:
                    Logger.Information("External control: automatic mode was switched on by a client (stopOnDisconnect {StopOnDisconnect})", mine.StopOnDisconnect);

                    return new StartAutoModeResponse { Enabled = true, AlreadyEnabled = false, StopOnDisconnect = mine.StopOnDisconnect };

                case AutoModeStartResult.AlreadyEnabled:
                    lock (_gate)
                    {
                        // The user's mode is the user's. Only a mode that a client switched on can be agreed on again, by whichever
                        // client asks now: the agreement for its end then belongs to that client.
                        if (_clientStart == null)
                        {
                            throw BridgeFault.AutoModeActive();
                        }

                        _clientStart = mine;
                    }

                    return new StartAutoModeResponse { Enabled = true, AlreadyEnabled = true, StopOnDisconnect = mine.StopOnDisconnect };

                case AutoModeStartResult.NoReader:
                    throw BridgeFault.NoReader();

                case AutoModeStartResult.NoTasks:
                    throw BridgeFault.NoProjectLoaded();

                default:
                    throw BridgeFault.Busy(_busyActivity() ?? BusyActivities.Other);
            }
        }

        /// <summary>Switches automatic mode off, whoever switched it on. A run that is going on is not interrupted.</summary>
        /// <returns>Whether the mode was on.</returns>
        internal StopAutoModeResponse Stop()
        {
            var wasEnabled = _host.AutoMode.IsAutoModeEnabled;

            _host.AutoMode.Stop();

            return new StopAutoModeResponse { WasEnabled = wasEnabled };
        }

        /// <summary>Reads what automatic mode is doing.</summary>
        /// <returns>The state of the mode.</returns>
        internal AutoModeStatusDto GetStatus()
        {
            lock (_gate)
            {
                var enabled = _host.AutoMode.IsAutoModeEnabled;
                var client = enabled ? _clientStart : null;

                return new AutoModeStatusDto
                {
                    Enabled = enabled,
                    StartedBy = enabled ? (client != null ? RunSources.Bridge : RunSources.Other) : null,
                    StopOnDisconnect = client?.StopOnDisconnect ?? false,
                    LastStopReason = BridgeMapper.ToWire(_host.AutoMode.LastStopReason),
                };
            }
        }

        /// <summary>
        /// A connection ended. A mode that this connection switched on, and agreed to end with it, is switched off: nobody is
        /// there to look at what it does. A mode that the user switched on, or that a client left running on purpose, is not touched.
        /// </summary>
        /// <param name="sessionId">The connection that ended.</param>
        internal void OnSessionEnded(string sessionId)
        {
            lock (_gate)
            {
                if (_clientStart == null || !_clientStart.StopOnDisconnect || !string.Equals(_clientStart.SessionId, sessionId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            Logger.Information("External control: the client that switched automatic mode on has gone; the mode is switched off");
            _host.AutoMode.Stop();
        }

        // Raised synchronously by whoever switched the mode on or off, on that thread: the notice is made here, at that moment, so
        // that a quick switch on and off gives two notices and not one that says "off" twice.
        private void OnStateChanged(object? sender, EventArgs e)
        {
            var starting = s_starting;
            bool enabled;

            lock (_gate)
            {
                // A raise made inside the start of a client is that start. Every other raise (the user, an extension, a stop of a client, a
                // fault of the mode) ends the claim of a client: a mode that is on afterwards is not the client's, and a mode that
                // is off has nobody to be stopped for. Where it is unclear the mode is taken as not the client's, which never stops a
                // mode that the user may have switched on.
                _clientStart = starting;
                enabled = starting != null || _host.AutoMode.IsAutoModeEnabled;
            }

            var startedBy = enabled ? (starting != null ? RunSources.Bridge : RunSources.Other) : null;
            var reason = BridgeMapper.ToWire(_host.AutoMode.LastStopReason);
            var lastResultSeq = _registry.LastResultSeq;

            _publish(
                BridgeEvents.AutoModeChanged,
                true,
                (seq, sessionId, at) => new AutoModeChangedEvent
                {
                    Seq = seq,
                    SessionId = sessionId,
                    At = at,
                    Enabled = enabled,
                    StartedBy = startedBy,
                    LastStopReason = reason,
                    LastResultSeq = lastResultSeq,
                });
        }

        // Raised synchronously by the mode for every run it started, after the run has ended and before the mode stops because of it.
        private void OnRunCompleted(object? sender, ExecResult result)
        {
            if (result == null)
            {
                return;
            }

            var runId = string.IsNullOrEmpty(result.RunId) ? Guid.NewGuid().ToString("N") : result.RunId;
            var dto = _registry.RecordUnrequested(seq => BridgeMapper.ToDto(result, runId, null, seq, RunSources.AutoMode));

            // No UID and no path: the number, the status and the counts.
            Logger.Information(
                "External control: automatic mode ended run {RunId} as {Status} (result {ResultSeq}, {TotalTasks} tasks, {FailedTasks} failed)",
                dto.RunId, dto.Status, dto.ResultSeq, dto.TotalTasks, dto.FailedTasks);

            _publish(
                BridgeEvents.AutoRunCompleted,
                true,
                (seq, sessionId, at) => new AutoRunCompletedEvent { Seq = seq, SessionId = sessionId, At = at, Result = dto });
        }
    }
}
