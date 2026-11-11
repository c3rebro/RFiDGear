using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel.DialogFactories;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RFiDGear.Tests
{
    public class TaskDialogFactoryTests
    {
        /// <summary>
        /// Every DESFire task type except None.
        /// </summary>
        public static IEnumerable<object[]> AddableDesfireTaskTypes()
        {
            return Enum.GetValues(typeof(TaskType_MifareDesfireTask))
                .Cast<TaskType_MifareDesfireTask>()
                .Where(type => type != TaskType_MifareDesfireTask.None)
                .Select(type => new object[] { type });
        }

        /// <summary>
        /// Confirming the dialog with OK must add the task to the task list. A type missing from the whitelist in
        /// the factory silently turns the OK button into a no-op.
        /// </summary>
        [Theory]
        [MemberData(nameof(AddableDesfireTaskTypes))]
        public async Task CreateDesfireTaskDialog_OnOkAddsEveryDesfireTaskType(TaskType_MifareDesfireTask taskType)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var factory = new TaskDialogFactory(() => { }, () => { }, _ => { }, new OpenTaskListEditGate());
                var chipTasks = new ChipTaskHandlerModel();
                var dialogs = new ObservableCollection<IDialogViewModel>();
                var dialog = (MifareDesfireSetupViewModel)factory.CreateDesfireTaskDialog(null, chipTasks, dialogs);
                dialog.SelectedTaskType = taskType;

                dialog.OnOk(dialog);

                // A validation message would mean the task index was rejected, not that the type is missing.
                Assert.Empty(dialogs);
                Assert.Contains(dialog, chipTasks.TaskCollection);
            });
        }

        /// <summary>
        /// When the task list may not be changed (a run is in progress), confirming a dialog changes nothing and the
        /// dialog stays open, so the confirmation can be repeated.
        /// </summary>
        [Fact]
        public async Task OnOk_WhenTheTaskListMayNotBeChanged_ChangesNothingAndKeepsTheDialogOpen()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var gate = new RefusingTaskListEditGate();
                var notified = 0;
                var factory = new TaskDialogFactory(() => notified++, () => { }, _ => { }, gate);
                var chipTasks = new ChipTaskHandlerModel();
                var dialogs = new ObservableCollection<IDialogViewModel>();

                var classic = (MifareClassicSetupViewModel)factory.CreateClassicTaskDialog(null, chipTasks, dialogs);
                classic.SelectedTaskType = TaskType_MifareClassicTask.ReadData;
                var desfire = (MifareDesfireSetupViewModel)factory.CreateDesfireTaskDialog(null, chipTasks, dialogs);
                desfire.SelectedTaskType = TaskType_MifareDesfireTask.ReadData;
                var ultralight = (MifareUltralightSetupViewModel)factory.CreateUltralightTaskDialog(null, chipTasks, dialogs);
                ultralight.SelectedTaskType = TaskType_MifareUltralightTask.ReadData;
                var genericChip = (GenericChipTaskViewModel)factory.CreateGenericChipTaskDialog(null, chipTasks, dialogs);
                genericChip.SelectedTaskType = TaskType_GenericChipTask.CheckUID;

                var closed = 0;
                classic.DialogClosing += (_, _) => closed++;
                desfire.DialogClosing += (_, _) => closed++;
                ultralight.DialogClosing += (_, _) => closed++;
                genericChip.DialogClosing += (_, _) => closed++;

                classic.OnOk(classic);
                desfire.OnOk(desfire);
                ultralight.OnOk(ultralight);
                genericChip.OnOk(genericChip);

                Assert.Equal(4, gate.Attempts);
                Assert.Empty(chipTasks.TaskCollection);
                Assert.Equal(0, notified);
                Assert.Equal(0, closed);
                Assert.Empty(dialogs);   // the gate tells the user itself
            });
        }

        /// <summary>
        /// An edit that the gate admits but whose input is rejected (a duplicate task index) keeps the dialog open
        /// as before, and the gate is told that nothing changed.
        /// </summary>
        [Fact]
        public async Task OnOk_WhenTheInputIsRejected_KeepsTheDialogOpen()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var gate = new OpenTaskListEditGate();
                var factory = new TaskDialogFactory(() => { }, () => { }, _ => { }, gate);
                var chipTasks = new ChipTaskHandlerModel();
                var dialogs = new ObservableCollection<IDialogViewModel>();
                var first = (MifareDesfireSetupViewModel)factory.CreateDesfireTaskDialog(null, chipTasks, dialogs);
                first.SelectedTaskType = TaskType_MifareDesfireTask.ReadData;
                first.OnOk(first);
                Assert.Contains(first, chipTasks.TaskCollection);

                // A second new task with the same index is rejected by the validation.
                var second = (MifareDesfireSetupViewModel)factory.CreateDesfireTaskDialog(null, chipTasks, dialogs);
                second.SelectedTaskType = TaskType_MifareDesfireTask.ReadData;
                second.CurrentTaskIndex = first.CurrentTaskIndex;
                var closed = 0;
                second.DialogClosing += (_, _) => closed++;

                second.OnOk(second);

                Assert.DoesNotContain(second, chipTasks.TaskCollection);
                Assert.Equal(0, closed);
                Assert.Single(dialogs);   // the validation message
            });
        }
    }

    /// <summary>A gate that always admits the change, as if nothing else were going on.</summary>
    internal sealed class OpenTaskListEditGate : ITaskListEditGate
    {
        public bool TryEdit(Func<bool> edit) => edit();
    }

    /// <summary>A gate that refuses every change, as while a run is in progress.</summary>
    internal sealed class RefusingTaskListEditGate : ITaskListEditGate
    {
        public int Attempts { get; private set; }

        public bool TryEdit(Func<bool> edit)
        {
            Attempts++;
            return false;
        }
    }
}
