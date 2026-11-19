using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

using RFiDGear.Bridge;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.ViewModel;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The revision of a project says in which state it is: the same for the same project, another when anything a save would
    /// write is another, and not changed by what a run only shows. It is a keyed hash, so that it gives nothing away about the keys
    /// that are typed into tasks.
    /// </summary>
    public sealed class ProjectRevisionTests
    {
        private static ChipTaskHandlerModel Project(int taskCount)
        {
            var model = new ChipTaskHandlerModel();

            for (var i = 0; i < taskCount; i++)
            {
                model.TaskCollection.Add(new CommonTaskViewModel { CurrentTaskIndex = i.ToString() });
            }

            return model;
        }

        [Fact]
        public void TheSameProject_HasTheSameRevision_WhichIsAValidOneForTheProtocol()
        {
            var model = Project(3);

            var first = ProjectRevision.Compute(model);
            var second = ProjectRevision.Compute(model);

            Assert.Equal(first, second);
            Assert.Matches("^[0-9a-f]{32}$", first);
            Assert.True(BridgeProtocol.IsValidRevision(first));
        }

        [Fact]
        public void ATaskThatWasChangedInTheWindow_ChangesTheRevision_WithoutAChangeOfTheCount()
        {
            var model = Project(2);
            var before = ProjectRevision.Compute(model);

            var task = (CommonTaskViewModel)model.TaskCollection[0];
            task.SelectedTaskDescription = "changed after the client looked";

            Assert.Equal(2, model.TaskCollection.Count);
            Assert.NotEqual(before, ProjectRevision.Compute(model));
        }

        [Fact]
        public void ReorderingAndRemovingTasks_BothChangeTheRevision()
        {
            var model = Project(3);
            var original = ProjectRevision.Compute(model);

            model.TaskCollection.Move(0, 2);
            var reordered = ProjectRevision.Compute(model);
            model.TaskCollection.Move(2, 0);
            var back = ProjectRevision.Compute(model);
            model.TaskCollection.RemoveAt(2);
            var removed = ProjectRevision.Compute(model);

            Assert.NotEqual(original, reordered);
            Assert.Equal(original, back);
            Assert.NotEqual(original, removed);
            Assert.NotEqual(reordered, removed);
        }

        [Fact]
        public void WhatARunOnlyShows_IsNotPartOfTheRevision()
        {
            var model = Project(2);
            var before = ProjectRevision.Compute(model);

            // What a run leaves on the tasks: neither a save nor a project that is loaded again has it.
            foreach (var task in model.TaskCollection)
            {
                var common = (CommonTaskViewModel)task;
                common.CurrentTaskErrorLevel = ERROR.AuthFailure;
                common.IsTaskCompletedSuccessfully = false;
                common.ExecutionState = TaskExecutionState.Running;
            }

            Assert.Equal(before, ProjectRevision.Compute(model));
        }

        [Fact]
        public async Task TheFocusOfATask_IsAStateOfTheWindow_AndNotPartOfTheRevision()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var model = new ChipTaskHandlerModel();
                var task = new GenericChipTaskViewModel { CurrentTaskIndex = "0", SelectedTaskType = TaskType_GenericChipTask.ChipIsOfType };
                model.TaskCollection.Add(task);
                var before = ProjectRevision.Compute(model);

                // The loop focuses the task it is working on.
                task.IsFocused = true;

                Assert.Equal(before, ProjectRevision.Compute(model));
            });
        }

        [Fact]
        public async Task TheDataOfAWriteTaskThatTakesItFromAFile_IsNoPartOfTheRevision_ItIsReplacedBeforeTheWrite()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var model = new ChipTaskHandlerModel();
                var fromProject = new MifareDesfireSetupViewModel { CurrentTaskIndex = "0", SelectedTaskType = TaskType_MifareDesfireTask.WriteData, RefreshDesfireDataFromFileBeforeWrite = false };
                var fromFile = new MifareDesfireSetupViewModel { CurrentTaskIndex = "1", SelectedTaskType = TaskType_MifareDesfireTask.WriteData, RefreshDesfireDataFromFileBeforeWrite = true };
                model.TaskCollection.Add(fromProject);
                model.TaskCollection.Add(fromFile);
                fromProject.ChildNodeViewModelTemp.Children.Add(new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(new byte[] { 1, 2, 3, 4 }, 0), fromProject.ChildNodeViewModelTemp));
                fromFile.ChildNodeViewModelTemp.Children.Add(new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(new byte[] { 1, 2, 3, 4 }, 0), fromFile.ChildNodeViewModelTemp));
                var before = ProjectRevision.Compute(model);

                // The write of the second task puts the content of its file here before it writes: what is in the buffer is not its input.
                fromFile.ChildNodeViewModelTemp.Children[0].DesfireFile = new MifareDesfireFileModel(new byte[] { 9, 9, 9, 9 }, 0);
                var afterTheFile = ProjectRevision.Compute(model);
                fromProject.ChildNodeViewModelTemp.Children[0].DesfireFile = new MifareDesfireFileModel(new byte[] { 9, 9, 9, 9 }, 0);
                var afterTheEdit = ProjectRevision.Compute(model);

                Assert.Equal(before, afterTheFile);
                Assert.NotEqual(before, afterTheEdit);
            });
        }

        [Fact]
        public async Task TheDataOfAClassicTask_IsPartOfTheRevisionForAWriteTask_AndNotForOneThatReads()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var model = new ChipTaskHandlerModel();
                var write = new MifareClassicSetupViewModel { CurrentTaskIndex = "0", SelectedTaskType = TaskType_MifareClassicTask.WriteData };
                var read = new MifareClassicSetupViewModel { CurrentTaskIndex = "1", SelectedTaskType = TaskType_MifareClassicTask.ReadData };
                model.TaskCollection.Add(write);
                model.TaskCollection.Add(read);
                var all = ProjectRevision.Compute(model);

                // What a read puts into the task: the data of the card, in both buffers.
                read.ChildNodeViewModelTemp.Children[0].MifareClassicDataBlock.Data = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
                read.ChildNodeViewModelFromChip.Children[0].MifareClassicDataBlock.Data = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
                var afterTheRead = ProjectRevision.Compute(model);

                // What a person types into the task that writes.
                write.ChildNodeViewModelTemp.Children[0].MifareClassicDataBlock.Data = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
                var afterTheEdit = ProjectRevision.Compute(model);

                Assert.Equal(all, afterTheRead);
                Assert.NotEqual(all, afterTheEdit);
            });
        }

        [Fact]
        public void TheRevision_IsNotAPlainHashOfTheText_ThatATypedKeyIsWrittenInto()
        {
            var model = Project(1);
            using var stream = new MemoryStream();

            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                new XmlSerializer(typeof(ChipTaskHandlerModel)).Serialize(writer, model);
            }

            var plain = Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();

            // Whoever sees a revision could otherwise test guesses for a key against it. The keyed hash differs from any plain one.
            Assert.NotEqual(plain.Substring(0, 32), ProjectRevision.Compute(model));
        }
    }
}
