using System;
using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>
    /// What every event carries. <see cref="Seq"/> grows with every event of one connection, in the order they were sent;
    /// it is for ordering and for noticing a gap, which is legal after events of one kind were merged. Events are hints:
    /// after a reconnect nothing is replayed, and the truth is <c>getStatus</c> and <c>getRunResult</c>.
    /// </summary>
    public abstract record BridgeEvent
    {
        // The envelope is written first: System.Text.Json would write the members of the derived type before it, and a
        // trace of the pipe is much easier to read with the sequence number at the front.

        /// <summary>Gets the sequence number of the event on this connection, from 1.</summary>
        [JsonPropertyName("seq")]
        [JsonPropertyOrder(-3)]
        public long Seq { get; init; }

        /// <summary>Gets the id of the connection (<see cref="HelloResponse.SessionId"/>).</summary>
        [JsonPropertyName("sessionId")]
        [JsonPropertyOrder(-2)]
        public string SessionId { get; init; } = string.Empty;

        /// <summary>Gets when the host sent the event.</summary>
        [JsonPropertyName("at")]
        [JsonPropertyOrder(-1)]
        public DateTimeOffset At { get; init; }
    }

    /// <summary>A task run, from anywhere, started or ended.</summary>
    public sealed record RunningChangedEvent : BridgeEvent
    {
        /// <summary>Gets whether a task run is going on now.</summary>
        [JsonPropertyName("isRunning")]
        public bool IsRunning { get; init; }

        /// <summary>Gets the request id of the run, when this connection's host started it over the bridge.</summary>
        [JsonPropertyName("ownRequestId")]
        public string? OwnRequestId { get; init; }

        /// <summary>Gets the id of that run.</summary>
        [JsonPropertyName("ownRunId")]
        public string? OwnRunId { get; init; }
    }

    /// <summary>A run started over the bridge was admitted.</summary>
    public sealed record RunStartedEvent : BridgeEvent
    {
        /// <summary>Gets the request id.</summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; init; } = string.Empty;

        /// <summary>Gets the id of the run.</summary>
        [JsonPropertyName("runId")]
        public string RunId { get; init; } = string.Empty;

        /// <summary>Gets when the run started.</summary>
        [JsonPropertyName("startedAt")]
        public DateTimeOffset StartedAt { get; init; }
    }

    /// <summary>A run started over the bridge ended. It is sent once per run; a client that missed it reads the result with <c>getRunResult</c>.</summary>
    public sealed record RunCompletedEvent : BridgeEvent
    {
        /// <summary>Gets the result of the run.</summary>
        [JsonPropertyName("result")]
        public RunResultDto Result { get; init; } = new RunResultDto();
    }

    /// <summary>The reader or the card on it changed.</summary>
    public sealed record ReaderChangedEvent : BridgeEvent
    {
        /// <summary>Gets what the reader sees now.</summary>
        [JsonPropertyName("reader")]
        public ReaderDto Reader { get; init; } = new ReaderDto();
    }

    /// <summary>The loaded project changed.</summary>
    public sealed record ProjectChangedEvent : BridgeEvent
    {
        /// <summary>Gets the full path of the project now, or nothing for a project that was never saved.</summary>
        [JsonPropertyName("path")]
        public string? Path { get; init; }

        /// <summary>Gets how many tasks the project has now.</summary>
        [JsonPropertyName("taskCount")]
        public int TaskCount { get; init; }
    }

    /// <summary>RFiDGear is closing. This is sent on a best-effort basis: a client that sees the connection end without it must treat a run in flight as having an unknown outcome.</summary>
    public sealed record ShuttingDownEvent : BridgeEvent
    {
        /// <summary>Gets why RFiDGear closes, one of <see cref="ShutdownReasons"/>.</summary>
        [JsonPropertyName("reason")]
        public string Reason { get; init; } = ShutdownReasons.ProcessExit;

        /// <summary>Gets whether a run started over the bridge is in flight.</summary>
        [JsonPropertyName("runInFlight")]
        public bool RunInFlight { get; init; }
    }
}
