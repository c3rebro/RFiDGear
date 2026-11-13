#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;

using Serilog;

using StreamJsonRpc;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>What the host tells every connection about itself, and which capabilities it offers right now.</summary>
    internal sealed class BridgeServiceOptions
    {
        /// <summary>Gets the version of the RFiDGear application, as text; informational for the client.</summary>
        internal string AppVersion { get; init; } = string.Empty;

        /// <summary>Gets an id of this process, which changes when RFiDGear is restarted.</summary>
        internal string InstanceId { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets the capabilities this host offers. A method of a capability that is not in the set is refused with
        /// <see cref="BridgeErrorCodes.CapabilityNotSupported"/>. The handshake itself is always there.
        /// </summary>
        internal IReadOnlySet<string> Capabilities { get; init; } = new HashSet<string>(StringComparer.Ordinal) { BridgeCapabilities.Session };

        /// <summary>
        /// Gets whether RFiDGear has finished starting. Until it says so only <c>hello</c> and <c>getStatus</c> are served; every
        /// other method answers <see cref="BridgeErrorCodes.NotReady"/>.
        /// </summary>
        internal Func<bool> IsStartupComplete { get; init; } = () => true;

        /// <summary>Gets what <c>getStatus</c> answers with; a host that offers the <c>status</c> capability sets it.</summary>
        internal Func<Task<StatusResponse>>? GetStatus { get; init; }

        /// <summary>Gets what <c>loadProject</c> does; a host that offers the <c>project.load</c> capability sets it.</summary>
        internal Func<LoadProjectRequest, Task<LoadProjectResponse>>? LoadProject { get; init; }

        /// <summary>Gets what <c>scanReader</c> does; a host that offers the <c>reader.scan</c> capability sets it.</summary>
        internal Func<Task<ScanReaderResponse>>? ScanReader { get; init; }
    }

    /// <summary>
    /// The server side of one connection: what <see cref="IBridgeService"/> does for a client. Every call passes the same
    /// guard first. It refuses a call before <c>hello</c>, refuses a method whose capability the host does not offer, turns a
    /// <see cref="BridgeFault"/> into the error of the wire, and turns anything unexpected into an
    /// <see cref="BridgeErrorCodes.InternalError"/> that carries an id for the log and no text of the exception.
    /// </summary>
    internal sealed class BridgeService : IBridgeService
    {
        private const int MaxClientNameLength = 128;
        private const int MaxClientVersionLength = 64;
        private const int MaxRequestedEvents = 32;

        private readonly BridgeServiceOptions _options;
        private readonly string _sessionId;
        private HashSet<string> _enabledEvents = new HashSet<string>(StringComparer.Ordinal);
        private volatile bool _helloDone;

        /// <summary>Creates the service of one connection.</summary>
        /// <param name="options">What the host offers.</param>
        /// <param name="sessionId">The id of the connection.</param>
        internal BridgeService(BridgeServiceOptions options, string sessionId)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        }

        /// <summary>Gets whether the client has made the handshake.</summary>
        internal bool HelloDone => _helloDone;

        /// <summary>Says whether the client asked for an event and the host offers it.</summary>
        /// <param name="name">The wire name of the event.</param>
        /// <returns><see langword="true"/> when the event is to be sent to this connection.</returns>
        internal bool IsEventEnabled(string name) => Volatile.Read(ref _enabledEvents).Contains(name);

        /// <inheritdoc />
        public Task<HelloResponse> Hello(HelloRequest request) => Run(BridgeMethods.Hello, () => Task.FromResult(HelloCore(request)));

        /// <inheritdoc />
        public Task<StatusResponse> GetStatus(GetStatusRequest request) =>
            Run(BridgeMethods.GetStatus, () => (_options.GetStatus ?? throw new NotSupportedException())());

        /// <inheritdoc />
        public Task<LoadProjectResponse> LoadProject(LoadProjectRequest request) =>
            Run(BridgeMethods.LoadProject, () => (_options.LoadProject ?? throw new NotSupportedException())(request));

        /// <inheritdoc />
        public Task<StartRunResponse> StartRun(StartRunRequest request) => Run<StartRunResponse>(BridgeMethods.StartRun, () => throw new NotSupportedException());

        /// <inheritdoc />
        public Task<CancelRunResponse> CancelRun(CancelRunRequest request) => Run<CancelRunResponse>(BridgeMethods.CancelRun, () => throw new NotSupportedException());

        /// <inheritdoc />
        public Task<GetRunResultResponse> GetRunResult(GetRunResultRequest request) => Run<GetRunResultResponse>(BridgeMethods.GetRunResult, () => throw new NotSupportedException());

        /// <inheritdoc />
        public Task<ScanReaderResponse> ScanReader(ScanReaderRequest request) =>
            Run(BridgeMethods.ScanReader, () => (_options.ScanReader ?? throw new NotSupportedException())());

        private async Task<T> Run<T>(string wireName, Func<Task<T>> body)
        {
            try
            {
                if (wireName != BridgeMethods.Hello && !_helloDone)
                {
                    throw BridgeFault.HandshakeRequired();
                }

                var capability = BridgeCatalog.MethodCapabilities[wireName];

                if (!_options.Capabilities.Contains(capability))
                {
                    throw BridgeFault.CapabilityNotSupported(capability);
                }

                if (wireName != BridgeMethods.Hello && wireName != BridgeMethods.GetStatus && !_options.IsStartupComplete())
                {
                    throw BridgeFault.NotReady("startup");
                }

                return await body().ConfigureAwait(false);
            }
            catch (BridgeFault fault)
            {
                throw fault.ToRpc();
            }
            catch (Exception e) when (e is not LocalRpcException && e is not OperationCanceledException)
            {
                // The type, never the text: what a reader, a project or a library throws may hold a path, a name or a value.
                var errorId = Guid.NewGuid().ToString("N")[..12];
                Log.ForContext<BridgeService>().Error("A call of the external control bridge failed ({Method}, {ExceptionType}, errorId {ErrorId})", wireName, e.GetType().FullName, errorId);

                throw new LocalRpcException(BridgeErrorKinds.InternalError)
                {
                    ErrorCode = BridgeErrorCodes.InternalError,
                    ErrorData = new BridgeErrorData { Kind = BridgeErrorKinds.InternalError, ErrorId = errorId },
                };
            }
        }

        private HelloResponse HelloCore(HelloRequest request)
        {
            Validate(request);

            // Negotiation is by major version only: the host speaks one, and takes it if the client's range holds it.
            if (request.ProtocolMajorMin > BridgeProtocol.Major || request.ProtocolMajorMax < BridgeProtocol.Major)
            {
                throw BridgeFault.IncompatibleProtocol(BridgeProtocol.Major, BridgeProtocol.Major);
            }

            var capabilities = BridgeCatalog.AllCapabilities.Where(_options.Capabilities.Contains).ToList();
            var enabled = request.Events
                .Where(name => BridgeCatalog.EventCapabilities.TryGetValue(name, out var capability) && _options.Capabilities.Contains(capability))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            Volatile.Write(ref _enabledEvents, new HashSet<string>(enabled, StringComparer.Ordinal));
            _helloDone = true;

            return new HelloResponse
            {
                Product = BridgeProtocol.Product,
                AppVersion = _options.AppVersion,
                ContractsVersion = typeof(IBridgeService).Assembly.GetName().Version?.ToString() ?? string.Empty,
                InstanceId = _options.InstanceId,
                Protocol = new ProtocolVersionDto { Major = BridgeProtocol.Major, Minor = BridgeProtocol.Minor },
                SessionId = _sessionId,
                Capabilities = capabilities,
                Events = enabled,
                Limits = new BridgeLimitsDto
                {
                    MaxMessageBytes = BridgeProtocol.MaxMessageBytes,
                    MaxVariables = BridgeProtocol.MaxVariables,
                    MaxVariableNameLength = BridgeProtocol.MaxVariableNameLength,
                    MaxVariableValueLength = BridgeProtocol.MaxVariableValueLength,
                    MaxPathLength = BridgeProtocol.MaxPathLength,
                    MaxRequestIdLength = BridgeProtocol.MaxRequestIdLength,
                    ResultHistory = BridgeProtocol.ResultHistory,
                    RequestIdHistory = BridgeProtocol.RequestIdHistory,
                    MaxWaitMs = BridgeProtocol.MaxWaitMs,
                },
            };
        }

        // The name of the field only, never its value: the answer must not echo what a client sent.
        private static void Validate(HelloRequest request)
        {
            if (request == null)
            {
                throw BridgeFault.InvalidRequest("request");
            }

            if (request.ClientName == null || request.ClientName.Length > MaxClientNameLength)
            {
                throw BridgeFault.InvalidRequest("clientName");
            }

            if (request.ClientVersion == null || request.ClientVersion.Length > MaxClientVersionLength)
            {
                throw BridgeFault.InvalidRequest("clientVersion");
            }

            if (request.ProtocolMajorMin < 0 || request.ProtocolMajorMax < request.ProtocolMajorMin)
            {
                throw BridgeFault.InvalidRequest("protocolMajorMin");
            }

            if (request.Events == null || request.Events.Count > MaxRequestedEvents || request.Events.Any(name => name == null || name.Length > MaxClientVersionLength))
            {
                throw BridgeFault.InvalidRequest("events");
            }
        }
    }
}
