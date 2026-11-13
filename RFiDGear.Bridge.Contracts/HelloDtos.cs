using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>The first call on every connection: who the client is and which protocol versions and events it wants.</summary>
    public sealed record HelloRequest
    {
        /// <summary>Gets the name of the client application, for the log of RFiDGear.</summary>
        [JsonPropertyName("clientName")]
        public string ClientName { get; init; } = string.Empty;

        /// <summary>Gets the version of the client application, for the log of RFiDGear.</summary>
        [JsonPropertyName("clientVersion")]
        public string ClientVersion { get; init; } = string.Empty;

        /// <summary>Gets the lowest protocol major version the client can speak.</summary>
        [JsonPropertyName("protocolMajorMin")]
        public int ProtocolMajorMin { get; init; }

        /// <summary>Gets the highest protocol major version the client can speak.</summary>
        [JsonPropertyName("protocolMajorMax")]
        public int ProtocolMajorMax { get; init; }

        /// <summary>
        /// Gets the events (<see cref="BridgeEvents"/>) the client wants to receive. The host sends nothing else, so an event
        /// added later never reaches a client that has not asked for it; a name the host does not know is left out of the answer.
        /// </summary>
        [JsonPropertyName("events")]
        public IReadOnlyList<string> Events { get; init; } = Array.Empty<string>();
    }

    /// <summary>The version of the protocol that was agreed.</summary>
    public sealed record ProtocolVersionDto
    {
        /// <summary>Gets the major version; both sides speak this one.</summary>
        [JsonPropertyName("major")]
        public int Major { get; init; }

        /// <summary>Gets the minor version of the host; informational, the capabilities say what it can do.</summary>
        [JsonPropertyName("minor")]
        public int Minor { get; init; }
    }

    /// <summary>The limits of the host, so that a client can stay within them.</summary>
    public sealed record BridgeLimitsDto
    {
        /// <summary>Gets the longest message the host accepts, in bytes.</summary>
        [JsonPropertyName("maxMessageBytes")]
        public int MaxMessageBytes { get; init; }

        /// <summary>Gets the most variables a run may be given.</summary>
        [JsonPropertyName("maxVariables")]
        public int MaxVariables { get; init; }

        /// <summary>Gets the longest variable name, in characters.</summary>
        [JsonPropertyName("maxVariableNameLength")]
        public int MaxVariableNameLength { get; init; }

        /// <summary>Gets the longest variable value, in characters.</summary>
        [JsonPropertyName("maxVariableValueLength")]
        public int MaxVariableValueLength { get; init; }

        /// <summary>Gets the longest path, in characters.</summary>
        [JsonPropertyName("maxPathLength")]
        public int MaxPathLength { get; init; }

        /// <summary>Gets the longest request id, in characters.</summary>
        [JsonPropertyName("maxRequestIdLength")]
        public int MaxRequestIdLength { get; init; }

        /// <summary>Gets how many full results the host keeps for a later lookup.</summary>
        [JsonPropertyName("resultHistory")]
        public int ResultHistory { get; init; }

        /// <summary>Gets how many request ids the host remembers.</summary>
        [JsonPropertyName("requestIdHistory")]
        public int RequestIdHistory { get; init; }

        /// <summary>Gets the longest a lookup may wait for a result, in milliseconds.</summary>
        [JsonPropertyName("maxWaitMs")]
        public int MaxWaitMs { get; init; }
    }

    /// <summary>The answer to the handshake: who the host is, which version was agreed and what it can do.</summary>
    public sealed record HelloResponse
    {
        /// <summary>Gets the product name, always <see cref="BridgeProtocol.Product"/>.</summary>
        [JsonPropertyName("product")]
        public string Product { get; init; } = string.Empty;

        /// <summary>Gets the version of the RFiDGear application; informational. Decide by <see cref="Capabilities"/>, not by this.</summary>
        [JsonPropertyName("appVersion")]
        public string AppVersion { get; init; } = string.Empty;

        /// <summary>Gets the version of this contract assembly the host was built with.</summary>
        [JsonPropertyName("contractsVersion")]
        public string ContractsVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets an id of this RFiDGear process. It changes when RFiDGear is restarted, which is how a client tells "the host
        /// forgot this request" from "the host is a new process".
        /// </summary>
        [JsonPropertyName("instanceId")]
        public string InstanceId { get; init; } = string.Empty;

        /// <summary>Gets the agreed protocol version.</summary>
        [JsonPropertyName("protocol")]
        public ProtocolVersionDto Protocol { get; init; } = new ProtocolVersionDto();

        /// <summary>Gets an id of this connection, which also appears on every event.</summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; init; } = string.Empty;

        /// <summary>Gets what this host can do (<see cref="BridgeCapabilities"/>).</summary>
        [JsonPropertyName("capabilities")]
        public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();

        /// <summary>Gets the events that were switched on for this connection.</summary>
        [JsonPropertyName("events")]
        public IReadOnlyList<string> Events { get; init; } = Array.Empty<string>();

        /// <summary>Gets the limits of the host.</summary>
        [JsonPropertyName("limits")]
        public BridgeLimitsDto Limits { get; init; } = new BridgeLimitsDto();
    }
}
