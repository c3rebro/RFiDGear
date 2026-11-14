using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Models;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel.DialogFactories;
using RFiDGear.ViewModel.TaskSetupViewModels;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The Ultralight task as a task kind of the application: it is saved with the project, listed and ordered with the other
    /// kinds, and what a run leaves in it is not written (#220). Running it is tested with the loop, in
    /// <see cref="TaskExecutionServiceTests"/>.
    /// </summary>
    public class UltralightTaskKindTests
    {
        private static string Serialize(ChipTaskHandlerModel project)
        {
            using (var writer = new StringWriter())
            {
                new XmlSerializer(typeof(ChipTaskHandlerModel)).Serialize(writer, project);

                return writer.ToString();
            }
        }

        private static ChipTaskHandlerModel Deserialize(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                return (ChipTaskHandlerModel)new XmlSerializer(typeof(ChipTaskHandlerModel)).Deserialize(reader);
            }
        }

        [Fact]
        public async Task AProjectWithAnUltralightTask_IsSavedAndLoadedWithWhatTheUserChose()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareUltralightSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareUltralightTask.WriteData,
                    CurrentTaskIndex = "4",
                    SelectedTaskDescription = "Write the badge",
                    SelectedExecuteConditionErrorLevel = ERROR.AuthFailure,
                    SelectedExecuteConditionTaskIndex = "2"
                };
                var project = new ChipTaskHandlerModel();
                project.TaskCollection.Add(task);

                var xml = Serialize(project);

                Assert.Contains("<SelectedTaskType>WriteData</SelectedTaskType>", xml);

                var loaded = Assert.IsType<MifareUltralightSetupViewModel>(Assert.Single(Deserialize(xml).TaskCollection));
                Assert.Equal(TaskType_MifareUltralightTask.WriteData, loaded.SelectedTaskType);
                Assert.Equal("4", loaded.CurrentTaskIndex);
                Assert.Equal("Write the badge", loaded.SelectedTaskDescription);
                Assert.Equal(ERROR.AuthFailure, loaded.SelectedExecuteConditionErrorLevel);
                Assert.Equal("2", loaded.SelectedExecuteConditionTaskIndex);
            });
        }

        [Fact]
        public async Task WhatARunLeavesInATask_AndTheEditorsOwnState_IsNotWrittenToTheProject()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareUltralightSetupViewModel { SelectedTaskType = TaskType_MifareUltralightTask.ReadData, CurrentTaskIndex = "1" };
                task.IsTaskCompletedSuccessfully = true;
                task.CurrentTaskErrorLevel = ERROR.AuthFailure;
                task.StatusText = "something that happened";
                ((IGenericTask)(object)task).ExecutionState = TaskExecutionState.Completed;

                var project = new ChipTaskHandlerModel();
                project.TaskCollection.Add(task);

                var xml = Serialize(project);

                // The names of the properties: what a run leaves behind, and what only the open dialog shows.
                foreach (var name in new[]
                {
                    "IsTaskCompletedSuccessfully",
                    "CurrentTaskErrorLevel",
                    "ExecutionState",
                    "AttemptResults",
                    "StatusText",
                    "IsValidSelectedTaskIndex",
                    "IsValidSelectedExecuteConditionTaskIndex",
                    "IsDataExplorerEditTabEnabled",
                    "ChildNodeViewModel",
                    "SelectedUltralightPageCurrent",
                    "MifareUltralightPages",
                    "AvailableTasks"
                })
                {
                    Assert.DoesNotContain("<" + name, xml);
                }
            });
        }

        [Theory]
        [InlineData(TaskType_MifareUltralightTask.ReadData)]
        [InlineData(TaskType_MifareUltralightTask.WriteData)]
        public async Task CreateUltralightTaskDialog_OnOkAddsTheTask(TaskType_MifareUltralightTask taskType)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var factory = new TaskDialogFactory(() => { }, () => { }, _ => { }, new OpenTaskListEditGate());
                var chipTasks = new ChipTaskHandlerModel();
                var dialogs = new ObservableCollection<IDialogViewModel>();
                var dialog = (MifareUltralightSetupViewModel)factory.CreateUltralightTaskDialog(null, chipTasks, dialogs);
                dialog.SelectedTaskType = taskType;

                dialog.OnOk(dialog);

                // A validation message would mean the index was rejected, not that the task is missing.
                Assert.Empty(dialogs);
                Assert.Contains(dialog, chipTasks.TaskCollection);
            });
        }

        [Fact]
        public async Task ATaskOfAnotherKind_CanBeAddedWhileAnUltralightTaskIsInTheList_AndTheListStaysInOrder()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var factory = new TaskDialogFactory(() => { }, () => { }, _ => { }, new OpenTaskListEditGate());
                var chipTasks = new ChipTaskHandlerModel();
                var dialogs = new ObservableCollection<IDialogViewModel>();

                var ultralight = (MifareUltralightSetupViewModel)factory.CreateUltralightTaskDialog(null, chipTasks, dialogs);
                ultralight.SelectedTaskType = TaskType_MifareUltralightTask.ReadData;
                ultralight.CurrentTaskIndex = "5";
                ultralight.OnOk(ultralight);

                var desfire = (MifareDesfireSetupViewModel)factory.CreateDesfireTaskDialog(null, chipTasks, dialogs);
                desfire.SelectedTaskType = TaskType_MifareDesfireTask.ReadData;
                desfire.CurrentTaskIndex = "2";
                desfire.OnOk(desfire);

                Assert.Empty(dialogs);
                Assert.Equal(2, chipTasks.TaskCollection.Count);
                Assert.Same(desfire, chipTasks.TaskCollection[0]);
                Assert.Same(ultralight, chipTasks.TaskCollection[1]);
            });
        }
    }
}
