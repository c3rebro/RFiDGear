using System.Collections.ObjectModel;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Models;
using RFiDGear.UI.Behaviors;
using Xunit;

namespace RFiDGear.Tests
{
    public class DataGridDragDropBehaviorTests
    {
        [Fact]
        public void TryAssignTaskIndexForDrop_UsesFirstAvailableGapBetweenNeighbors()
        {
            var previous = new StubTask { CurrentTaskIndex = "1" };
            var dragged = new StubTask { CurrentTaskIndex = "5" };
            var next = new StubTask { CurrentTaskIndex = "4" };
            var other = new StubTask { CurrentTaskIndex = "2" };
            var items = new ObservableCollection<object> { previous, dragged, next, other };

            var result = DataGridDragDropBehavior.TryAssignTaskIndexForDrop(items, dragged, 1, out var errorMessage);

            Assert.True(result);
            Assert.Null(errorMessage);
            Assert.Equal("3", dragged.CurrentTaskIndex);
        }

        [Fact]
        public void TryAssignTaskIndexForDrop_NoGapBetweenNeighbors_ReturnsFalse()
        {
            var previous = new StubTask { CurrentTaskIndex = "1" };
            var dragged = new StubTask { CurrentTaskIndex = "9" };
            var next = new StubTask { CurrentTaskIndex = "2" };
            var items = new ObservableCollection<object> { previous, dragged, next };

            var result = DataGridDragDropBehavior.TryAssignTaskIndexForDrop(items, dragged, 1, out var errorMessage);

            Assert.False(result);
            Assert.Equal("9", dragged.CurrentTaskIndex);
            Assert.False(string.IsNullOrWhiteSpace(errorMessage));
        }

        private sealed class StubTask : IGenericTask
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
