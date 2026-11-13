using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>The parameters of <c>getStatus</c>. There are none yet; the type exists so that an optional field can be added later.</summary>
    public sealed record GetStatusRequest;

    /// <summary>A card that is on the reader.</summary>
    public sealed record CredentialDto
    {
        /// <summary>Gets the UID of the card, as upper-case hexadecimal digits.</summary>
        [JsonPropertyName("uid")]
        public string Uid { get; init; } = string.Empty;

        /// <summary>Gets the type code of the card.</summary>
        [JsonPropertyName("typeCode")]
        public int TypeCode { get; init; }

        /// <summary>Gets a name for the type of card, for people. Free text; do not parse it.</summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Gets whether this is the card the reader reports first.</summary>
        [JsonPropertyName("isPrimary")]
        public bool IsPrimary { get; init; }
    }

    /// <summary>What the reader sees.</summary>
    public sealed record ReaderDto
    {
        /// <summary>Gets the name of the reader, for people. Free text; do not parse it.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        /// <summary>Gets the state of the reader, one of <see cref="ReaderStates"/>.</summary>
        [JsonPropertyName("state")]
        public string State { get; init; } = ReaderStates.Unknown;

        /// <summary>Gets whether a card is on the reader.</summary>
        [JsonPropertyName("hasCard")]
        public bool HasCard { get; init; }

        /// <summary>Gets the cards on the reader; empty unless <see cref="State"/> is <see cref="ReaderStates.Ready"/>.</summary>
        [JsonPropertyName("credentials")]
        public IReadOnlyList<CredentialDto> Credentials { get; init; } = Array.Empty<CredentialDto>();
    }

    /// <summary>The project that is loaded.</summary>
    public sealed record ProjectStatusDto
    {
        /// <summary>Gets whether a project is loaded.</summary>
        [JsonPropertyName("loaded")]
        public bool Loaded { get; init; }

        /// <summary>Gets the full path of the loaded project, or nothing for a project that was never saved.</summary>
        [JsonPropertyName("path")]
        public string? Path { get; init; }

        /// <summary>Gets how many tasks the project has.</summary>
        [JsonPropertyName("taskCount")]
        public int TaskCount { get; init; }
    }

    /// <summary>A run that is going on.</summary>
    public sealed record ActiveRunDto
    {
        /// <summary>Gets the request id, when the run was started over the bridge.</summary>
        [JsonPropertyName("requestId")]
        public string? RequestId { get; init; }

        /// <summary>Gets the run id, when it is known.</summary>
        [JsonPropertyName("runId")]
        public string? RunId { get; init; }

        /// <summary>Gets who started the run, one of <see cref="RunSources"/>.</summary>
        [JsonPropertyName("source")]
        public string Source { get; init; } = RunSources.Other;

        /// <summary>Gets when the run started, when it is known.</summary>
        [JsonPropertyName("startedAt")]
        public DateTimeOffset? StartedAt { get; init; }
    }

    /// <summary>
    /// What is possible right now. <c>hello</c> says what this RFiDGear can do; this says what it can do at the moment. Both
    /// answer without waiting for a run to end.
    /// </summary>
    public sealed record StatusResponse
    {
        /// <summary>Gets the id of this RFiDGear process (see <see cref="HelloResponse.InstanceId"/>).</summary>
        [JsonPropertyName("instanceId")]
        public string InstanceId { get; init; } = string.Empty;

        /// <summary>Gets whether RFiDGear has finished starting (the last project is loaded, the reader is being watched). Until then only <c>hello</c> and <c>getStatus</c> work.</summary>
        [JsonPropertyName("startupComplete")]
        public bool StartupComplete { get; init; }

        /// <summary>Gets whether the reader has not been looked at yet, so that a missing card may only mean "not yet".</summary>
        [JsonPropertyName("readerInitializing")]
        public bool ReaderInitializing { get; init; }

        /// <summary>Gets whether the main window answered in time. A window that does not answer (a dialog, a stalled update prompt) delays everything that needs it.</summary>
        [JsonPropertyName("uiResponsive")]
        public bool UiResponsive { get; init; }

        /// <summary>Gets whether some values are old because the main window did not answer in time.</summary>
        [JsonPropertyName("stale")]
        public bool Stale { get; init; }

        /// <summary>Gets the loaded project.</summary>
        [JsonPropertyName("project")]
        public ProjectStatusDto Project { get; init; } = new ProjectStatusDto();

        /// <summary>Gets whether any task run is going on, from anywhere.</summary>
        [JsonPropertyName("isRunning")]
        public bool IsRunning { get; init; }

        /// <summary>Gets the run that is going on, when there is one the host knows more about.</summary>
        [JsonPropertyName("activeRun")]
        public ActiveRunDto? ActiveRun { get; init; }

        /// <summary>Gets what the reader sees.</summary>
        [JsonPropertyName("reader")]
        public ReaderDto Reader { get; init; } = new ReaderDto();

        /// <summary>Gets the sequence number of the latest result the host holds, 0 when there is none. It lets a client see after a reconnect whether it missed one.</summary>
        [JsonPropertyName("lastResultSeq")]
        public long LastResultSeq { get; init; }

        /// <summary>Gets what automatic mode is doing; nothing when this RFiDGear does not offer the <see cref="BridgeCapabilities.AutoMode"/> capability.</summary>
        [JsonPropertyName("autoMode")]
        public AutoModeStatusDto? AutoMode { get; init; }
    }
}
