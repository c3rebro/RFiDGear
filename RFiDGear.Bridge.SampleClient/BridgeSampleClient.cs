#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.RegularExpressions;
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
        private const int MaxApprovedRunsRemembered = 64;

        private static readonly TimeSpan DefaultStartTimeout = TimeSpan.FromSeconds(30);

        // The form of a variable a project can name for an approved UID: a dollar sign and a name, as the Check UID task reads it.
        private static readonly Regex UidVariablePattern = new Regex(@"^\$[A-Za-z0-9_]{1,64}\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        private readonly NamedPipeClientStream _pipe;
        private readonly JsonRpc _rpc;
        private readonly ConcurrentDictionary<string, byte> _ownRequests = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // The runs that were admitted for a card this client approved in automatic mode, as the notices said (the latest only).
        private readonly object _approvedRunsGate = new object();
        private readonly HashSet<string> _approvedRuns = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _approvedRunOrder = new Queue<string>();
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

        /// <summary>Gets a task that ends when the connection ends, for whatever reason (RFiDGear closed, the pipe broke, this client was disposed).</summary>
        public Task Completion => _rpc.Completion;

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

        /// <summary>
        /// Gets or sets whether this client hands out keys only for the runs it started itself (a request of <see cref="StartedByThisClient"/>).
        /// It is the client's own check and does not depend on the host: a host that is older than <see cref="BridgeCapabilities.KeysContext"/>
        /// sends no <c>requestId</c>, so every request is refused, which is the safe way to be wrong. <see cref="RegisterKeyProviderAsync"/> sets it.
        /// </summary>
        public bool ServeOwnRunsOnly { get; set; }

        /// <summary>
        /// Gets or sets how long a request for a key of a run of automatic mode waits for the notice that says the run was admitted for a
        /// card this client approved (<c>cardPresentationEnded</c> with the reason <c>approved</c>). RFiDGear sends the notice before the
        /// first task of the run, but on a path of its own, so the request for a key can be here first. It is used only with
        /// <see cref="ServeOwnRunsOnly"/>, and a request for a run that no notice names is refused when the time is up.
        /// </summary>
        public TimeSpan ApprovedRunWait { get; set; } = TimeSpan.FromSeconds(2);

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
        /// <param name="events">
        /// The wire names of the events to receive (<see cref="BridgeEvents"/>); only these are sent. The notice that ends a card
        /// (<see cref="BridgeEvents.CardPresentationEnded"/>) is always added: the client's own check of whose run it is learns the runs of the
        /// cards it approved from it. A RFiDGear that does not offer it ignores the name, and it is only ever sent in a mode that waits for approval.
        /// </param>
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
                var patience = connectTimeout ?? TimeSpan.FromSeconds(10);
                await pipe.ConnectAsync(patience, cancellationToken).ConfigureAwait(false);
                var client = new BridgeSampleClient(pipe);

                try
                {
                    client.Hello = await client.Service.Hello(new HelloRequest
                    {
                        ClientName = "RFiDGear.Bridge.SampleClient",
                        ClientVersion = "1.0",
                        ProtocolMajorMin = BridgeProtocol.Major,
                        ProtocolMajorMax = BridgeProtocol.Major,
                        Events = WithTheNoticesTheClientNeeds(events),
                    }).WaitAsync(patience, cancellationToken).ConfigureAwait(false);

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
        /// Says which revision a run of the project that <c>loadProject</c> answered about should expect. A RFiDGear that does not
        /// offer <see cref="BridgeCapabilities.ProjectRevision"/> has none to give, and a run then has only the path and the task count.
        /// One that offers it but gave no revision for this project (it could not make one) leaves a client without the protection
        /// it relies on: the client does not go on without it, because a run that is started anyway would run whatever is loaded.
        /// </summary>
        /// <param name="loaded">What <c>loadProject</c> answered.</param>
        /// <returns>The revision to pass as <c>expectProjectRevision</c>, or <see langword="null"/> when RFiDGear does not offer revisions.</returns>
        /// <exception cref="InvalidOperationException">RFiDGear offers revisions and gave none for this project.</exception>
        public string? RevisionToExpect(LoadProjectResponse loaded)
        {
            ArgumentNullException.ThrowIfNull(loaded);

            if (!Offers(BridgeCapabilities.ProjectRevision))
            {
                return null;
            }

            return string.IsNullOrEmpty(loaded.Revision)
                ? throw new InvalidOperationException("RFiDGear offers project.revision but gave no revision for the project it loaded: nothing was started.")
                : loaded.Revision;
        }

        /// <summary>
        /// Starts one run of the loaded project and waits for its result. The rule it keeps: <b>a request that may have reached
        /// RFiDGear is never repeated as a new request</b>. If the answer does not come (a timeout, a lost connection, an error
        /// that does not say "never started"), the client asks about the same request id instead, which only looks and never
        /// starts anything. (It does not send the request again: RFiDGear recognises an id only while it still knows it, and an id
        /// that is older than the last 1024, or that a restart without <c>execution.journal</c> made it forget, would start another
        /// run and write the card again.)
        /// </summary>
        /// <param name="requestId">The id of this job. Make it from the job (an order number), or write it down before the call: it is what you ask with later.</param>
        /// <param name="variables">The variables of the run (names as the project writes them, with the leading dollar sign).</param>
        /// <param name="expectProjectPath">The project the job is for (the path <c>loadProject</c> answered with), or <see langword="null"/> to run whichever is loaded. A project that was replaced since is refused, and nothing is started.</param>
        /// <param name="expectTaskCount">The number of tasks the project has, or <see langword="null"/>.</param>
        /// <param name="expectProjectRevision">
        /// The revision of the project as <c>loadProject</c> or <c>getStatus</c> answered it, or <see langword="null"/> (also when
        /// <see cref="BridgeCapabilities.ProjectRevision"/> is not offered). A project in another state, a task that was changed in the
        /// window after you looked, is refused, and nothing is started. Path and task count cannot tell that.
        /// </param>
        /// <param name="cancellationToken">Stops waiting; it does not stop the run (use <c>cancelRun</c> for that).</param>
        /// <returns>How it ended.</returns>
        public async Task<RunOutcome> RunOnceAsync(
            string requestId,
            IReadOnlyDictionary<string, string> variables,
            string? expectProjectPath = null,
            int? expectTaskCount = null,
            string? expectProjectRevision = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(variables);

            // Written down before the call: the host may ask for a key the moment it starts, before the answer to startRun is here.
            _ownRequests[requestId] = 0;

            var request = new StartRunRequest
            {
                RequestId = requestId,
                Variables = variables.Select(pair => new VariablePair { Name = pair.Key, Value = pair.Value }).ToList(),
                ExpectProjectPath = expectProjectPath,
                ExpectTaskCount = expectTaskCount,
                ExpectProjectRevision = expectProjectRevision,
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
        /// One card, one approval, one run: the reference flow for a job that writes a card. It looks at the reader, asks the
        /// caller whether THIS card may be written (the caller checks its job, its profile and its list of serial numbers; this is the
        /// part that is not RFiDGear's), and only then starts the run with the approved UID in a variable that the project's Check UID
        /// task compares with the card that the run reads itself. A card that was swapped in between is not the approved one, and
        /// the tasks that depend on the check never start. Afterwards it compares the card that the result says the run read with the
        /// approved one. It never starts a run for more than one card, and never repeats one (see <see cref="RunOnceAsync"/>).
        /// </summary>
        /// <param name="requestId">The id of this job and this card. Make it from the job; it is the only way to ask about the run later, and an id is never reused for another card.</param>
        /// <param name="uidVariable">The variable the project reads the approved UID from, with its dollar sign (for example <c>$EXPECTEDUID</c>).</param>
        /// <param name="approve">The program's own decision about the card. Called once, with nothing started yet; <see langword="false"/> ends it.</param>
        /// <param name="variables">The other variables of the run; it must not hold <paramref name="uidVariable"/>.</param>
        /// <param name="expectProjectPath">The project the job is for (see <see cref="RunOnceAsync"/>).</param>
        /// <param name="expectTaskCount">The number of tasks of the project.</param>
        /// <param name="expectProjectRevision">The revision that was approved with the project (see <see cref="RevisionToExpect"/>).</param>
        /// <param name="cancellationToken">Stops waiting; it does not stop a run that was started.</param>
        /// <returns>What came of it. Look at <see cref="ApprovedRunOutcome.IsConfirmed"/> before you record the card as written.</returns>
        /// <exception cref="ArgumentException">The variable is not of the form <c>$NAME</c>, or the caller set it as well.</exception>
        public async Task<ApprovedRunOutcome> RunApprovedCardAsync(
            string requestId,
            string uidVariable,
            Func<ApprovedCard, CancellationToken, Task<bool>> approve,
            IReadOnlyDictionary<string, string>? variables = null,
            string? expectProjectPath = null,
            int? expectTaskCount = null,
            string? expectProjectRevision = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(requestId);
            ArgumentNullException.ThrowIfNull(approve);

            if (uidVariable == null || !UidVariablePattern.IsMatch(uidVariable))
            {
                throw new ArgumentException("The variable of the approved UID is a dollar sign and a name, such as $EXPECTEDUID.", nameof(uidVariable));
            }

            if (variables != null && variables.Keys.Any(name => string.Equals(name, uidVariable, StringComparison.OrdinalIgnoreCase)))
            {
                // The bridge takes the names of variables without regard to case, so $expecteduid is the same variable.
                throw new ArgumentException("The variable of the approved UID is set by this method and by nobody else.", nameof(variables));
            }

            ScanReaderResponse scan;

            try
            {
                scan = await Service.ScanReader(new ScanReaderRequest()).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (RemoteInvocationException failure)
            {
                return Stopped(ApprovedRunKind.ReaderNotReady, "The reader could not be looked at (" + (ErrorDataOf(failure)?.Kind ?? "error") + "). Nothing was started.");
            }

            var reader = scan.Reader;

            if (reader.State != ReaderStates.Ready)
            {
                return Stopped(ApprovedRunKind.ReaderNotReady, "The reader is " + reader.State + ". Nothing was started.");
            }

            if (!reader.HasCard || reader.Credentials.Count == 0)
            {
                return Stopped(ApprovedRunKind.NoCard, "There is no card on the reader. Nothing was started.");
            }

            if (reader.Credentials.Count != 1 || !reader.Credentials[0].IsPrimary || string.IsNullOrEmpty(reader.Credentials[0].Uid))
            {
                return Stopped(ApprovedRunKind.AmbiguousCard, "More than one card is on the reader, or RFiDGear cannot say which one is the card. One card at a time is approved. Nothing was started.");
            }

            var credential = reader.Credentials[0];
            var card = new ApprovedCard(credential.Uid, credential.TypeCode, credential.DisplayName);

            if (!await approve(card, cancellationToken).ConfigureAwait(false))
            {
                return Stopped(ApprovedRunKind.Declined, "The card was not approved. Nothing was started.", card.Uid);
            }

            // An approval that was given while the caller was cancelling (a prompt that does not look at the token answers after Ctrl+C)
            // is not a reason to start a card-writing run.
            cancellationToken.ThrowIfCancellationRequested();

            var runVariables = new Dictionary<string, string>(variables ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            {
                [uidVariable] = card.Uid,
            };
            var run = await RunOnceAsync(requestId, runVariables, expectProjectPath, expectTaskCount, expectProjectRevision, cancellationToken).ConfigureAwait(false);

            // What the run says about itself, against what was approved: the card it read. The project is not compared here:
            // RFiDGear refused the run before it started if the project was not the approved state.
            var cardMatches = run.Result != null && string.Equals(run.Result.CardUid, card.Uid, StringComparison.OrdinalIgnoreCase);

            return new ApprovedRunOutcome(ApprovedRunKind.Ran, card.Uid, run, cardMatches, run.Detail);
        }

        private static ApprovedRunOutcome Stopped(ApprovedRunKind kind, string detail, string? approvedUid = null) =>
            new ApprovedRunOutcome(kind, approvedUid, null, false, detail);

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
        /// <param name="ownRunsOnly">
        /// Whether keys are to be asked for the runs of this client only. RFiDGear then answers "not authorized" itself for every
        /// other run and does not call this client, and the client also refuses them (<see cref="ServeOwnRunsOnly"/>). It needs
        /// <see cref="BridgeCapabilities.KeysContext"/>; without it, or when RFiDGear does not say that it applies the restriction,
        /// this method throws instead of registering a client that would believe it is protected.
        /// </param>
        /// <returns>The id of the provider to name in projects, and the time limits.</returns>
        public async Task<RegisterKeyProviderResponse> RegisterKeyProviderAsync(bool ownRunsOnly = false)
        {
            if (!Offers(BridgeCapabilities.KeysProvider))
            {
                throw new NotSupportedException("This RFiDGear does not offer the capability " + BridgeCapabilities.KeysProvider + ".");
            }

            if (ownRunsOnly && !Offers(BridgeCapabilities.KeysContext))
            {
                throw new NotSupportedException("This RFiDGear does not offer the capability " + BridgeCapabilities.KeysContext + ", so it cannot tell which runs are this client's.");
            }

            // Before the registration, so that the client never serves a key without the check it asked for.
            ServeOwnRunsOnly = ownRunsOnly;

            var registered = await Service.RegisterKeyProvider(new RegisterKeyProviderRequest { OwnRunsOnly = ownRunsOnly }).ConfigureAwait(false);

            // The answer says what is in force. A restriction that was asked for and is not applied is not a registration to go on with.
            return ownRunsOnly && !registered.OwnRunsOnly
                ? throw new InvalidOperationException("RFiDGear did not apply the restriction to the runs of this client; do not serve keys on this connection.")
                : registered;
        }

        /// <summary>
        /// Says whether a request for a key is for a run that this client started or decided. A run it started over the bridge: RFiDGear
        /// says the run is a client's (<c>source</c> is <see cref="RunSources.Bridge"/>) and the request id is one that this client sent
        /// with <c>startRun</c>. The run of a card it approved in automatic mode: <c>source</c> is <see cref="RunSources.AutoMode"/>
        /// and the run id is one that a <c>cardPresentationEnded</c> with the reason <c>approved</c> named (that notice goes to the
        /// connection that started the mode, so it is this client's approval). A request without them (an extension, the window, a card
        /// nobody approved, an older RFiDGear) is not.
        /// </summary>
        /// <param name="request">The request of the host.</param>
        /// <returns><see langword="true"/> for a run this client started or decided.</returns>
        public bool StartedByThisClient(ResolveKeyRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.Equals(request.Source, RunSources.AutoMode, StringComparison.Ordinal))
            {
                return request.RunId != null && IsApprovedRun(request.RunId);
            }

            return string.Equals(request.Source, RunSources.Bridge, StringComparison.Ordinal)
                && request.RequestId != null
                && _ownRequests.ContainsKey(request.RequestId);
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
        public void RunStarted(RunStartedEvent e) => Raise(e);

        /// <inheritdoc />
        public void RunCompleted(RunCompletedEvent e) => Raise(e);

        /// <inheritdoc />
        public void RunningChanged(RunningChangedEvent e) => Raise(e);

        /// <inheritdoc />
        public void ReaderChanged(ReaderChangedEvent e) => Raise(e);

        /// <inheritdoc />
        public void ProjectChanged(ProjectChangedEvent e) => Raise(e);

        /// <inheritdoc />
        public void ShuttingDown(ShuttingDownEvent e) => Raise(e);

        /// <inheritdoc />
        public void AutoModeChanged(AutoModeChangedEvent e) => Raise(e);

        /// <inheritdoc />
        public void AutoRunCompleted(AutoRunCompletedEvent e) => Raise(e);

        /// <inheritdoc />
        public void CardPresented(CardPresentedEvent e) => Raise(e);

        /// <inheritdoc />
        public void CardPresentationEnded(CardPresentationEndedEvent e)
        {
            // The notice goes to the connection that started the mode, so a run it names was admitted for a card this client approved.
            if (string.Equals(e.Reason, PresentationEndReasons.Approved, StringComparison.Ordinal) && !string.IsNullOrEmpty(e.RunId))
            {
                NoteApprovedRun(e.RunId);
            }

            Raise(e);
        }

        /// <inheritdoc />
        public async Task<ResolveKeyResponse> ResolveKey(ResolveKeyRequest request, CancellationToken cancellationToken)
        {
            var vault = KeyVault;

            if (vault == null || (ServeOwnRunsOnly && !await StartedByThisClientAsync(request, cancellationToken).ConfigureAwait(false)))
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

        // Like StartedByThisClient, but a request for a run of automatic mode waits a little for the notice that names the run: it is
        // sent before the first task, on its own path, and the request for a key can be here first.
        private async Task<bool> StartedByThisClientAsync(ResolveKeyRequest request, CancellationToken cancellationToken)
        {
            if (StartedByThisClient(request))
            {
                return true;
            }

            if (!string.Equals(request.Source, RunSources.AutoMode, StringComparison.Ordinal) || request.RunId == null)
            {
                return false;
            }

            var until = DateTime.UtcNow + ApprovedRunWait;

            while (DateTime.UtcNow < until)
            {
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);

                if (IsApprovedRun(request.RunId))
                {
                    return true;
                }
            }

            return false;
        }

        // The events the caller asked for, and the one the client's own check cannot do without.
        private static IReadOnlyList<string> WithTheNoticesTheClientNeeds(IReadOnlyList<string>? events) =>
            (events ?? Array.Empty<string>()).Concat(new[] { BridgeEvents.CardPresentationEnded }).Distinct(StringComparer.Ordinal).ToList();

        private void NoteApprovedRun(string runId)
        {
            lock (_approvedRunsGate)
            {
                if (!_approvedRuns.Add(runId))
                {
                    return;
                }

                _approvedRunOrder.Enqueue(runId);

                while (_approvedRunOrder.Count > MaxApprovedRunsRemembered)
                {
                    _approvedRuns.Remove(_approvedRunOrder.Dequeue());
                }
            }
        }

        private bool IsApprovedRun(string runId)
        {
            lock (_approvedRunsGate)
            {
                return _approvedRuns.Contains(runId);
            }
        }

        private void Raise(BridgeEvent e)
        {
            var handler = EventReceived;

            if (handler != null)
            {
                handler(e);
            }
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
