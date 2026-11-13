#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;

using Serilog;

using StreamJsonRpc;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>The time limits of a connection; tests shorten them.</summary>
    internal sealed class BridgeServerTimings
    {
        /// <summary>Gets how long a client may stay connected without making the handshake.</summary>
        internal TimeSpan HandshakeDeadline { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>Gets how long sending one notification may take before the client is taken to have stopped reading.</summary>
        internal TimeSpan SendWatchdog { get; init; } = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// One client connected to the pipe: the JSON-RPC connection with its service, and the one consumer that sends the
    /// notifications in order. It ends when the client disconnects, when the handshake does not come in time, when a message
    /// is too long, when the client stops reading, or when the host stops it. It never touches the main window.
    /// </summary>
    internal sealed class BridgeSession : IDisposable
    {
        private static ILogger Logger => Log.ForContext<BridgeSession>();

        private readonly Stream _pipe;
        private readonly BridgeServerTimings _timings;
        private readonly int _maxMessageBytes;
        private readonly CancellationTokenSource _ended = new CancellationTokenSource();
        private CappedReadStream? _capped;
        private JsonRpc? _rpc;
        private long _seq;
        private int _aborted;
        private int _disposed;

        /// <summary>Creates the session of a connected client.</summary>
        /// <param name="pipe">The connected pipe.</param>
        /// <param name="options">What the host offers.</param>
        /// <param name="timings">The time limits.</param>
        /// <param name="maxMessageBytes">The longest message that is accepted, in bytes.</param>
        internal BridgeSession(Stream pipe, BridgeServiceOptions options, BridgeServerTimings timings, int maxMessageBytes = BridgeProtocol.MaxMessageBytes)
        {
            _pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));
            _timings = timings ?? throw new ArgumentNullException(nameof(timings));
            _maxMessageBytes = maxMessageBytes;
            SessionId = Guid.NewGuid().ToString("N");
            Service = new BridgeService(options, SessionId);
        }

        /// <summary>Gets the id of the connection, which is also on every event.</summary>
        internal string SessionId { get; }

        /// <summary>Gets the JSON-RPC connection once it has started; for tests that look at how it is set up.</summary>
        internal JsonRpc? Rpc => _rpc;

        /// <summary>Gets the service this connection calls.</summary>
        internal BridgeService Service { get; }

        /// <summary>Gets the events waiting to be sent to this client.</summary>
        internal BridgeEventQueue Events { get; } = new BridgeEventQueue();

        /// <summary>Serves the client until the connection ends.</summary>
        /// <returns>A task that completes when the connection has ended and everything is released.</returns>
        internal async Task RunAsync()
        {
            // Stopped between being accepted and starting: nothing is served, and the pipe has been closed by Abort.
            if (Volatile.Read(ref _aborted) != 0)
            {
                Events.Complete();

                return;
            }

            _capped = new CappedReadStream(_pipe, _maxMessageBytes);
            var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = BridgeJson.CreateServerOptions() };
            var rpc = new BridgeJsonRpc(new HeaderDelimitedMessageHandler(_capped, formatter));
            _rpc = rpc;

            // Nothing of the traffic may reach a trace listener: later slices carry keys in messages.
            rpc.TraceSource.Switch.Level = SourceLevels.Off;
            rpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;

            // Registered by interface: only the methods of the contract can be called, not whatever else the class has.
            rpc.AddLocalRpcTarget<IBridgeService>(Service, new JsonRpcTargetOptions
            {
                MethodNameTransform = CommonMethodNameTransforms.CamelCase,
                NotifyClientOfEvents = false,
            });
            rpc.StartListening();
            Logger.Information("External control: a client connected (session {SessionId})", SessionId);

            var sender = Task.Run(() => SendAsync(rpc));
            var deadline = Task.Run(() => EnforceHandshakeDeadlineAsync());

            try
            {
                await rpc.Completion.ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is InvalidDataException || e is OperationCanceledException || e is ConnectionLostException)
            {
                // The client went away or sent something that ended the connection: the end of a session, not a fault.
            }
            catch (Exception e)
            {
                Logger.Warning("External control: a connection ended with an error ({ExceptionType}, session {SessionId})", e.GetType().FullName, SessionId);
            }
            finally
            {
                Events.Complete();
                _ended.Cancel();
                await Task.WhenAll(sender, deadline).ConfigureAwait(false);

                if (_capped.EndedBecauseTooLarge)
                {
                    Logger.Warning("External control: a client sent a message longer than the limit and was disconnected (session {SessionId})", SessionId);
                }

                Logger.Information("External control: the client disconnected (session {SessionId})", SessionId);
            }
        }

        /// <summary>
        /// Ends the connection. The pipe is closed too, so this also works for a session that has not started yet (a stop that
        /// comes between the accept and <see cref="RunAsync"/>) and for one whose write is stuck.
        /// </summary>
        internal void Abort()
        {
            Volatile.Write(ref _aborted, 1);
            _rpc?.Dispose();
            _pipe.Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            Abort();
            _pipe.Dispose();
            _ended.Dispose();
        }

        // The one consumer of the queue. A notification that the client does not take within the watchdog ends the
        // connection: the events that are queued are hints, and the results stay in the history.
        private async Task SendAsync(JsonRpc rpc)
        {
            try
            {
                while (await Events.DequeueAsync(_ended.Token).ConfigureAwait(false) is { } pending)
                {
                    if (!Service.IsEventEnabled(pending.Name))
                    {
                        Events.MarkSent();
                        continue;
                    }

                    var evt = pending.Create(++_seq, SessionId, DateTimeOffset.UtcNow);
                    var send = rpc.NotifyAsync(pending.Name, new object?[] { evt });
                    var finished = await Task.WhenAny(send, Task.Delay(_timings.SendWatchdog, _ended.Token)).ConfigureAwait(false);

                    if (finished != send)
                    {
                        Logger.Warning("External control: a client did not take a notification in time and was disconnected (session {SessionId})", SessionId);
                        Abort();

                        return;
                    }

                    await send.ConfigureAwait(false);
                    Events.MarkSent();
                }
            }
            catch (OperationCanceledException)
            {
                // The connection ended.
            }
            catch (Exception e)
            {
                Logger.Warning("External control: sending a notification failed ({ExceptionType}, session {SessionId})", e.GetType().FullName, SessionId);
                Abort();
            }
        }

        private async Task EnforceHandshakeDeadlineAsync()
        {
            try
            {
                await Task.Delay(_timings.HandshakeDeadline, _ended.Token).ConfigureAwait(false);

                if (!Service.HelloDone)
                {
                    Logger.Warning("External control: a client did not make the handshake in time and was disconnected (session {SessionId})", SessionId);
                    Abort();
                }
            }
            catch (OperationCanceledException)
            {
                // The connection ended first.
            }
        }
    }
}
