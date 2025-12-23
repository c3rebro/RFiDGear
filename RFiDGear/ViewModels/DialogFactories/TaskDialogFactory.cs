using System;
using System.Collections.ObjectModel;
using System.Linq;

using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.Infrastructure.Tasks.Interfaces;

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
                        if (chipTasks.TaskCollection.OfType<GenericChipTaskViewModel>().Any(x => x.SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt))
                        {
                            chipTasks.TaskCollection.RemoveAt(chipTasks.TaskCollection.IndexOf(selectedSetupViewModel));
                        }

                        chipTasks.TaskCollection.Add(sender);

                        chipTasks.TaskCollection = new ObservableCollection<object>(chipTasks.TaskCollection.OrderBy(x => (x as IGenericTask).SelectedTaskIndexAsInt));

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

                        if (chipTasks.TaskCollection.OfType<MifareClassicSetupViewModel>().Where(x => (x as MifareClassicSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any())
                        {
                            chipTasks.TaskCollection.RemoveAt(chipTasks.TaskCollection.IndexOf(selectedSetupViewModel));
                        }

                        chipTasks.TaskCollection.Add(sender);

                        chipTasks.TaskCollection = new ObservableCollection<object>(chipTasks.TaskCollection.OrderBy(x => (x as IGenericTask).SelectedTaskIndexAsInt));

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
                        if (chipTasks.TaskCollection.OfType<MifareDesfireSetupViewModel>().Any(x => (x as MifareDesfireSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt))
                        {
                            chipTasks.TaskCollection.RemoveAt(chipTasks.TaskCollection.IndexOf(selectedSetupViewModel));
                        }

                        chipTasks.TaskCollection.Add(sender);

                        chipTasks.TaskCollection = new ObservableCollection<object>(chipTasks.TaskCollection.OrderBy(x => (x as IGenericTask).SelectedTaskIndexAsInt));

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
                        if ((chipTasks.TaskCollection.OfType<MifareUltralightSetupViewModel>().Where(x => (x as MifareUltralightSetupViewModel).SelectedTaskIndexAsInt == sender.SelectedTaskIndexAsInt).Any()))
                        {
                            chipTasks.TaskCollection.RemoveAt(chipTasks.TaskCollection.IndexOf(selectedSetupViewModel));
                        }

                        chipTasks.TaskCollection.Add(sender);

                        chipTasks.TaskCollection = new ObservableCollection<object>(chipTasks.TaskCollection.OrderBy(x => (x as IGenericTask).SelectedTaskIndexAsInt));

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
    }
}
