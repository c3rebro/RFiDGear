#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;

using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// A client of the bridge as an external application would write it: it connects to the pipe of its user, reads the
    /// protocol with the options of the contract, uses a typed proxy of <see cref="IBridgeService"/> with the camel-case names,
    /// and receives the events on the object that implements <see cref="IBridgeClientEvents"/>. It also shows what a client has
    /// to do that the library does not do for it: to receive the error data of the bridge it overrides
    /// <c>GetErrorDetailsDataType</c>.
    /// </summary>
    internal sealed class BridgeTestClient : IBridgeClientEvents, IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<BridgeEvent> _events = new List<BridgeEvent>();
        private readonly NamedPipeClientStream _pipe;

        private BridgeTestClient(NamedPipeClientStream pipe)
        {
            _pipe = pipe;
            var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = BridgeJson.CreateClientOptions() };
            Rpc = new ClientRpc(new HeaderDelimitedMessageHandler(pipe, formatter));
            Rpc.AddLocalRpcTarget<IBridgeClientEvents>(this, new JsonRpcTargetOptions { MethodNameTransform = CommonMethodNameTransforms.CamelCase });
            Service = Rpc.Attach<IBridgeService>(new JsonRpcProxyOptions { MethodNameTransform = CommonMethodNameTransforms.CamelCase });
            Rpc.StartListening();
        }

        /// <summary>Gets the connection, for calls that the typed proxy does not make (an unknown method, a request with an extra member).</summary>
        internal JsonRpc Rpc { get; }

        /// <summary>Gets the typed proxy of the contract.</summary>
        internal IBridgeService Service { get; }

        /// <summary>Gets the events received so far.</summary>
        internal IReadOnlyList<BridgeEvent> Events
        {
            get
            {
                lock (_gate)
                {
                    return _events.ToList();
                }
            }
        }

        /// <summary>Connects to the pipe, with the access check a client should make.</summary>
        /// <param name="pipeName">The pipe name.</param>
        /// <param name="timeoutMs">How long to wait for the pipe.</param>
        /// <returns>The connected client.</returns>
        internal static async Task<BridgeTestClient> ConnectAsync(string pipeName, int timeoutMs = 5000)
        {
            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await pipe.ConnectAsync(timeoutMs);

                return new BridgeTestClient(pipe);
            }
            catch
            {
                pipe.Dispose();
                throw;
            }
        }

        /// <summary>Makes the handshake with the protocol range 1 to 1.</summary>
        /// <param name="events">The events to ask for.</param>
        /// <returns>The answer.</returns>
        internal Task<HelloResponse> HelloAsync(params string[] events) =>
            Service.Hello(new HelloRequest { ClientName = "test", ClientVersion = "1.0", ProtocolMajorMin = 1, ProtocolMajorMax = 1, Events = events });

        /// <summary>Waits until a number of events have arrived.</summary>
        /// <param name="count">How many.</param>
        /// <param name="timeoutMs">How long to wait.</param>
        /// <returns>The events.</returns>
        internal async Task<IReadOnlyList<BridgeEvent>> WaitForEventsAsync(int count, int timeoutMs = 5000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < until)
            {
                var events = Events;

                if (events.Count >= count)
                {
                    return events;
                }

                await Task.Delay(10);
            }

            throw new TimeoutException($"Only {Events.Count} of {count} events arrived.");
        }

        /// <summary>Reads an error the way a client has to: the data of the bridge, whatever the library calls it.</summary>
        /// <param name="failure">The exception.</param>
        /// <returns>The error data, or <see langword="null"/> when there is none.</returns>
        internal static BridgeErrorData? DataOf(RemoteInvocationException failure) => failure.DeserializedErrorData as BridgeErrorData;

        /// <inheritdoc />
        public void RunStarted(RunStartedEvent e) => Add(e);

        /// <inheritdoc />
        public void RunCompleted(RunCompletedEvent e) => Add(e);

        /// <inheritdoc />
        public void RunningChanged(RunningChangedEvent e) => Add(e);

        /// <inheritdoc />
        public void ReaderChanged(ReaderChangedEvent e) => Add(e);

        /// <inheritdoc />
        public void ProjectChanged(ProjectChangedEvent e) => Add(e);

        /// <inheritdoc />
        public void ShuttingDown(ShuttingDownEvent e) => Add(e);

        /// <inheritdoc />
        public void Dispose()
        {
            Rpc.Dispose();
            _pipe.Dispose();
        }

        private void Add(BridgeEvent e)
        {
            lock (_gate)
            {
                _events.Add(e);
            }
        }

        private sealed class ClientRpc : JsonRpc
        {
            public ClientRpc(IJsonRpcMessageHandler handler)
                : base(handler)
            {
            }

            // Without this the library reads error.data as its own CommonErrorData and the data of the bridge is lost.
            protected override Type? GetErrorDetailsDataType(JsonRpcError error) => typeof(BridgeErrorData);
        }
    }
}
