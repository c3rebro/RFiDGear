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
        private readonly TaskOutcome _outcome;
        private readonly LocalizedStrings _strings;

        /// <param name="outcome">What the host reported for the task.</param>
        /// <param name="strings">The page's texts.</param>
        public TaskOutcomeRowViewModel(TaskOutcome outcome, LocalizedStrings strings)
        {
            _outcome = outcome;
            _strings = strings;
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
