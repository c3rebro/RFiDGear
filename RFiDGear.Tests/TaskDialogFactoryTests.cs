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
                var factory = new TaskDialogFactory(() => { }, () => { }, _ => { });
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
    }
}
