using System;
using System.Collections.ObjectModel;
using System.Linq;

using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.ViewModel.DialogFactories
{
    public class TaskDialogFactory
    {
        private readonly Action notifyChipTasksChanged;
        private readonly Action activateMainWindow;
        private readonly Action<bool> updateReaderBusy;

        public TaskDialogFactory(Action notifyChipTasksChanged, Action activateMainWindow, Action<bool> updateReaderBusy)
        {
            this.notifyChipTasksChanged = notifyChipTasksChanged ?? throw new ArgumentNullException(nameof(notifyChipTasksChanged));
            this.activateMainWindow = activateMainWindow ?? throw new ArgumentNullException(nameof(activateMainWindow));
            this.updateReaderBusy = updateReaderBusy ?? (_ => { });
        }

        public IUserDialogViewModel CreateGenericChipTaskDialog(object selectedSetupViewModel, ChipTaskHandlerModel chipTasks, ObservableCollection<IDialogViewModel> dialogs)
        {
            return new GenericChipTaskViewModel(selectedSetupViewModel, chipTasks.TaskCollection, dialogs)
            {
                Caption = ResourceLoader.GetResource("windowCaptionAddEditGenericChipTask"),

                OnOk = async (sender) =>
                {
                    if (sender.SelectedTaskType == TaskType_GenericChipTask.ChangeDefault)
                    {
                        await sender.SaveSettings.ExecuteAsync(null);
                    }

                    if (sender.SelectedTaskType == TaskType_GenericChipTask.ChipIsOfType ||
                        sender.SelectedTaskType == TaskType_GenericChipTask.CheckUID ||
                        sender.SelectedTaskType == TaskType_GenericChipTask.ChipIsMultiChip)
                    {
                        if (!TryValidateTaskIndices(sender.CurrentTaskIndex, sender.SelectedExecuteConditionTaskIndex, sender.SelectedExecuteConditionErrorLevel, chipTasks.TaskCollection, selectedSetupViewModel, dialogs))
                        {
                            return;
                        }

                        if (chipTasks.TaskCollection.OfType<GenericChipTaskViewModel>().Any(x => x.SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt))
                        {
                            chipTasks.TaskCollection.RemoveAt(chipTasks.TaskCollection.IndexOf(selectedSetupViewModel));
                        }

                        chipTasks.TaskCollection.Add(sender);

                        chipTasks.TaskCollection = new ObservableCollection<object>(chipTasks.TaskCollection.OrderBy(x => (x as IGenericTaskModel).SelectedTaskIndexAsInt));

                        notifyChipTasksChanged();
                    }
                    sender.Close();

                    activateMainWindow();
                },

                OnCancel = (sender) =>
                {
                    sender.Close();

                    activateMainWindow();
                },

                OnAuth = (sender) =>
                {
                },

                OnCloseRequest = (sender) =>
                {
                    sender.Close();

                    activateMainWindow();
                }
            };
        }

        public IUserDialogViewModel CreateClassicTaskDialog(object selectedSetupViewModel, ChipTaskHandlerModel chipTasks, ObservableCollection<IDialogViewModel> dialogs)
        {
            return new MifareClassicSetupViewModel(selectedSetupViewModel, dialogs)
            {
                Caption = ResourceLoader.GetResource("windowCaptionAddEditMifareClassicTask"),
                IsClassicAuthInfoEnabled = true,

                OnOk = (sender) =>
                {
                    if (sender.SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
                    {
                        sender.SaveSettings.ExecuteAsync(null);
                    }

                    if (sender.SelectedTaskType == TaskType_MifareClassicTask.WriteData ||
                        sender.SelectedTaskType == TaskType_MifareClassicTask.ReadData ||
                        sender.SelectedTaskType == TaskType_MifareClassicTask.EmptyCheck)
                    {
                        if (!TryValidateTaskIndices(sender.CurrentTaskIndex, sender.SelectedExecuteConditionTaskIndex, sender.SelectedExecuteConditionErrorLevel, chipTasks.TaskCollection, selectedSetupViewModel, dialogs))
                        {
                            return;
                        }

                        if (chipTasks.TaskCollection.OfType<MifareClassicSetupViewModel>().Where(x => (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any())
                        {
                            chipTasks.TaskCollection.RemoveAt(chipTasks.TaskCollection.IndexOf(selectedSetupViewModel));
                        }

                        chipTasks.TaskCollection.Add(sender);

                        chipTasks.TaskCollection = new ObservableCollection<object>(chipTasks.TaskCollection.OrderBy(x => (x as IGenericTaskModel).SelectedTaskIndexAsInt));

                        notifyChipTasksChanged();
                    }
                    sender.Close();

                    activateMainWindow();
                },

                OnUpdateStatus = (sender) =>
                {
                    updateReaderBusy(sender);
                },

                OnCancel = (sender) =>
                {
                    sender.Close();

                    activateMainWindow();
                },

                OnAuth = (sender) =>
                {
                },

                OnCloseRequest = (sender) =>
                {
                    sender.Close();

                    activateMainWindow();
                }
            };
        }

        public IUserDialogViewModel CreateDesfireTaskDialog(object selectedSetupViewModel, ChipTaskHandlerModel chipTasks, ObservableCollection<IDialogViewModel> dialogs)
        {
            return new MifareDesfireSetupViewModel(selectedSetupViewModel, dialogs)
            {
                Caption = ResourceLoader.GetResource("windowCaptionAddEditMifareDesfireTask"),

                OnOk = (sender) =>
                {
                    if (sender.SelectedTaskType == TaskType_MifareDesfireTask.ChangeDefault)
                    {
                        sender.SaveSettings.ExecuteAsync(null);
                    }

                    if (sender.SelectedTaskType == TaskType_MifareDesfireTask.FormatDesfireCard ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.PICCMasterKeyChangeover ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.ReadAppSettings ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.AppExistCheck ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.AuthenticateApplication ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.ApplicationKeyChangeover ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.DeleteApplication ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.CreateApplication ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.DeleteFile ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.CreateFile ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.ReadData ||
                        sender.SelectedTaskType == TaskType_MifareDesfireTask.WriteData)
                    {
                        if (!TryValidateTaskIndices(sender.CurrentTaskIndex, sender.SelectedExecuteConditionTaskIndex, sender.SelectedExecuteConditionErrorLevel, chipTasks.TaskCollection, selectedSetupViewModel, dialogs))
                        {
                            return;
                        }

                        if (chipTasks.TaskCollection.OfType<MifareDesfireSetupViewModel>().Any(x => (x as MifareDesfireSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt))
                        {
                            chipTasks.TaskCollection.RemoveAt(chipTasks.TaskCollection.IndexOf(selectedSetupViewModel));
                        }

                        chipTasks.TaskCollection.Add(sender);

                        chipTasks.TaskCollection = new ObservableCollection<object>(chipTasks.TaskCollection.OrderBy(x => (x as IGenericTaskModel).SelectedTaskIndexAsInt));

                        notifyChipTasksChanged();

                        sender.Close();

                        activateMainWindow();
                    }
                },

                OnUpdateStatus = (sender) =>
                {
                    updateReaderBusy(sender);
                },

                OnCancel = (sender) =>
                {
                    sender.Close();

                    activateMainWindow();
                },

                OnCloseRequest = (sender) =>
                {
                    sender.Close();

                    activateMainWindow();
                }
            };
        }

        public IUserDialogViewModel CreateUltralightTaskDialog(object selectedSetupViewModel, ChipTaskHandlerModel chipTasks, ObservableCollection<IDialogViewModel> dialogs)
        {

            return new MifareUltralightSetupViewModel(selectedSetupViewModel, dialogs)
            {
                Caption = ResourceLoader.GetResource("windowCaptionAddEditMifareDesfireTask"),

                OnOk = (sender) =>
                {
                    if (sender.SelectedTaskType == TaskType_MifareUltralightTask.ChangeDefault)
                    {
                        sender.Settings.SaveSettings();
                    }

                    if (sender.SelectedTaskType == TaskType_MifareUltralightTask.ReadData ||
                        sender.SelectedTaskType == TaskType_MifareUltralightTask.WriteData)
                    {
                        if (!TryValidateTaskIndices(sender.CurrentTaskIndex, sender.SelectedExecuteConditionTaskIndex, sender.SelectedExecuteConditionErrorLevel, chipTasks.TaskCollection, selectedSetupViewModel, dialogs))
                        {
                            return;
                        }

                        if ((chipTasks.TaskCollection.OfType<MifareUltralightSetupViewModel>().Where(x => (x as MifareUltralightSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any()))
                        {
                            chipTasks.TaskCollection.RemoveAt(chipTasks.TaskCollection.IndexOf(selectedSetupViewModel));
                        }

                        chipTasks.TaskCollection.Add(sender);

                        chipTasks.TaskCollection = new ObservableCollection<object>(chipTasks.TaskCollection.OrderBy(x => (x as IGenericTaskModel).SelectedTaskIndexAsInt));

                        notifyChipTasksChanged();

                        sender.Close();

                        activateMainWindow();
                    }
                },

                OnCancel = (sender) =>
                {
                    sender.Close();

                    activateMainWindow();
                },

                OnCloseRequest = (sender) =>
                {
                    sender.Close();

                    activateMainWindow();
                }
            };
        }

        /// <summary>
        /// Validates task indices and shows a dialog when the input is invalid.
        /// </summary>
        /// <param name="taskIndex">The task index assigned to the current task.</param>
        /// <param name="executeConditionTaskIndex">The task index referenced by the execute condition.</param>
        /// <param name="executeConditionErrorLevel">The execute condition error level.</param>
        /// <param name="taskCollection">The collection of existing tasks.</param>
        /// <param name="selectedSetupViewModel">The task being edited, if any.</param>
        /// <param name="dialogs">The dialog collection to update with validation messages.</param>
        /// <returns><see langword="true"/> when validation succeeds; otherwise <see langword="false"/>.</returns>
        private static bool TryValidateTaskIndices(string taskIndex, string executeConditionTaskIndex, ERROR executeConditionErrorLevel, ObservableCollection<object> taskCollection, object selectedSetupViewModel, ObservableCollection<IDialogViewModel> dialogs)
        {
            if (!TaskIndexValidation.TryValidateTaskIndex(taskIndex, taskCollection, selectedSetupViewModel, out var errorMessage) ||
                !TaskIndexValidation.TryValidateExecuteConditionIndex(executeConditionTaskIndex, executeConditionErrorLevel, taskCollection, out errorMessage))
            {
                dialogs?.Add(new CustomDialogViewModel
                {
                    Caption = ResourceLoader.GetResource("messageBoxDefaultCaption"),
                    Message = errorMessage,
                    OnOk = sender => sender.Close()
                });
                return false;
            }

            return true;
        }
    }
}
