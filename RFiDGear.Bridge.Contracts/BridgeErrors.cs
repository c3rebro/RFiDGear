using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>
    /// The numeric codes of the errors the host answers. They are application codes of JSON-RPC, outside its reserved range
    /// (-32768 to -32000), and are carried in <c>error.code</c>. The structured part is in <c>error.data</c>
    /// (<see cref="BridgeErrorData"/>). An error never carries the text of an exception.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class BridgeErrorCodes
    {
        /// <summary>Another method was called before <c>hello</c>.</summary>
        public const int HandshakeRequired = 1001;

        /// <summary>No protocol major version is supported by both sides.</summary>
        public const int IncompatibleProtocol = 1002;

        /// <summary>
        /// The method belongs to a capability this host does not offer. A host that does not know the method at all (an older
        /// version) answers the JSON-RPC code -32601 (method not found); a client treats both as this error.
        /// </summary>
        public const int CapabilityNotSupported = 1003;

        /// <summary>
        /// A request is over a limit or breaks a rule; <c>details.field</c> names the field, never its value. A request that is
        /// not even readable (an unknown member, a value of the wrong type) is answered by the JSON-RPC library itself with its
        /// code -32602 (invalid params) before any code of RFiDGear runs; a client treats both as this error.
        /// </summary>
        public const int InvalidRequest = 1004;

        /// <summary>RFiDGear is still starting; only <c>hello</c> and <c>getStatus</c> work yet.</summary>
        public const int NotReady = 1005;

        /// <summary>A run, a project load, an edit or a reader scan is in progress; <c>details.activity</c> says which.</summary>
        public const int Busy = 1006;

        /// <summary>Automatic mode is on, started by the user in the main window.</summary>
        public const int AutoModeActive = 1007;

        /// <summary>The project file does not exist.</summary>
        public const int ProjectNotFound = 1008;

        /// <summary>The file is not a project this version can open (also a project made by a newer version).</summary>
        public const int ProjectUnsupportedFormat = 1009;

        /// <summary>The project could not be loaded for another reason.</summary>
        public const int ProjectLoadFailed = 1010;

        /// <summary>No project is loaded, or it holds no task.</summary>
        public const int NoProjectLoaded = 1011;

        /// <summary>The loaded project is not the one the client expected.</summary>
        public const int ProjectMismatch = 1012;

        /// <summary>The request id was used before with another request.</summary>
        public const int RequestIdConflict = 1013;

        /// <summary>The main window did not respond in time; nothing was started.</summary>
        public const int UiUnresponsive = 1014;

        /// <summary>RFiDGear is closing.</summary>
        public const int ShuttingDown = 1015;

        /// <summary>Something went wrong inside RFiDGear; <see cref="BridgeErrorData.ErrorId"/> matches an entry of its log.</summary>
        public const int InternalError = 1016;
    }

    /// <summary>The short names of the errors, carried in <see cref="BridgeErrorData.Kind"/>; they match <see cref="BridgeErrorCodes"/> one to one.</summary>
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = WireConstants.Justification)]
    public static class BridgeErrorKinds
    {
        /// <summary>See <see cref="BridgeErrorCodes.HandshakeRequired"/>.</summary>
        public const string HandshakeRequired = "handshakeRequired";

        /// <summary>See <see cref="BridgeErrorCodes.IncompatibleProtocol"/>.</summary>
        public const string IncompatibleProtocol = "incompatibleProtocol";

        /// <summary>See <see cref="BridgeErrorCodes.CapabilityNotSupported"/>.</summary>
        public const string CapabilityNotSupported = "capabilityNotSupported";

        /// <summary>See <see cref="BridgeErrorCodes.InvalidRequest"/>.</summary>
        public const string InvalidRequest = "invalidRequest";

        /// <summary>See <see cref="BridgeErrorCodes.NotReady"/>.</summary>
        public const string NotReady = "notReady";

        /// <summary>See <see cref="BridgeErrorCodes.Busy"/>.</summary>
        public const string Busy = "busy";

        /// <summary>See <see cref="BridgeErrorCodes.AutoModeActive"/>.</summary>
        public const string AutoModeActive = "autoModeActive";

        /// <summary>See <see cref="BridgeErrorCodes.ProjectNotFound"/>.</summary>
        public const string ProjectNotFound = "projectNotFound";

        /// <summary>See <see cref="BridgeErrorCodes.ProjectUnsupportedFormat"/>.</summary>
        public const string ProjectUnsupportedFormat = "projectUnsupportedFormat";

        /// <summary>See <see cref="BridgeErrorCodes.ProjectLoadFailed"/>.</summary>
        public const string ProjectLoadFailed = "projectLoadFailed";

        /// <summary>See <see cref="BridgeErrorCodes.NoProjectLoaded"/>.</summary>
        public const string NoProjectLoaded = "noProjectLoaded";

        /// <summary>See <see cref="BridgeErrorCodes.ProjectMismatch"/>.</summary>
        public const string ProjectMismatch = "projectMismatch";

        /// <summary>See <see cref="BridgeErrorCodes.RequestIdConflict"/>.</summary>
        public const string RequestIdConflict = "requestIdConflict";

        /// <summary>See <see cref="BridgeErrorCodes.UiUnresponsive"/>.</summary>
        public const string UiUnresponsive = "uiUnresponsive";

        /// <summary>See <see cref="BridgeErrorCodes.ShuttingDown"/>.</summary>
        public const string ShuttingDown = "shuttingDown";

        /// <summary>See <see cref="BridgeErrorCodes.InternalError"/>.</summary>
        public const string InternalError = "internalError";
    }

    /// <summary>The structured part of an error (<c>error.data</c>).</summary>
    public sealed record BridgeErrorData
    {
        /// <summary>Gets the short name of the error, one of <see cref="BridgeErrorKinds"/>.</summary>
        [JsonPropertyName("kind")]
        public string Kind { get; init; } = string.Empty;

        /// <summary>Gets an id that also appears in the log of RFiDGear, for <see cref="BridgeErrorCodes.InternalError"/>.</summary>
        [JsonPropertyName("errorId")]
        public string? ErrorId { get; init; }

        /// <summary>
        /// Gets whether the request is known not to have touched a card. It is set on every error of <c>startRun</c>:
        /// <c>true</c> means nothing ran and the request may be repeated with a new request id; <c>false</c> or absent means
        /// it may have, and the client must ask for the result with the same request id instead of sending it again.
        /// </summary>
        [JsonPropertyName("neverStarted")]
        public bool? NeverStarted { get; init; }

        /// <summary>Gets further facts, never a value the client sent and never the text of an exception.</summary>
        [JsonPropertyName("details")]
        public IReadOnlyDictionary<string, string>? Details { get; init; }
    }
}
