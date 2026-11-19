using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Mvvm;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>How a task row is drawn.</summary>
    internal enum TaskRowKind
    {
        /// <summary>The task ran and succeeded.</summary>
        Success,

        /// <summary>The task ran and failed.</summary>
        Failure,

        /// <summary>The task is a check that ran and answered "no": the answer of the check, not a failure.</summary>
        AnsweredNo,

        /// <summary>The task did not run: its condition was not met, the run never got to it, or it was unavailable.</summary>
        Skipped,

        /// <summary>The task ran but reported neither success nor failure.</summary>
        Unknown
    }

    /// <summary>
    /// One line of the result list. Its texts are worked out from the outcome each time they are read, so a change of
    /// language only needs <see cref="Refresh"/>.
    /// </summary>
    internal sealed class TaskOutcomeRowViewModel : ObservableBase
    {
        /// <summary>The name of the type of the tasks that hold both the logic checks and the start of a program.</summary>
        private const string CommonTaskTypeName = "CommonTaskViewModel";

        // The error codes arrive as text (the contract has no enum for them); these are the two the host calls an answer.
        private const string IsNotTrueCode = "IsNotTrue";
        private const string IsNotFalseCode = "IsNotFalse";

        private readonly TaskOutcome _outcome;
        private readonly LocalizedStrings _strings;
        private readonly ExecutionOutcome _runOutcome;

        /// <param name="outcome">What the host reported for the task.</param>
        /// <param name="strings">The page's texts.</param>
        /// <param name="runOutcome">How the run that the task belongs to ended; a run that failed keeps what it cannot tell apart as failed.</param>
        public TaskOutcomeRowViewModel(TaskOutcome outcome, LocalizedStrings strings, ExecutionOutcome runOutcome = ExecutionOutcome.Failure)
        {
            _outcome = outcome;
            _strings = strings;
            _runOutcome = runOutcome;
        }

        /// <summary>
        /// Whether a task ran and gave the answer "no" of a check (<c>IsNotTrue</c> or <c>IsNotFalse</c>), which is its result and
        /// no failure: the host does not count it against the run, and a task that depends on it takes over from there. The host
        /// knows a program that failed to start by the type of the task and never accepts it; a result carries only the name of the
        /// view model, and the view model that starts programs also holds the logic checks. So in a run that failed, such an
        /// answer of that view model stays a failure (it may be the program), and in a run that did not fail it cannot be one,
        /// because the host would have failed the run for it.
        /// </summary>
        /// <param name="outcome">What the host reported for the task.</param>
        /// <param name="runOutcome">How the run that the task belongs to ended.</param>
        /// <returns><c>true</c> if the task is a check that answered "no".</returns>
        internal static bool IsAnsweredNo(TaskOutcome outcome, ExecutionOutcome runOutcome)
        {
            if (outcome.SkipReason != null || outcome.IsSuccess != false)
            {
                return false;
            }

            if (outcome.NormalizedErrorCode != IsNotTrueCode && outcome.NormalizedErrorCode != IsNotFalseCode)
            {
                return false;
            }

            return runOutcome != ExecutionOutcome.Failure || outcome.TaskTypeName != CommonTaskTypeName;
        }

        /// <summary>The task's place in the project, starting at 1.</summary>
        public int Position => _outcome.Position + 1;

        /// <summary>The task index as stored in the project.</summary>
        public string TaskIndex => _outcome.ProjectTaskIndex;

        /// <summary>The kind of task, for example the name of its type.</summary>
        public string TaskType => _outcome.TaskTypeName;

        /// <summary>The normalized error code the task ended with.</summary>
        public string ErrorCode => _outcome.NormalizedErrorCode;

        /// <summary>How the row is drawn.</summary>
        public TaskRowKind Kind
        {
            get
            {
                if (_outcome.SkipReason != null)
                {
                    return TaskRowKind.Skipped;
                }

                if (IsAnsweredNo(_outcome, _runOutcome))
                {
                    return TaskRowKind.AnsweredNo;
                }

                return _outcome.IsSuccess switch
                {
                    true => TaskRowKind.Success,
                    false => TaskRowKind.Failure,
                    _ => TaskRowKind.Unknown
                };
            }
        }

        /// <summary>The result column: why the task was skipped, or whether it succeeded.</summary>
        public string ResultText
        {
            get
            {
                if (_outcome.SkipReason != null)
                {
                    return SkipText(_outcome.SkipReason);
                }

                if (IsAnsweredNo(_outcome, _runOutcome))
                {
                    return _strings[ExtensionStrings.Keys.RowAnsweredNo];
                }

                return _outcome.IsSuccess switch
                {
                    true => _strings[ExtensionStrings.Keys.RowOk],
                    false => _strings[ExtensionStrings.Keys.RowFailed],
                    _ => _strings[ExtensionStrings.Keys.RowUnknown]
                };
            }
        }

        /// <summary>Tells the list to read the language-dependent texts again.</summary>
        public void Refresh() => OnPropertyChanged(nameof(ResultText));

        private string SkipText(string reason)
        {
            if (reason == TaskSkipReasons.NotReached)
            {
                return _strings[ExtensionStrings.Keys.RowNotReached];
            }

            if (reason == TaskSkipReasons.ExecuteConditionNotMet)
            {
                return _strings[ExtensionStrings.Keys.RowConditionNotMet];
            }

            if (reason == TaskSkipReasons.NotExecuted)
            {
                return _strings[ExtensionStrings.Keys.RowNotExecuted];
            }

            if (reason == TaskSkipReasons.TaskModelUnavailable)
            {
                return _strings[ExtensionStrings.Keys.RowTaskUnavailable];
            }

            // A reason this version does not know: show it as it is rather than hide that the task did not run.
            return reason;
        }
    }
}
