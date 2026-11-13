#nullable enable
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;

using Serilog;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>Where a <see cref="BridgeServer"/> stands.</summary>
    internal enum BridgeServerState
    {
        /// <summary>Not started.</summary>
        Created,

        /// <summary>The pipe exists and waits for a client.</summary>
        Listening,

        /// <summary>A client is connected; the pipe does not accept another one until it leaves.</summary>
        SessionActive,

        /// <summary>The pipe could not be created (the name is taken, or access was denied) and is not retried.</summary>
        Failed,

        /// <summary>Stopped on purpose.</summary>
        Stopped,
    }

    /// <summary>
    /// Listens on one named pipe and serves one client at a time. The pipe is created for the current user only
    /// (<see cref="PipeOptions.CurrentUserOnly"/>) with a single instance, so a second client waits at the operating system
    /// until the first has left, and a process that already holds the name makes the start fail instead of being served by
    /// accident. If the pipe cannot be created the server stays off and says so in the log: it never falls back to a wider
    /// access.
    /// </summary>
    internal sealed class BridgeServer : IDisposable
    {
        private static ILogger Logger => Log.ForContext<BridgeServer>();

        private readonly string _pipeName;
        private readonly BridgeServiceOptions _options;
        private readonly BridgeServerTimings _timings;
        private readonly int _maxMessageBytes;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly object _gate = new object();
        private NamedPipeServerStream? _pipe;
        private BridgeSession? _session;
        private Task? _loop;
        private BridgeServerState _state = BridgeServerState.Created;

        /// <summary>Creates the server; <see cref="Start"/> opens the pipe.</summary>
        /// <param name="pipeName">The name of the pipe (<see cref="PipeNames.ForCurrentUser"/>).</param>
        /// <param name="options">What the host offers.</param>
        /// <param name="timings">The time limits; the defaults when <see langword="null"/>.</param>
        /// <param name="maxMessageBytes">The longest message that is accepted, in bytes.</param>
        internal BridgeServer(string pipeName, BridgeServiceOptions options, BridgeServerTimings? timings = null, int maxMessageBytes = BridgeProtocol.MaxMessageBytes)
        {
            _pipeName = string.IsNullOrWhiteSpace(pipeName) ? throw new ArgumentException("A pipe name is needed.", nameof(pipeName)) : pipeName;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timings = timings ?? new BridgeServerTimings();
            _maxMessageBytes = maxMessageBytes;
        }

        /// <summary>Gets where the server stands.</summary>
        internal BridgeServerState State
        {
            get
            {
                lock (_gate)
                {
                    return _state;
                }
            }
        }

        /// <summary>Gets the pipe that waits for a client, or <see langword="null"/>; for tests that look at its access control.</summary>
        internal NamedPipeServerStream? WaitingPipe
        {
            get
            {
                lock (_gate)
                {
                    return _pipe;
                }
            }
        }

        /// <summary>Gets the connection of the client that is connected, or <see langword="null"/>.</summary>
        internal BridgeSession? ActiveSession
        {
            get
            {
                lock (_gate)
                {
                    return _session;
                }
            }
        }
        /// <summary>Gets whether a client is connected.</summary>
        internal bool HasSession => State == BridgeServerState.SessionActive;

        /// <summary>
        /// Opens the pipe and starts to wait for a client. The first instance of the pipe is created before this returns, so a
        /// name that is taken shows at once.
        /// </summary>
        /// <returns><see langword="true"/> when the pipe is listening; <see langword="false"/> when it could not be created.</returns>
        internal bool Start()
        {
            lock (_gate)
            {
                if (_state != BridgeServerState.Created)
                {
                    return _state == BridgeServerState.Listening || _state == BridgeServerState.SessionActive;
                }

                if (!TryCreatePipe())
                {
                    return false;
                }

                _state = BridgeServerState.Listening;
                _loop = Task.Run(() => AcceptLoopAsync());

                return true;
            }
        }

        /// <summary>
        /// Puts an event on the queue of the client that is connected, or drops it when there is none: events are hints, and a
        /// client that connects later reads the truth from <c>getStatus</c> and <c>getRunResult</c>.
        /// </summary>
        /// <param name="name">The wire name of the event.</param>
        /// <param name="durable"><see langword="true"/> for an event that must not be merged or lost (a result); a client that lets them pile up is disconnected.</param>
        /// <param name="create">Makes the event when it is about to be sent.</param>
        internal void Publish(string name, bool durable, BridgeEventFactory create)
        {
            BridgeSession? session;

            lock (_gate)
            {
                session = _session;
            }

            if (session == null)
            {
                return;
            }

            var accepted = durable ? session.Events.EnqueueDurable(name, create) : session.Events.EnqueueState(name, create);

            if (!accepted && durable)
            {
                Logger.Warning("External control: a client let too many results pile up and was disconnected (session {SessionId})", session.SessionId);
                session.Abort();
            }
        }

        /// <summary>Stops listening and ends the connection that is open.</summary>
        internal void Stop()
        {
            BridgeSession? session;
            NamedPipeServerStream? waiting;

            lock (_gate)
            {
                if (_state == BridgeServerState.Stopped)
                {
                    return;
                }

                _state = BridgeServerState.Stopped;
                session = _session;
                waiting = _pipe;
                _pipe = null;
            }

            _stop.Cancel();
            session?.Abort();
            waiting?.Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();

            try
            {
                _loop?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // The loop ended with an exception that it has logged.
            }

            _stop.Dispose();
        }

        // Creates the next instance of the pipe. Must be called under the lock.
        private bool TryCreatePipe()
        {
            try
            {
                _pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // The name is taken, or the system refuses. Not retried, and not widened: the log has the type only.
                _state = BridgeServerState.Failed;
                Logger.Warning("External control: the pipe could not be created and the bridge stays off ({ExceptionType})", e.GetType().FullName);

                return false;
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                NamedPipeServerStream pipe;

                lock (_gate)
                {
                    if (_state == BridgeServerState.Stopped || _pipe == null)
                    {
                        return;
                    }

                    pipe = _pipe;
                }

                try
                {
                    await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                }
                catch (Exception e) when (e is OperationCanceledException || e is ObjectDisposedException || e is IOException)
                {
                    return;
                }

                var session = new BridgeSession(pipe, _options, _timings, _maxMessageBytes);

                try
                {
                    lock (_gate)
                    {
                        if (_state == BridgeServerState.Stopped)
                        {
                            return;
                        }

                        _session = session;
                        _pipe = null;
                        _state = BridgeServerState.SessionActive;
                    }

                    try
                    {
                        await session.RunAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Logger.Warning("External control: a session ended with an error ({ExceptionType})", e.GetType().FullName);
                    }
                }
                finally
                {
                    // The pipe must be closed before the next instance is created: with one instance allowed, a session that was
                    // ended while a write was stuck would otherwise leave the name busy and turn the bridge off for good.
                    session.Dispose();
                }

                lock (_gate)
                {
                    _session = null;

                    if (_state == BridgeServerState.Stopped)
                    {
                        return;
                    }

                    // The next instance. In the moment between the two there is no pipe, and a client that retries finds it.
                    if (!TryCreatePipe())
                    {
                        return;
                    }

                    _state = BridgeServerState.Listening;
                }
            }
        }
    }
}
