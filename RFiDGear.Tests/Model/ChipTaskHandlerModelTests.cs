using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using RFiDGear.ViewModel;

namespace RFiDGear.Tests.Model
{
    [TestClass]
    public class ChipTaskHandlerModelTests
    {
        [TestMethod]
        public void TaskCollection_InitializesAsTypedCollection()
        {
            var handler = new ChipTaskHandlerModel();

            Assert.IsInstanceOfType(handler.TaskCollection, typeof(ObservableCollection<IGenericTaskModel>));
            Assert.AreEqual(0, handler.TaskCollection.Count);

            handler.TaskCollection.Add(new FakeTask());

            Assert.AreEqual(1, handler.TaskCollection.Count);
            Assert.IsInstanceOfType(handler.TaskCollection[0], typeof(IGenericTaskModel));
        }

        [TestMethod]
        public void TaskCollection_EnforcesGenericTaskModelTypes()
        {
            var handler = new ChipTaskHandlerModel();

            handler.TaskCollection.Add(new CommonTaskViewModel());
            handler.TaskCollection.Add(new GenericChipTaskViewModel());

            CollectionAssert.AllItemsAreInstancesOfType(handler.TaskCollection, typeof(IGenericTaskModel));
        }

        private class FakeTask : IGenericTaskModel
        {
            public bool? IsTaskCompletedSuccessfully { get; set; }

            public ERROR SelectedExecuteConditionErrorLevel { get; set; }

            public string SelectedExecuteConditionTaskIndex { get; set; }

            public ERROR CurrentTaskErrorLevel { get; set; }

            public string CurrentTaskIndex { get; set; }

            public int SelectedTaskIndexAsInt { get; set; }
        }
    }
}
