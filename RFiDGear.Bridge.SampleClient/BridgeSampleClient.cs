#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace RFiDGear.Bridge.SampleClient
{
    /// <summary>How a run ended, as far as the client can tell.</summary>
    public enum RunOutcomeKind
    {
        /// <summary>The run ended and RFiDGear reported it; <see cref="RunOutcome.Result"/> says how (it may be missing, see the detail).</summary>
        Completed,

        /// <summary>RFiDGear refused the request and said that nothing was started. The card was not touched, and a new attempt is allowed.</summary>
        NotStarted,

        /// <summary>The client cannot tell whether the card was touched. Look at the card before you start the job again.</summary>
        Unknown,
    }

    /// <summary>What <see cref="BridgeSampleClient.RunOnceAsync"/> and <see cref="BridgeSampleClient.ResumeAsync"/> found out.</summary>
    /// <param name="Kind">How it ended.</param>
    /// <param name="RequestId">The request id of the run. Keep it: it is the only way to ask about the run later.</param>
    /// <param name="Result">The result of the run, when RFiDGear still holds it.</param>
    /// <param name="Detail">A sentence for the operator; never a key, a path or a value of a variable.</param>
    public sealed record RunOutcome(RunOutcomeKind Kind, string RequestId, RunResultDto? Result, string Detail);

    /// <summary>
    /// A small client of the external control bridge of RFiDGear, written the way the client guide asks for. It is the
    /// connection (pipe of the current user only, the error data of the bridge made readable), the handshake, the call that
    /// starts a run <em>without ever sending it twice</em>, the long poll for the result, the catch-up after a lost connection and
    /// the supplier of card keys for a vault of the caller's. It is a sample: read it, copy what you need, and keep the rules.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class BridgeSampleClient : IBridgeClientEvents, IBridgeClientKeys, IAsyncDisposable
    {
        private static readonly TimeSpan DefaultStartTimeout = TimeSpan.FromSeconds(30);

        private readonly NamedPipeClientStream _pipe;
        private readonly JsonRpc _rpc;
        private TimeSpan _longPoll = TimeSpan.FromSeconds(20);

        private BridgeSampleClient(NamedPipeClientStream pipe)
        {
            _pipe = pipe;

            // The options of the contract: camel-case names written explicitly, and unknown members of an answer skipped, so that a
            // newer RFiDGear that adds a field does not break this client.
            var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = BridgeJson.CreateClientOptions() };
            _rpc = new ErrorDataRpc(new HeaderDelimitedMessageHandler(pipe, formatter));

            // The host calls the client by these names; the methods have the names of the contract with a capital letter.
            var names = new JsonRpcTargetOptions { MethodNameTransform = CommonMethodNameTransforms.CamelCase };
            _rpc.AddLocalRpcTarget<IBridgeClientEvents>(this, names);
            _rpc.AddLocalRpcTarget<IBridgeClientKeys>(this, names);
            Service = _rpc.Attach<IBridgeService>(new JsonRpcProxyOptions { MethodNameTransform = CommonMethodNameTransforms.CamelCase });
            _rpc.StartListening();
        }

        /// <summary>Gets the calls the host offers. Use the methods of this class where they exist; they hold the rules.</summary>
        public IBridgeService Service { get; }

        /// <summary>Gets the answer to the handshake: the capabilities, the id of this RFiDGear process and the limits.</summary>
        public HelloResponse Hello { get; private set; } = new HelloResponse();

        /// <summary>Gets or sets how long a <c>startRun</c> may take before the client stops waiting (and asks, instead of sending it again).</summary>
        public TimeSpan StartTimeout { get; set; } = DefaultStartTimeout;

        /// <summary>
        /// Gets or sets how long one question about a run waits for the run to end (the long poll of <c>getRunResult</c>). The client
        /// asks again when it runs out, so this is not how long a run may take. At most <see cref="BridgeProtocol.MaxWaitMs"/> milliseconds.
        /// </summary>
        public TimeSpan LongPoll
        {
            get => _longPoll;
            set => _longPoll = value > TimeSpan.Zero && value.TotalMilliseconds <= BridgeProtocol.MaxWaitMs
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), "The wait has to be longer than nothing and at most the limit of the host.");
        }

        /// <summary>
        /// Gets or sets the vault of this client: what it answers when the host asks for a key. Without one every request is
        /// refused (<see cref="KeyResolveResults.NotAuthorized"/>). It must check the reference against what it serves.
        /// </summary>
        public Func<ResolveKeyRequest, CancellationToken, Task<ResolveKeyResponse>>? KeyVault { get; set; }

        /// <summary>Raised for every notification the host sends and this client asked for. Keep the handler short and never call the host from it.</summary>
        public event Action<BridgeEvent>? EventReceived;

        /// <summary>Gets the pipe name of the RFiDGear that runs for the current Windows user.</summary>
        /// <returns>The pipe name, without the <c>\\.\pipe\</c> prefix.</returns>
        public static string DefaultPipeName()
        {
            using var identity = WindowsIdentity.GetCurrent();

            return BridgeProtocol.PipeNameFor(identity.User?.Value ?? throw new InvalidOperationException("The current Windows user has no security identifier."));
        }

        /// <summary>Connects to RFiDGear and makes the handshake.</summary>
        /// <param name="pipeName">The pipe name; the one of the current user when <see langword="null"/>.</param>
        /// <param name="events">The wire names of the events to receive (<see cref="BridgeEvents"/>); only these are sent.</param>
        /// <param name="connectTimeout">How long to wait for the pipe; ten seconds when <see langword="null"/>.</param>
        /// <param name="cancellationToken">Cancels the connection.</param>
        /// <returns>The connected client.</returns>
        public static async Task<BridgeSampleClient> ConnectAsync(
            string? pipeName = null,
            IReadOnlyList<string>? events = null,
            TimeSpan? connectTimeout = null,
            CancellationToken cancellationToken = default)
        {
            // CurrentUserOnly makes the client refuse a pipe that another user created under this name.
            var pipe = new NamedPipeClientStream(".", pipeName ?? DefaultPipeName(), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await pipe.ConnectAsync(connectTimeout ?? TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                var client = new BridgeSampleClient(pipe);

                try
                {
                    client.Hello = await client.Service.Hello(new HelloRequest
                    {
                        ClientName = "RFiDGear.Bridge.SampleClient",
                        ClientVersion = "1.0",
                        ProtocolMajorMin = BridgeProtocol.Major,
                        ProtocolMajorMax = BridgeProtocol.Major,
                        Events = events ?? Array.Empty<string>(),
                    }).ConfigureAwait(false);

                    return client;
                }
                catch
                {
                    await client.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
            catch
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>Says whether this RFiDGear offers a capability. Decide what to call from this, never from the version of the application.</summary>
        /// <param name="capability">One of <see cref="BridgeCapabilities"/>.</param>
        /// <returns><see langword="true"/> when it is offered.</returns>
        public bool Offers(string capability) => Hello.Capabilities.Contains(capability, StringComparer.Ordinal);

        /// <summary>
        /// Starts one run of the loaded project and waits for its result. The rule it keeps: <b>a request that may have reached
        /// RFiDGear is never repeated as a new request</b>. If the answer does not come (a timeout, a lost connection, an error
        /// that does not say "never started"), the client asks about the same request id instead. (Sending the same id again
        /// would be safe too, because RFiDGear recognises it; a new id would write the card twice.)
        /// </summary>
        /// <param name="requestId">The id of this job. Make it from the job (an order number), or write it down before the call: it is what you ask with later.</param>
        /// <param name="variables">The variables of the run (names as the project writes them, with the leading dollar sign).</param>
        /// <param name="cancellationToken">Stops waiting; it does not stop the run (use <c>cancelRun</c> for that).</param>
        /// <returns>How it ended.</returns>
        public async Task<RunOutcome> RunOnceAsync(string requestId, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(variables);

            var request = new StartRunRequest
            {
                RequestId = requestId,
                Variables = variables.Select(pair => new VariablePair { Name = pair.Key, Value = pair.Value }).ToList(),
            };

            try
            {
                await Service.StartRun(request).WaitAsync(StartTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (RemoteInvocationException failure) when (ErrorDataOf(failure) is { NeverStarted: true } data)
            {
                // The one answer that allows a new attempt: RFiDGear says that nothing was started.
                return new RunOutcome(RunOutcomeKind.NotStarted, requestId, null, "Refused: " + data.Kind);
            }
            catch (Exception failure) when (failure is TimeoutException || IsLostConnection(failure) || failure is RemoteInvocationException)
            {
                // Doubt. The run may have been started. Do not call startRun again: ask.
            }

            return await ResumeAsync(requestId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asks what became of a request, waiting for the run to end. It is also the way back after a lost connection: connect
        /// again and call this with the request id you wrote down.
        /// </summary>
        /// <param name="requestId">The request id of the run.</param>
        /// <param name="cancellationToken">Stops waiting.</param>
        /// <returns>How it ended.</returns>
        public async Task<RunOutcome> ResumeAsync(string requestId, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                GetRunResultResponse answer;

                try
                {
                    answer = await Service.GetRunResult(new GetRunResultRequest { RequestId = requestId, WaitMs = (int)LongPoll.TotalMilliseconds }).WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception failure) when (failure is TimeoutException || IsLostConnection(failure) || failure is RemoteInvocationException)
                {
                    return new RunOutcome(RunOutcomeKind.Unknown, requestId, null, "The connection to RFiDGear was lost. Connect again and ask with the same request id; do not send the job again.");
                }

                switch (answer.State)
                {
                    case RunStates.Running:
                        continue;

                    case RunStates.Completed:
                        return new RunOutcome(
                            RunOutcomeKind.Completed,
                            requestId,
                            answer.Result,
                            answer.Result == null ? "The run ended, but RFiDGear no longer holds its result." : "The run ended: " + answer.Result.Status + ".");

                    case RunStates.InterruptedByRestart:
                        return new RunOutcome(RunOutcomeKind.Unknown, requestId, null, "RFiDGear was restarted while the run was going on. The card may have been written; look at it.");

                    default:
                        // Never seen, or older than the history. It is not a proof that nothing was started.
                        return new RunOutcome(RunOutcomeKind.Unknown, requestId, null, "RFiDGear does not know this request id: either it never arrived or it is too old. Look at the card before you decide.");
                }
            }
        }

        /// <summary>
        /// Reads the results this client missed, from the number of the last one it has. Compare the <c>InstanceId</c> of the
        /// answer with the one of the handshake of the earlier connection: another id means RFiDGear was restarted and the numbers
        /// start again. <c>Gap</c> says that some results were dropped from the history of the host and are lost to this client.
        /// </summary>
        /// <param name="afterResultSeq">The number of the last result the client has; 0 for all that the host holds.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>The results, the gap flag and the number of the latest.</returns>
        public Task<ListRunResultsResponse> CatchUpAsync(long afterResultSeq, CancellationToken cancellationToken = default) =>
            Service.ListRunResults(new ListRunResultsRequest { AfterResultSeq = afterResultSeq }).WaitAsync(cancellationToken);

        /// <summary>Registers this client as the supplier of card keys, with the vault in <see cref="KeyVault"/>, for the life of the connection.</summary>
        /// <returns>The id of the provider to name in projects, and the time limits.</returns>
        public async Task<RegisterKeyProviderResponse> RegisterKeyProviderAsync()
        {
            if (!Offers(BridgeCapabilities.KeysProvider))
            {
                throw new NotSupportedException("This RFiDGear does not offer the capability " + BridgeCapabilities.KeysProvider + ".");
            }

            return await Service.RegisterKeyProvider(new RegisterKeyProviderRequest()).ConfigureAwait(false);
        }

        /// <summary>Reads the error data of the bridge from a failed call.</summary>
        /// <param name="failure">The exception of the call.</param>
        /// <returns>The data (kind, error id, whether nothing was started), or <see langword="null"/> when the error has none.</returns>
        public static BridgeErrorData? ErrorDataOf(RemoteInvocationException failure)
        {
            ArgumentNullException.ThrowIfNull(failure);

            return failure.DeserializedErrorData as BridgeErrorData;
        }

        /// <inheritdoc />
        public void RunStarted(RunStartedEvent e) => EventReceived?.Invoke(e);

        /// <inheritdoc />
        public void RunCompleted(RunCompletedEvent e) => EventReceived?.Invoke(e);

        /// <inheritdoc />
        public void RunningChanged(RunningChangedEvent e) => EventReceived?.Invoke(e);

        /// <inheritdoc />
        public void ReaderChanged(ReaderChangedEvent e) => EventReceived?.Invoke(e);

        /// <inheritdoc />
        public void ProjectChanged(ProjectChangedEvent e) => EventReceived?.Invoke(e);

        /// <inheritdoc />
        public void ShuttingDown(ShuttingDownEvent e) => EventReceived?.Invoke(e);

        /// <inheritdoc />
        public void AutoModeChanged(AutoModeChangedEvent e) => EventReceived?.Invoke(e);

        /// <inheritdoc />
        public void AutoRunCompleted(AutoRunCompletedEvent e) => EventReceived?.Invoke(e);

        /// <inheritdoc />
        public async Task<ResolveKeyResponse> ResolveKey(ResolveKeyRequest request, CancellationToken cancellationToken)
        {
            var vault = KeyVault;

            if (vault == null)
            {
                return new ResolveKeyResponse { Result = KeyResolveResults.NotAuthorized };
            }

            return await vault(request, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _rpc.Dispose();
            await _pipe.DisposeAsync().ConfigureAwait(false);
        }

        private static bool IsLostConnection(Exception failure) =>
            failure is ConnectionLostException || failure is ObjectDisposedException || failure is IOException;

        // Without this the library reads the data of an error as its own type, and the data of the bridge (kind, "never
        // started") is lost. Every client has to do it.
        private sealed class ErrorDataRpc : JsonRpc
        {
            internal ErrorDataRpc(IJsonRpcMessageHandler handler)
                : base(handler)
            {
            }

            protected override Type? GetErrorDetailsDataType(JsonRpcError error)
            {
                ArgumentNullException.ThrowIfNull(error);

                return typeof(BridgeErrorData);
            }
        }
    }
}
