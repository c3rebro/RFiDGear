using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>The parameters of <c>startAutoMode</c>.</summary>
    public sealed record StartAutoModeRequest
    {
        /// <summary>
        /// Gets whether automatic mode is switched off when this client's connection ends. It is on by default: a mode that goes
        /// on processing cards with nobody to look at the results is not what a client that has gone away wants. A client that
        /// reconnects often, and wants the mode to go on meanwhile, sets it to <see langword="false"/> and reads what it missed
        /// with <c>listRunResults</c>.
        /// </summary>
        [JsonPropertyName("stopOnDisconnect")]
        public bool StopOnDisconnect { get; init; } = true;
    }

    /// <summary>The answer to <c>startAutoMode</c>.</summary>
    public sealed record StartAutoModeResponse
    {
        /// <summary>Gets whether automatic mode is on now. Always <see langword="true"/> in an answer; a mode that cannot be switched on is an error.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }

        /// <summary>Gets whether it had been switched on by this bridge before, so that this call changed nothing but the setting of <see cref="StopOnDisconnect"/>.</summary>
        [JsonPropertyName("alreadyEnabled")]
        public bool AlreadyEnabled { get; init; }

        /// <summary>Gets whether the mode is switched off when the connection ends, as agreed now.</summary>
        [JsonPropertyName("stopOnDisconnect")]
        public bool StopOnDisconnect { get; init; }
    }

    /// <summary>The parameters of <c>stopAutoMode</c>. There are none yet; the type exists so that an optional field can be added later.</summary>
    public sealed record StopAutoModeRequest;

    /// <summary>The answer to <c>stopAutoMode</c>. A run of the mode that is going on is not interrupted: it ends, its result is reported, and nothing follows.</summary>
    public sealed record StopAutoModeResponse
    {
        /// <summary>Gets whether the mode was on. Stopping a mode that is off is an answer, not an error.</summary>
        [JsonPropertyName("wasEnabled")]
        public bool WasEnabled { get; init; }
    }

    /// <summary>What <c>getStatus</c> says about automatic mode.</summary>
    public sealed record AutoModeStatusDto
    {
        /// <summary>Gets whether the mode is on.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }

        /// <summary>Gets who switched it on while it is on, one of <see cref="RunSources"/> (<c>bridge</c>, or <c>other</c> for the user, the start-up argument or an extension); nothing while it is off.</summary>
        [JsonPropertyName("startedBy")]
        public string? StartedBy { get; init; }

        /// <summary>Gets whether the mode is switched off when the connection of the client that switched it on ends; only for a mode that this bridge switched on.</summary>
        [JsonPropertyName("stopOnDisconnect")]
        public bool StopOnDisconnect { get; init; }

        /// <summary>Gets why the mode last stopped, one of <see cref="AutoModeStopReasons"/>.</summary>
        [JsonPropertyName("lastStopReason")]
        public string LastStopReason { get; init; } = AutoModeStopReasons.None;
    }

    /// <summary>The parameters of <c>listRunResults</c>.</summary>
    public sealed record ListRunResultsRequest
    {
        /// <summary>Gets the number of the last result the client has; the answer starts after it. 0 for all that are held.</summary>
        [JsonPropertyName("afterResultSeq")]
        public long AfterResultSeq { get; init; }

        /// <summary>Gets how many results at most; 0 for as many as are held (the last <see cref="BridgeProtocol.ResultHistory"/>).</summary>
        [JsonPropertyName("limit")]
        public int Limit { get; init; }
    }

    /// <summary>
    /// The answer to <c>listRunResults</c>. The numbers of the results (<see cref="RunResultDto.ResultSeq"/>) run through all runs
    /// of one RFiDGear process, those of clients and those of automatic mode, in the order in which they ended. They start
    /// again at 1 when RFiDGear is restarted, which the client sees from <see cref="InstanceId"/>.
    /// </summary>
    public sealed record ListRunResultsResponse
    {
        /// <summary>Gets the id of the RFiDGear process (see <see cref="HelloResponse.InstanceId"/>); the numbers belong to it.</summary>
        [JsonPropertyName("instanceId")]
        public string InstanceId { get; init; } = string.Empty;

        /// <summary>Gets the results after the number asked for, oldest first.</summary>
        [JsonPropertyName("results")]
        public IReadOnlyList<RunResultDto> Results { get; init; } = Array.Empty<RunResultDto>();

        /// <summary>Gets whether results between the number asked for and the first of <see cref="Results"/> were dropped from the history, so that the client has missed them for good.</summary>
        [JsonPropertyName("gap")]
        public bool Gap { get; init; }

        /// <summary>Gets the number of the latest result, 0 when there is none; more than <see cref="Results"/> ends with when a limit cut the list.</summary>
        [JsonPropertyName("lastResultSeq")]
        public long LastResultSeq { get; init; }
    }
}
