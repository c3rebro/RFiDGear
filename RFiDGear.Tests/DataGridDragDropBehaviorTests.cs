using System.Collections.ObjectModel;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Models;
using RFiDGear.UI.Behaviors;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using Xunit;

namespace RFiDGear.Tests
{
    public class DataGridDragDropBehaviorTests
    {
        [Fact]
        public void HasExecuteCondition_ReturnsFalseWhenUnset()
        {
            var task = new StubTaskModel
            {
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
                SelectedExecuteConditionTaskIndex = string.Empty
            };

            Assert.False(DataGridDragDropBehavior.HasExecuteCondition(task));
        }

        [Fact]
        public void HasExecuteCondition_ReturnsTrueWhenIndexSet()
        {
            var task = new StubTaskModel
            {
                SelectedExecuteConditionErrorLevel = ERROR.Empty,
                SelectedExecuteConditionTaskIndex = "2"
            };

            Assert.True(DataGridDragDropBehavior.HasExecuteCondition(task));
        }

        [Fact]
        public void HasExecuteCondition_ReturnsTrueWhenErrorLevelSet()
        {
            var task = new StubTaskModel
            {
                SelectedExecuteConditionErrorLevel = ERROR.NoError,
                SelectedExecuteConditionTaskIndex = string.Empty
            };

            Assert.True(DataGridDragDropBehavior.HasExecuteCondition(task));
        }

        [Fact]
        public void TryBlockDragDropWithDialog_AddsDialogWhenExecuteConditionIsSet()
        {
            var task = new StubTaskModel
            {
                SelectedExecuteConditionErrorLevel = ERROR.NoError,
                SelectedExecuteConditionTaskIndex = string.Empty
            };
            var dialogs = new ObservableCollection<IDialogViewModel>();

            var blocked = DataGridDragDropBehavior.TryBlockDragDropWithDialog(task, dialogs);

            Assert.True(blocked);
            Assert.Single(dialogs);
        }

        private sealed class StubTaskModel : IGenericTask
        {
            public bool? IsTaskCompletedSuccessfully { get; set; }

            public ERROR SelectedExecuteConditionErrorLevel { get; set; }

            public string SelectedExecuteConditionTaskIndex { get; set; } = string.Empty;

            public ERROR CurrentTaskErrorLevel { get; set; }

            public string CurrentTaskIndex { get; set; } = string.Empty;

            public int SelectedTaskIndexAsInt => int.TryParse(CurrentTaskIndex, out var index) ? index : -1;

            public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new();
        }
    }
}
