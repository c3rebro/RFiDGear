using System.Collections.ObjectModel;

using RFiDGear.Infrastructure;
using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;
using Xunit;

namespace RFiDGear.Tests
{
    public class TaskIndexValidationTests
    {
        [Fact]
        public void TryValidateTaskIndex_RejectsNonNumericInput()
        {
            var tasks = new ObservableCollection<object>();

            var isValid = TaskIndexValidation.TryValidateTaskIndex("not-a-number", tasks, null, out var errorMessage);

            Assert.False(isValid);
            Assert.Contains("non-negative number", errorMessage);
        }

        [Fact]
        public void TryValidateTaskIndex_RejectsDuplicateIndex()
        {
            var existingTask = new FakeTask { CurrentTaskIndex = "1" };
            var tasks = new ObservableCollection<object> { existingTask };

            var isValid = TaskIndexValidation.TryValidateTaskIndex("1", tasks, null, out var errorMessage);

            Assert.False(isValid);
            Assert.Contains("already exists", errorMessage);
        }

        [Fact]
        public void TryValidateTaskIndex_AllowsCurrentTaskWhenEditing()
        {
            var existingTask = new FakeTask { CurrentTaskIndex = "2" };
            var tasks = new ObservableCollection<object> { existingTask };

            var isValid = TaskIndexValidation.TryValidateTaskIndex("2", tasks, existingTask, out var errorMessage);

            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        [Fact]
        public void TryValidateExecuteConditionIndex_RejectsMissingReference()
        {
            var existingTask = new FakeTask { CurrentTaskIndex = "2" };
            var tasks = new ObservableCollection<object> { existingTask };

            var isValid = TaskIndexValidation.TryValidateExecuteConditionIndex("3", ERROR.NoError, tasks, out var errorMessage);

            Assert.False(isValid);
            Assert.Contains("reference", errorMessage);
        }

        [Fact]
        public void TryValidateExecuteConditionIndex_AllowsValidReference()
        {
            var existingTask = new FakeTask { CurrentTaskIndex = "2" };
            var tasks = new ObservableCollection<object> { existingTask };

            var isValid = TaskIndexValidation.TryValidateExecuteConditionIndex("2", ERROR.NoError, tasks, out var errorMessage);

            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        [Fact]
        public void TryValidateExecuteConditionIndex_AllowsEmptyCondition()
        {
            var tasks = new ObservableCollection<object>();

            var isValid = TaskIndexValidation.TryValidateExecuteConditionIndex("not-a-number", ERROR.Empty, tasks, out var errorMessage);

            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        private sealed class FakeTask : IGenericTaskModel
        {
            public bool? IsTaskCompletedSuccessfully { get; set; }
            public ERROR SelectedExecuteConditionErrorLevel { get; set; }
            public string SelectedExecuteConditionTaskIndex { get; set; }
            public ERROR CurrentTaskErrorLevel { get; set; }
            public string CurrentTaskIndex { get; set; }
            public int SelectedTaskIndexAsInt { get; }
            public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new ObservableCollection<TaskAttemptResult>();
        }
    }
}
