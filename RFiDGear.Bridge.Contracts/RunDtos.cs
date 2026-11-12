using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>A variable handed to a run (a report path, a job number, ...). A name that appears twice is an error.</summary>
    public sealed record VariablePair
    {
        /// <summary>Gets the name; case does not matter.</summary>
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the value. It is not written to the log of RFiDGear.</summary>
        [JsonPropertyName("value")]
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>The parameters of <c>startRun</c>.</summary>
    public sealed record StartRunRequest
    {
        /// <summary>
        /// Gets the id that identifies this request (<see cref="BridgeProtocol.IsValidRequestId"/>). Sending the same id again
        /// with the same content never starts a second run; it returns the run the first request started. A client that did
        /// not get an answer must ask with <c>getRunResult</c> and this id, and must not send a new <c>startRun</c>.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; init; } = string.Empty;

        /// <summary>Gets the variables of the run.</summary>
        [JsonPropertyName("variables")]
        public IReadOnlyList<VariablePair> Variables { get; init; } = Array.Empty<VariablePair>();

        /// <summary>Gets the path of the project the client expects to be loaded; a different one refuses the run before anything is touched.</summary>
        [JsonPropertyName("expectProjectPath")]
        public string? ExpectProjectPath { get; init; }

        /// <summary>Gets the number of tasks the client expects the project to have; a different one refuses the run before anything is touched.</summary>
        [JsonPropertyName("expectTaskCount")]
        public int? ExpectTaskCount { get; init; }
    }

    /// <summary>The answer to <c>startRun</c>: the run was admitted, or it is the run an earlier request with the same id started.</summary>
    public sealed record StartRunResponse
    {
        /// <summary>Gets the request id, as sent.</summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; init; } = string.Empty;

        /// <summary>Gets the id of the run; it is also the id in the result and in the log of RFiDGear.</summary>
        [JsonPropertyName("runId")]
        public string RunId { get; init; } = string.Empty;

        /// <summary>Gets the state of the run, <see cref="RunStates.Running"/> or, for a repeated request, possibly <see cref="RunStates.Completed"/>.</summary>
        [JsonPropertyName("state")]
        public string State { get; init; } = RunStates.Running;

        /// <summary>Gets whether the request id had been used before, so that this answer describes an earlier request.</summary>
        [JsonPropertyName("duplicate")]
        public bool Duplicate { get; init; }
    }

    /// <summary>The parameters of <c>cancelRun</c>.</summary>
    public sealed record CancelRunRequest
    {
        /// <summary>Gets the request id of the run to cancel.</summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; init; } = string.Empty;
    }

    /// <summary>
    /// The answer to <c>cancelRun</c>. A run is cancelled between two tasks: the task that is running finishes first, and a
    /// run whose last task had already started can still end as a success.
    /// </summary>
    public sealed record CancelRunResponse
    {
        /// <summary>Gets the state of the run now, one of <see cref="RunStates"/>.</summary>
        [JsonPropertyName("state")]
        public string State { get; init; } = RunStates.Unknown;

        /// <summary>Gets whether the host has taken the request to cancel; <c>false</c> for a run that had ended or is not known.</summary>
        [JsonPropertyName("cancelRequested")]
        public bool CancelRequested { get; init; }
    }

    /// <summary>The parameters of <c>getRunResult</c>: the request id or the run id, and how long to wait.</summary>
    public sealed record GetRunResultRequest
    {
        /// <summary>Gets the request id of the run. Exactly one of <see cref="RequestId"/> and <see cref="RunId"/> is given.</summary>
        [JsonPropertyName("requestId")]
        public string? RequestId { get; init; }

        /// <summary>Gets the run id. Exactly one of <see cref="RequestId"/> and <see cref="RunId"/> is given.</summary>
        [JsonPropertyName("runId")]
        public string? RunId { get; init; }

        /// <summary>Gets how long to wait for a run that is still going on, in milliseconds, at most <see cref="BridgeProtocol.MaxWaitMs"/>; 0 answers at once.</summary>
        [JsonPropertyName("waitMs")]
        public int WaitMs { get; init; }
    }

    /// <summary>The result of a task.</summary>
    public sealed record TaskOutcomeDto
    {
        /// <summary>Gets the position of the task in the result, from 0. Tasks the run reached come first, in the order it reached them, then the ones it did not reach.</summary>
        [JsonPropertyName("position")]
        public int Position { get; init; }

        /// <summary>Gets the task index as written in the project. Free text; do not parse it.</summary>
        [JsonPropertyName("projectTaskIndex")]
        public string ProjectTaskIndex { get; init; } = string.Empty;

        /// <summary>Gets the kind of task.</summary>
        [JsonPropertyName("taskTypeName")]
        public string TaskTypeName { get; init; } = string.Empty;

        /// <summary><c>true</c> when the task ran and succeeded, <c>false</c> when it ran and failed, nothing when it did not run or its result is not known.</summary>
        [JsonPropertyName("isSuccess")]
        public bool? IsSuccess { get; init; }

        /// <summary>Gets the normalized error code of the task (<c>NoError</c>, <c>AuthFailure</c>, <c>KeyUnavailable</c>, ...). It never holds key material.</summary>
        [JsonPropertyName("normalizedErrorCode")]
        public string NormalizedErrorCode { get; init; } = string.Empty;

        /// <summary>Gets why the task did not run, when it did not.</summary>
        [JsonPropertyName("skipReason")]
        public string? SkipReason { get; init; }
    }

    /// <summary>
    /// How a run ended. A client trusts a result only when <see cref="TaskOutcomes"/> has <see cref="TotalTasks"/> entries.
    /// A positive end of the task list is not the same as a card that holds what was meant: the client checks the card itself.
    /// </summary>
    public sealed record RunResultDto
    {
        /// <summary>Gets the request id, when the run was started over the bridge.</summary>
        [JsonPropertyName("requestId")]
        public string? RequestId { get; init; }

        /// <summary>Gets the id of the run.</summary>
        [JsonPropertyName("runId")]
        public string RunId { get; init; } = string.Empty;

        /// <summary>Gets who started the run, one of <see cref="RunSources"/>.</summary>
        [JsonPropertyName("source")]
        public string Source { get; init; } = RunSources.Bridge;

        /// <summary>Gets how the run ended, one of <see cref="RunStatuses"/>.</summary>
        [JsonPropertyName("status")]
        public string Status { get; init; } = RunStatuses.Failure;

        /// <summary>Gets why the run did not end the normal way, one of <see cref="FailureReasons"/>.</summary>
        [JsonPropertyName("failureReason")]
        public string FailureReason { get; init; } = FailureReasons.None;

        /// <summary>Gets how many tasks the project had.</summary>
        [JsonPropertyName("totalTasks")]
        public int TotalTasks { get; init; }

        /// <summary>Gets how many tasks failed.</summary>
        [JsonPropertyName("failedTasks")]
        public int FailedTasks { get; init; }

        /// <summary>Gets when the run started.</summary>
        [JsonPropertyName("startedAt")]
        public DateTimeOffset StartedAt { get; init; }

        /// <summary>Gets when the run ended.</summary>
        [JsonPropertyName("completedAt")]
        public DateTimeOffset CompletedAt { get; init; }

        /// <summary>Gets the UID of the card the run worked on, when it got as far as reading one.</summary>
        [JsonPropertyName("cardUid")]
        public string? CardUid { get; init; }

        /// <summary>Gets the sequence number of this result among the results of this RFiDGear process, from 1.</summary>
        [JsonPropertyName("resultSeq")]
        public long ResultSeq { get; init; }

        /// <summary>Gets the results of the tasks, in order.</summary>
        [JsonPropertyName("taskOutcomes")]
        public IReadOnlyList<TaskOutcomeDto> TaskOutcomes { get; init; } = Array.Empty<TaskOutcomeDto>();
    }

    /// <summary>The answer to <c>getRunResult</c>. An id the host does not know is an answer (<see cref="RunStates.Unknown"/>), not an error.</summary>
    public sealed record GetRunResultResponse
    {
        /// <summary>Gets the state of the run, one of <see cref="RunStates"/>.</summary>
        [JsonPropertyName("state")]
        public string State { get; init; } = RunStates.Unknown;

        /// <summary>Gets the result, when <see cref="State"/> is <see cref="RunStates.Completed"/>.</summary>
        [JsonPropertyName("result")]
        public RunResultDto? Result { get; init; }
    }
}
