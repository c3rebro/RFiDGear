using System.Collections.ObjectModel;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks.Interfaces;

namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// Provides validation helpers for task indices and execute condition references.
    /// </summary>
    internal static class TaskIndexValidation
    {
        /// <summary>
        /// Validates that the task index is numeric, non-negative, and unique within the collection.
        /// </summary>
        /// <param name="taskIndex">The task index string to validate.</param>
        /// <param name="taskCollection">The collection of existing tasks.</param>
        /// <param name="currentTask">The task instance being edited, if any.</param>
        /// <param name="errorMessage">The validation error message, if validation fails.</param>
        /// <returns><see langword="true"/> when the task index is valid; otherwise <see langword="false"/>.</returns>
        public static bool TryValidateTaskIndex(string taskIndex, ObservableCollection<object> taskCollection, object currentTask, out string errorMessage)
        {
            if (!int.TryParse(taskIndex, out var parsedIndex) || parsedIndex < 0)
            {
                errorMessage = "Task index must be a non-negative number.";
                return false;
            }

            if (taskCollection != null)
            {
                foreach (var task in taskCollection)
                {
                    if (ReferenceEquals(task, currentTask))
                    {
                        continue;
                    }

                    if (TryGetTaskIndex(task, out var existingIndex) && existingIndex == parsedIndex)
                    {
                        errorMessage = "Task index already exists in the collection.";
                        return false;
                    }
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Validates that the execute condition index points to an existing task when a condition is selected.
        /// </summary>
        /// <param name="executeConditionTaskIndex">The task index referenced by the execute condition.</param>
        /// <param name="executeConditionErrorLevel">The execute condition error level.</param>
        /// <param name="taskCollection">The collection of existing tasks.</param>
        /// <param name="errorMessage">The validation error message, if validation fails.</param>
        /// <returns><see langword="true"/> when the execute condition reference is valid; otherwise <see langword="false"/>.</returns>
        public static bool TryValidateExecuteConditionIndex(string executeConditionTaskIndex, ERROR executeConditionErrorLevel, ObservableCollection<object> taskCollection, out string errorMessage)
        {
            if (executeConditionErrorLevel == ERROR.Empty)
            {
                errorMessage = null;
                return true;
            }

            if (!int.TryParse(executeConditionTaskIndex, out var parsedIndex) || parsedIndex < 0)
            {
                errorMessage = "Execute condition task index must be a non-negative number.";
                return false;
            }

            if (taskCollection != null)
            {
                foreach (var task in taskCollection)
                {
                    if (TryGetTaskIndex(task, out var existingIndex) && existingIndex == parsedIndex)
                    {
                        errorMessage = null;
                        return true;
                    }
                }
            }

            errorMessage = "Execute condition task index must reference an existing task.";
            return false;
        }

        private static bool TryGetTaskIndex(object task, out int taskIndex)
        {
            switch (task)
            {
                case IGenericTask genericTask when int.TryParse(genericTask.CurrentTaskIndex, out taskIndex):
                    return true;
                case MifareUltralightSetupViewModel ultralightTask when int.TryParse(ultralightTask.CurrentTaskIndex, out taskIndex):
                    return true;
                default:
                    taskIndex = -1;
                    return false;
            }
        }
    }
}
