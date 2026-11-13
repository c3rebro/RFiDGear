#nullable enable
using System.Linq;

using RFiDGear.Bridge;
using RFiDGear.Contracts;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// Turns what the host knows (<c>RFiDGear.Contracts</c>) into the types of the wire (<c>RFiDGear.Bridge</c>). The wire
    /// never uses the types of the host directly: a member that the host gains does not reach a client until it is mapped on
    /// purpose, and a value the mapper does not know becomes <c>unknown</c> instead of an error.
    /// </summary>
    internal static class BridgeMapper
    {
        /// <summary>
        /// Reads the picture the reader shows now and maps it. A reader provider that fails when it is asked (it is somebody
        /// else's code) must not take the notice or the status down with it: the answer is a reader in the state
        /// <see cref="ReaderStates.Unknown"/>, and the log has the type of the failure.
        /// </summary>
        /// <param name="reader">The reader service of the host.</param>
        /// <returns>The reader as the wire has it.</returns>
        internal static ReaderDto ReadReader(IReaderHost reader)
        {
            System.ArgumentNullException.ThrowIfNull(reader);

            try
            {
                return ToDto(reader.CurrentSnapshot);
            }
            catch (System.Exception e)
            {
                Serilog.Log.ForContext(typeof(BridgeMapper)).Debug("External control: the reader could not be read ({ExceptionType})", e.GetType().FullName);

                return new ReaderDto { State = ReaderStates.Unknown };
            }
        }

        /// <summary>Maps the picture of the reader.</summary>
        /// <param name="snapshot">What the reader showed last.</param>
        /// <returns>The reader as the wire has it.</returns>
        internal static ReaderDto ToDto(ReaderSnapshot snapshot)
        {
            System.ArgumentNullException.ThrowIfNull(snapshot);

            return new ReaderDto
            {
                Name = string.IsNullOrEmpty(snapshot.ReaderName) ? null : snapshot.ReaderName,
                State = ToWire(snapshot.State),
                HasCard = snapshot.HasCard,
                Credentials = snapshot.Credentials
                    .Select(credential => new CredentialDto
                    {
                        Uid = credential.Uid,
                        TypeCode = credential.TypeCode,
                        DisplayName = credential.DisplayName,
                        IsPrimary = credential.IsPrimary,
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// Maps the result of a run. Every task has an outcome in a result that is complete; a host that reports another number
        /// of outcomes than tasks must not pass for a complete result, so it becomes a failure whose reason is
        /// <see cref="FailureReasons.BridgeFault"/>. A value of the host that this protocol does not know becomes
        /// <see cref="FailureReasons.Unknown"/>, not an exception.
        /// </summary>
        /// <param name="result">What the host reported.</param>
        /// <param name="runId">The id the bridge gave the run.</param>
        /// <param name="requestId">The request id of the client that started it.</param>
        /// <param name="resultSeq">The number of the result in the order in which results were completed.</param>
        /// <param name="source">Who started the run, one of <see cref="RunSources"/>.</param>
        /// <returns>The result as the wire has it.</returns>
        internal static RunResultDto ToDto(ExecResult result, string runId, string? requestId, long resultSeq, string source = RunSources.Bridge)
        {
            System.ArgumentNullException.ThrowIfNull(result);

            var outcomes = (result.TaskOutcomes ?? System.Array.Empty<TaskOutcome>())
                .Select(outcome => new TaskOutcomeDto
                {
                    Position = outcome.Position,
                    ProjectTaskIndex = outcome.ProjectTaskIndex ?? string.Empty,
                    TaskTypeName = outcome.TaskTypeName ?? string.Empty,
                    IsSuccess = outcome.IsSuccess,
                    NormalizedErrorCode = outcome.NormalizedErrorCode ?? string.Empty,
                    SkipReason = outcome.SkipReason,
                })
                .ToList();

            var reason = ToWire(result.FailureReason);
            var status = StatusOf(result.Outcome, result.FailureReason, ref reason);

            if (outcomes.Count != result.TotalTasks)
            {
                status = RunStatuses.Failure;
                reason = FailureReasons.BridgeFault;
            }

            return new RunResultDto
            {
                RequestId = requestId,
                RunId = runId,
                Source = source,
                Status = status,
                FailureReason = reason,
                TotalTasks = result.TotalTasks,
                FailedTasks = result.FailedTasks,
                StartedAt = result.StartedAt,
                CompletedAt = result.CompletedAt,
                CardUid = result.CardUid,
                ResultSeq = resultSeq,
                TaskOutcomes = outcomes,
            };
        }

        /// <summary>
        /// Makes the result of a run the host admitted but never reported on (its task faulted or was cancelled before it gave a
        /// result): the card may have been touched, so it is a failure whose reason says that the bridge could not finish the run,
        /// with no outcomes, and never a clean result.
        /// </summary>
        /// <param name="runId">The id the bridge gave the run.</param>
        /// <param name="requestId">The request id of the client that started it.</param>
        /// <param name="resultSeq">The number of the result.</param>
        /// <param name="startedAt">When the run was admitted.</param>
        /// <returns>The result as the wire has it.</returns>
        internal static RunResultDto ToFaultedDto(string runId, string? requestId, long resultSeq, System.DateTimeOffset startedAt) => new RunResultDto
        {
            RequestId = requestId,
            RunId = runId,
            Source = RunSources.Bridge,
            Status = RunStatuses.Failure,
            FailureReason = FailureReasons.BridgeFault,
            TotalTasks = 0,
            FailedTasks = 0,
            StartedAt = startedAt,
            CompletedAt = System.DateTimeOffset.UtcNow,
            ResultSeq = resultSeq,
            TaskOutcomes = System.Array.Empty<TaskOutcomeDto>(),
        };

        /// <summary>Maps the reason a run did not end the normal way to a constant of <see cref="FailureReasons"/>.</summary>
        /// <param name="reason">The reason.</param>
        /// <returns>The wire name; <see cref="FailureReasons.Unknown"/> for a reason the mapper does not know.</returns>
        internal static string ToWire(ExecutionFailureReason reason) => reason switch
        {
            ExecutionFailureReason.None => FailureReasons.None,
            ExecutionFailureReason.Cancelled => FailureReasons.Cancelled,
            ExecutionFailureReason.TimedOut => FailureReasons.TimedOut,
            ExecutionFailureReason.Faulted => FailureReasons.Faulted,
            _ => FailureReasons.Unknown,
        };

        // One word for how the run ended. A failure that was a cancellation or a timeout says so; an outcome this protocol does
        // not know is a failure whose reason says that (unless the host already gave a reason).
        private static string StatusOf(ExecutionOutcome outcome, ExecutionFailureReason failure, ref string reason)
        {
            switch (outcome)
            {
                case ExecutionOutcome.Success:
                    return RunStatuses.Success;
                case ExecutionOutcome.CompletedWithIntentionalFailures:
                    return RunStatuses.CompletedWithIntentionalFailures;
                case ExecutionOutcome.Failure:
                    return failure switch
                    {
                        ExecutionFailureReason.Cancelled => RunStatuses.Cancelled,
                        ExecutionFailureReason.TimedOut => RunStatuses.TimedOut,
                        _ => RunStatuses.Failure,
                    };
                default:
                    if (reason == FailureReasons.None)
                    {
                        reason = FailureReasons.Unknown;
                    }

                    return RunStatuses.Failure;
            }
        }

        /// <summary>Maps the reason automatic mode last stopped to a constant of <see cref="AutoModeStopReasons"/>.</summary>
        /// <param name="reason">The reason.</param>
        /// <returns>The wire name; <see cref="AutoModeStopReasons.Unknown"/> for a reason the mapper does not know.</returns>
        internal static string ToWire(AutoModeStopReason reason) => reason switch
        {
            AutoModeStopReason.None => AutoModeStopReasons.None,
            AutoModeStopReason.StopRequested => AutoModeStopReasons.StopRequested,
            AutoModeStopReason.ReaderLost => AutoModeStopReasons.ReaderLost,
            AutoModeStopReason.ReaderChanged => AutoModeStopReasons.ReaderChanged,
            AutoModeStopReason.RunAborted => AutoModeStopReasons.RunAborted,
            AutoModeStopReason.RunTimedOut => AutoModeStopReasons.RunTimedOut,
            _ => AutoModeStopReasons.Unknown,
        };

        /// <summary>Maps the state of a reader scan to a constant of <see cref="ReaderStates"/>.</summary>
        /// <param name="state">The state.</param>
        /// <returns>The wire name; <see cref="ReaderStates.Unknown"/> for a state the mapper does not know.</returns>
        internal static string ToWire(ReaderScanState state) => state switch
        {
            ReaderScanState.NoReader => ReaderStates.NoReader,
            ReaderScanState.Ready => ReaderStates.Ready,
            ReaderScanState.ReadFailed => ReaderStates.ReadFailed,
            ReaderScanState.Busy => ReaderStates.Busy,
            _ => ReaderStates.Unknown,
        };
    }
}
