using System.Threading.Tasks;

namespace RFiDGear.Bridge
{
    /// <summary>
    /// The calls a client makes on RFiDGear over the pipe. Every method takes one request object and returns one response
    /// object, so a field can be added later without changing a signature. The wire names (<see cref="BridgeMethods"/>) are
    /// the camel-case form of the method names, which is why the names carry no <c>Async</c> suffix.
    /// <para>
    /// Rules that hold for every method:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="Hello"/> comes first; anything else before it fails with <see cref="BridgeErrorCodes.HandshakeRequired"/>.</description></item>
    /// <item><description>A method whose capability the host does not offer fails with <see cref="BridgeErrorCodes.CapabilityNotSupported"/>. Nothing is ever a silent no-op.</description></item>
    /// <item><description>Before RFiDGear has finished starting, everything but <see cref="Hello"/> and <see cref="GetStatus"/> fails with <see cref="BridgeErrorCodes.NotReady"/>.</description></item>
    /// <item><description>Nothing is retried by the host, and nothing may be retried by a client without the rules of <see cref="StartRun"/>.</description></item>
    /// </list>
    /// </summary>
    public interface IBridgeService
    {
        /// <summary>
        /// Agrees on a protocol version and tells the client what this RFiDGear can do. The client decides what it may call from
        /// <see cref="HelloResponse.Capabilities"/>, not from the application version.
        /// </summary>
        /// <param name="request">Who the client is, which protocol major versions it speaks, which events it wants.</param>
        /// <returns>The agreed version, the capabilities, the events that are on, and the limits.</returns>
        [Capability(BridgeCapabilities.Session)]
        Task<HelloResponse> Hello(HelloRequest request);

        /// <summary>Tells what is possible right now: whether start-up is over, the loaded project, whether a run is going on, what the reader sees.</summary>
        /// <param name="request">No parameters yet.</param>
        /// <returns>The status.</returns>
        [Capability(BridgeCapabilities.Status)]
        Task<StatusResponse> GetStatus(GetStatusRequest request);

        /// <summary>
        /// Loads a project. It replaces the project in the main window; changes there that were not saved are lost. It is
        /// refused while a run, a load or a scan is in progress (<see cref="BridgeErrorCodes.Busy"/>) and while the user has
        /// automatic mode on (<see cref="BridgeErrorCodes.AutoModeActive"/>).
        /// </summary>
        /// <param name="request">The path of the project.</param>
        /// <returns>The path and the number of tasks.</returns>
        [Capability(BridgeCapabilities.ProjectLoad)]
        Task<LoadProjectResponse> LoadProject(LoadProjectRequest request);

        /// <summary>
        /// Starts a run of the loaded project's task list once, and returns as soon as the run is admitted, not when it ends.
        /// The result comes as the event <see cref="BridgeEvents.RunCompleted"/> and from <see cref="GetRunResult"/>.
        /// <para>
        /// A card operation is never repeated by RFiDGear. A client that gets no answer (a timeout, a lost connection) must not
        /// send a new request: it asks with <see cref="GetRunResult"/> and the same request id. The host answers
        /// <see cref="RunStates.Unknown"/> for an id it does not know, which is either a request it never started or one older
        /// than the history it keeps; the client then checks the card before it decides anything. An error of this method
        /// carries <see cref="BridgeErrorData.NeverStarted"/>; only <c>true</c> allows a new request id.
        /// </para>
        /// </summary>
        /// <param name="request">The request id, the variables and what the client expects to be loaded.</param>
        /// <returns>The id of the run and its state.</returns>
        [Capability(BridgeCapabilities.ExecutionRun)]
        Task<StartRunResponse> StartRun(StartRunRequest request);

        /// <summary>
        /// Asks a run to stop. The run ends between two tasks; the final status is whatever the run reports, and may still be a success.
        /// </summary>
        /// <param name="request">The request id of the run.</param>
        /// <returns>The state of the run and whether the request was taken.</returns>
        [Capability(BridgeCapabilities.ExecutionCancel)]
        Task<CancelRunResponse> CancelRun(CancelRunRequest request);

        /// <summary>
        /// Reads the result of a run, or waits for it for a while. It is the way to find out what became of a request after a
        /// timeout or a lost connection.
        /// </summary>
        /// <param name="request">The request id or the run id, and how long to wait.</param>
        /// <returns>The state of the run and, once it ended, its result.</returns>
        [Capability(BridgeCapabilities.ExecutionResultLookup)]
        Task<GetRunResultResponse> GetRunResult(GetRunResultRequest request);

        /// <summary>Reads the reader now. A reader that is in use, or has no card, is an answer and not an error.</summary>
        /// <param name="request">No parameters yet.</param>
        /// <returns>What the reader sees.</returns>
        [Capability(BridgeCapabilities.ReaderScan)]
        Task<ScanReaderResponse> ScanReader(ScanReaderRequest request);
    }
}
