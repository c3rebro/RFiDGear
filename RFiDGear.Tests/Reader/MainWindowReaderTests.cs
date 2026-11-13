using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.ViewModel;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The main window and the reader scan of an extension, with the real view model and a scripted reader: its reads go
    /// through the same gate as a scan, they feed the picture the extensions see, and a run that meets a scan waits.
    /// These tests need the same things as the other tests that build a main window (a reachable update server).
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class MainWindowReaderTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static GenericChipModel LfCard(string uid) => new GenericChipModel(uid, CARD_TYPE.EM4102);

        private static async Task Within(Task task)
        {
            var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20)));
            Assert.Same(task, finished);
            await task;
        }

        private static async Task WithViewModel(Func<MainWindowViewModel, ScriptedReadProvider, ExtensionOperationCoordinator, Task> test)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                try
                {
                    var viewModel = await MainWindowViewModelTests.CreateInitializedViewModelAsync(tempRoot);
                    var provider = new ScriptedReadProvider();
                    var coordinator = (ExtensionOperationCoordinator)typeof(MainWindowViewModel).GetField("_coordinator", Private)!.GetValue(viewModel)!;
                    using (ReaderDeviceScope.Use(provider))
                    {
                        await test(viewModel, provider, coordinator);
                    }
                }
                finally
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            });
        }

        [Fact]
        public async Task TheReadButton_PutsTheCardInTheTree_PublishesItToExtensions_AndRaisesTheEventOnlyForAChange()
        {
            await WithViewModel(async (viewModel, provider, _) =>
            {
                var events = 0;
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => events++;
                provider.Script(ERROR.NoError, LfCard("0123456789"));
                provider.Script(ERROR.NoError, LfCard("0123456789"));

                await viewModel.ReadChipCommand.ExecuteAsync(null);

                Assert.Equal(1, events);
                Assert.Contains(viewModel.TreeViewParentNodes, node => node.UID == "0123456789");
                Assert.False(viewModel.ReaderGate.IsHeld);

                // What the host holds is the picture of that read, not a live look at the shared chip.
                provider.GenericChip = new GenericChipModel();
                Assert.Equal("0123456789", Assert.Single(viewModel.ExtensionHost.Reader.CurrentSnapshot.Credentials).Uid);

                await viewModel.ReadChipCommand.ExecuteAsync(null);

                Assert.Equal(1, events);
            });
        }

        [Fact]
        public async Task TheReadButton_WaitsForAScanThatHoldsTheReader_AndThenReads()
        {
            await WithViewModel(async (viewModel, provider, _) =>
            {
                provider.Script(ERROR.NoError, LfCard("0123456789"));
                Assert.True(viewModel.ReaderGate.TryEnter());

                var read = viewModel.ReadChipCommand.ExecuteAsync(null);
                Assert.False(read.IsCompleted);
                Assert.Equal(0, provider.ReadCount);

                viewModel.ReaderGate.Exit();
                await read;

                Assert.Equal(1, provider.ReadCount);
                Assert.False(viewModel.ReaderGate.IsHeld);
            });
        }

        [Fact]
        public async Task AReadThatThrows_GivesTheReaderBack()
        {
            await WithViewModel(async (viewModel, provider, _) =>
            {
                provider.Script(ERROR.NoError, failure: new InvalidOperationException("The reader failed."));

                await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.ReadChipCommand.ExecuteAsync(null));

                Assert.Equal(1, provider.ReadCount);
                Assert.False(viewModel.ReaderGate.IsHeld);
            });
        }

        [Fact]
        public async Task AReadThatMeetsAChangeOfTheReader_IsNotPublished_SoNoSecondEventFollows()
        {
            await WithViewModel(async (viewModel, provider, _) =>
            {
                var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                provider.Script(ERROR.NoError, LfCard("0123456789"), hold.Task);
                var events = 0;
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => events++;

                var read = viewModel.ReadChipCommand.ExecuteAsync(null);
                await Within(provider.ReadStarted(1));
                ((HeadlessExtensionHost)viewModel.ExtensionHost).NotifyReaderStateChanged();
                hold.SetResult(true);
                await read;

                Assert.Equal(1, events);

                // And nothing was stored either: with the chip gone, the host shows what the reader shows now.
                provider.GenericChip = new GenericChipModel();
                Assert.False(viewModel.ExtensionHost.Reader.CurrentSnapshot.HasCard);
            });
        }

        [Fact]
        public async Task AChangeOfTheReader_BetweenTheStoreAndTheAnnouncement_LeavesTheEventToTheChangeOfTheReader()
        {
            await WithViewModel(async (viewModel, provider, _) =>
            {
                provider.Script(ERROR.NoError, LfCard("0123456789"));
                var events = 0;
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => events++;

                // The command sets IsReaderBusy to false just before it announces the picture it stored.
                var changed = false;
                viewModel.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(viewModel.IsReaderBusy) && viewModel.IsReaderBusy == false && !changed)
                    {
                        changed = true;
                        ((HeadlessExtensionHost)viewModel.ExtensionHost).NotifyReaderStateChanged();
                    }
                };

                await viewModel.ReadChipCommand.ExecuteAsync(null);

                Assert.True(changed);
                Assert.Equal(1, events);
            });
        }

        [Fact]
        public async Task AHandlerOfTheEventOfTheReadButton_MayScanAgain_BecauseTheReadHasLetGoOfTheReader()
        {
            await WithViewModel(async (viewModel, provider, _) =>
            {
                provider.Script(ERROR.NoError, LfCard("0123456789"));
                provider.Script(ERROR.NoError, LfCard("0123456789"));
                Task<ReaderSnapshot>? scan = null;
                viewModel.ExtensionHost.Reader.ReaderStateChanged += (_, _) => scan ??= viewModel.ExtensionHost.Reader.ScanAsync();

                await viewModel.ReadChipCommand.ExecuteAsync(null);

                Assert.NotNull(scan);
                Assert.Equal(ReaderScanState.Ready, (await scan!).State);
                Assert.Equal(2, provider.ReadCount);
            });
        }

        [Fact]
        public async Task TheHostOfTheMainWindow_ScansWithTheGateOfTheMainWindow()
        {
            await WithViewModel(async (viewModel, provider, _) =>
            {
                Assert.True(viewModel.ReaderGate.TryEnter());

                var snapshot = await viewModel.ExtensionHost.Reader.ScanAsync();

                Assert.Equal(ReaderScanState.Busy, snapshot.State);
                Assert.Equal(0, provider.ReadCount);
                viewModel.ReaderGate.Exit();
            });
        }

        [Fact]
        public async Task TheTimerTick_SkipsItsRead_WhileTheReaderIsHeld_AndReadsAndGivesItBackOtherwise()
        {
            await WithViewModel(async (viewModel, provider, _) =>
            {
                var tick = typeof(MainWindowViewModel).GetMethod("UpdateChip", Private)!;

                // The clock only has an effect while automatic mode is on (it is off until it is asked for).
                viewModel.AutoMode.TryStart(requireReady: false, keepApplicationOpen: false);
                Assert.True(viewModel.ReaderGate.TryEnter());

                tick.Invoke(viewModel, new object?[] { null, EventArgs.Empty });

                Assert.Equal(0, provider.ReadCount);
                Assert.NotEqual(true, viewModel.IsReaderBusy);
                viewModel.ReaderGate.Exit();

                tick.Invoke(viewModel, new object?[] { null, EventArgs.Empty });
                await WaitUntil(() => provider.ReadCount == 1 && !viewModel.ReaderGate.IsHeld, "the tick to read and give the reader back");
                Assert.False(viewModel.IsReaderBusy);
            });
        }

        [Fact]
        public async Task ARunThatMeetsAScan_WaitsForIt_AndThenStarts()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                Assert.True(coordinator.TryBeginScan());

                var start = viewModel.TryBeginExecutionAfterAScanAsync();
                Assert.False(start.IsCompleted);

                coordinator.EndScan();

                Assert.True(await start);
                Assert.True(coordinator.IsExecuting);
                coordinator.EndExecution();
            });
        }

        [Fact]
        public async Task TheWriteOnceCommand_WaitsForAScan_InsteadOfDroppingTheRun()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                var tasks = (FakeTaskExecutionService)typeof(MainWindowViewModel).GetField("taskExecutionService", Private)!.GetValue(viewModel)!;
                Assert.True(coordinator.TryBeginScan());

                var run = viewModel.WriteToChipOnceCommand.ExecuteAsync(null);
                Assert.False(run.IsCompleted);
                Assert.Equal(0, tasks.CurrentTaskIndex);

                coordinator.EndScan();
                await Within(run);

                Assert.Equal(1, tasks.CurrentTaskIndex);
                Assert.False(coordinator.IsExecuting);
            });
        }

        [Fact]
        public async Task ARunThatWaitsLongerThanItMay_GivesUp()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                viewModel.MaxWaitForAScan = TimeSpan.FromMilliseconds(100);
                Assert.True(coordinator.TryBeginScan());

                Assert.False(await viewModel.TryBeginExecutionAfterAScanAsync());
                Assert.True(coordinator.IsScanning);
                coordinator.EndScan();
            });
        }

        [Fact]
        public async Task ARunThatMeetsAnotherRun_IsRefusedAtOnce_NotAfterAWait()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                Assert.True(coordinator.TryBeginExecution());

                var start = viewModel.TryBeginExecutionAfterAScanAsync();

                Assert.True(start.IsCompleted);
                Assert.False(await start);
                coordinator.EndExecution();
            });
        }

        // ── changes of the task list are coordinated with runs ──

        private static CommonTaskViewModel FinishedTask() => new CommonTaskViewModel
        {
            CurrentTaskIndex = "1",
            IsTaskCompletedSuccessfully = true,
            CurrentTaskErrorLevel = ERROR.AuthFailure
        };

        [Fact]
        public async Task TheResetAndRemoveCommands_AreRefusedWhileARunIsInProgress_AndTellTheUser()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                var task = FinishedTask();
                viewModel.ChipTasks.TaskCollection.Add(task);
                viewModel.SelectedSetupViewModel = task;
                var dialogsBefore = viewModel.Dialogs.Count;
                Assert.True(coordinator.TryBeginExecution());

                var commands = new IAsyncRelayCommand[]
                {
                    viewModel.ResetTaskStatusCommand,
                    viewModel.ResetSelectedTaskStatusCommand,
                    viewModel.ResetReportTaskDirectoryCommand,
                    viewModel.RemoveAllTasksCommand
                };

                foreach (var command in commands)
                {
                    await Within(command.ExecuteAsync(null));
                }

                // A finished task keeps its result (so it cannot run a second time) and stays in the list.
                Assert.True(task.IsTaskCompletedSuccessfully);
                Assert.Equal(ERROR.AuthFailure, task.CurrentTaskErrorLevel);
                Assert.Single(viewModel.ChipTasks.TaskCollection);
                Assert.Equal(dialogsBefore + commands.Length, viewModel.Dialogs.Count);
                Assert.True(coordinator.IsExecuting);   // the run's slot is not touched
                coordinator.EndExecution();
            });
        }

        [Fact]
        public async Task TheResetAndRemoveCommands_WorkWhenNothingIsRunning_AndGiveTheSlotBack()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                var task = FinishedTask();
                viewModel.ChipTasks.TaskCollection.Add(task);
                viewModel.SelectedSetupViewModel = task;
                var dialogsBefore = viewModel.Dialogs.Count;

                await Within(viewModel.ResetTaskStatusCommand.ExecuteAsync(null));
                Assert.Null(task.IsTaskCompletedSuccessfully);
                Assert.Equal(ERROR.Empty, task.CurrentTaskErrorLevel);

                task.IsTaskCompletedSuccessfully = true;
                task.CurrentTaskErrorLevel = ERROR.AuthFailure;
                await Within(viewModel.ResetSelectedTaskStatusCommand.ExecuteAsync(null));
                Assert.Null(task.IsTaskCompletedSuccessfully);
                Assert.Equal(ERROR.Empty, task.CurrentTaskErrorLevel);

                task.IsTaskCompletedSuccessfully = true;
                await Within(viewModel.ResetReportTaskDirectoryCommand.ExecuteAsync(null));
                Assert.Null(task.IsTaskCompletedSuccessfully);

                await Within(viewModel.RemoveAllTasksCommand.ExecuteAsync(null));
                Assert.Empty(viewModel.ChipTasks.TaskCollection);

                Assert.Equal(dialogsBefore, viewModel.Dialogs.Count);
                Assert.True(coordinator.TryBeginProjectOperation());   // every command gave the slot back
                coordinator.EndProjectOperation();
            });
        }

        [Fact]
        public async Task AResetThatMeetsAScan_WaitsForIt_AndThenResets()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                // The reader timer resets before it starts a run; a scan of an extension must not drop that step.
                var task = FinishedTask();
                viewModel.ChipTasks.TaskCollection.Add(task);
                Assert.True(coordinator.TryBeginScan());

                var reset = viewModel.ResetTaskStatusCommand.ExecuteAsync(null);
                Assert.False(reset.IsCompleted);
                Assert.True(task.IsTaskCompletedSuccessfully);

                coordinator.EndScan();
                await Within(reset);

                Assert.Null(task.IsTaskCompletedSuccessfully);
                Assert.Equal(ERROR.Empty, task.CurrentTaskErrorLevel);
                Assert.True(coordinator.TryBeginProjectOperation());
                coordinator.EndProjectOperation();
            });
        }

        [Fact]
        public async Task DeletingTheSelectedTask_IsRefusedWhileARunIsInProgress_AndWorksOtherwise()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                var task = FinishedTask();
                viewModel.ChipTasks.TaskCollection.Add(task);
                viewModel.SelectedSetupViewModel = task;
                var dialogsBefore = viewModel.Dialogs.Count;
                Assert.True(coordinator.TryBeginExecution());

                Assert.False(viewModel.TryDeleteSelectedTask());

                Assert.Single(viewModel.ChipTasks.TaskCollection);
                Assert.Equal(dialogsBefore + 1, viewModel.Dialogs.Count);   // the user is told
                Assert.True(coordinator.IsExecuting);
                coordinator.EndExecution();

                Assert.True(viewModel.TryDeleteSelectedTask());

                Assert.Empty(viewModel.ChipTasks.TaskCollection);
                Assert.Equal(dialogsBefore + 1, viewModel.Dialogs.Count);
                Assert.True(coordinator.TryBeginProjectOperation());   // the slot was given back
                coordinator.EndProjectOperation();
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task DeletingASelectionThatIsNotInTheTaskList_IsNotReportedAsRemoved()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                viewModel.ChipTasks.TaskCollection.Add(FinishedTask());
                viewModel.SelectedSetupViewModel = FinishedTask();   // a task that the list does not hold
                var dialogsBefore = viewModel.Dialogs.Count;

                Assert.False(viewModel.TryDeleteSelectedTask());

                Assert.Single(viewModel.ChipTasks.TaskCollection);
                Assert.Equal(dialogsBefore, viewModel.Dialogs.Count);   // the edit was admitted: nobody is told it is busy
                Assert.True(coordinator.TryBeginProjectOperation());   // and the slot was given back
                coordinator.EndProjectOperation();
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ConfirmingTheCommonTaskDialog_IsRefusedWhileARunIsInProgress_AndKeepsTheDialogOpen()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                await Within(viewModel.CreateGenericTaskCommand.ExecuteAsync(null));
                var dialog = viewModel.Dialogs.OfType<CommonTaskViewModel>().Single();
                dialog.SelectedTaskType = TaskType_CommonTask.CheckLogicCondition;
                dialog.CurrentTaskIndex = "7";
                var closed = 0;
                dialog.DialogClosing += (_, _) => closed++;
                var dialogsBefore = viewModel.Dialogs.Count;

                // A run starts while the dialog is open; confirming it now must not change the list under the run.
                Assert.True(coordinator.TryBeginExecution());
                dialog.OnOk(dialog);

                Assert.Empty(viewModel.ChipTasks.TaskCollection);
                Assert.Equal(0, closed);
                Assert.Equal(dialogsBefore + 1, viewModel.Dialogs.Count);   // the message
                coordinator.EndExecution();

                // After the run the same confirmation goes through.
                dialog.OnOk(dialog);

                Assert.Contains(dialog, viewModel.ChipTasks.TaskCollection);
                Assert.Equal(1, closed);
                Assert.True(coordinator.TryBeginProjectOperation());
                coordinator.EndProjectOperation();
            });
        }

        [Fact]
        public async Task TheRunsOwnReset_IsNotRefusedByTheGuard()
        {
            await WithViewModel(async (viewModel, _, coordinator) =>
            {
                var task = FinishedTask();
                viewModel.ChipTasks.TaskCollection.Add(task);

                // The run holds the execution slot; its reset at the start must not ask for the project-edit slot.
                await Within(viewModel.WriteToChipOnceCommand.ExecuteAsync(null));

                Assert.Null(task.IsTaskCompletedSuccessfully);
                Assert.Equal(ERROR.Empty, task.CurrentTaskErrorLevel);
                Assert.False(coordinator.IsExecuting);
            });
        }

        [Fact]
        public async Task TheReader_IsInitializing_UntilTheMonitorHasLookedAtIt_AndTheStatusBarIsToldWhenThatEnds()
        {
            await WithViewModel(async (viewModel, _, _) =>
            {
                var announced = 0;
                viewModel.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(viewModel.IsReaderInitializing))
                    {
                        announced++;
                    }
                };

                // Nothing has looked at the reader yet, whatever IsReaderBusy says: the status bar must not show IDLE.
                Assert.True(viewModel.IsReaderInitializing);
                Assert.False(viewModel.IsReaderBusy);

                await viewModel.CheckReaderAsync();

                Assert.False(viewModel.IsReaderInitializing);
                Assert.Equal(1, announced);

                // It is the start-up that is over, not one look: later looks change nothing, and say nothing.
                await viewModel.CheckReaderAsync();

                Assert.False(viewModel.IsReaderInitializing);
                Assert.Equal(1, announced);
            });
        }

        [Fact]
        public async Task ALookThatIsSkippedBecauseTheReaderIsBusy_DoesNotEndTheInitialization()
        {
            await WithViewModel(async (viewModel, _, _) =>
            {
                viewModel.IsReaderBusy = true;

                await viewModel.CheckReaderAsync();

                Assert.True(viewModel.IsReaderInitializing);

                // The same for a reader a scan of an extension holds.
                viewModel.IsReaderBusy = false;
                Assert.True(viewModel.ReaderGate.TryEnter());

                await viewModel.CheckReaderAsync();

                Assert.True(viewModel.IsReaderInitializing);
                viewModel.ReaderGate.Exit();

                await viewModel.CheckReaderAsync();

                Assert.False(viewModel.IsReaderInitializing);
            });
        }

        [Fact]
        public async Task WithoutAReader_TheInitializationEnds_BecauseThereIsNothingToWaitFor()
        {
            await WithViewModel(async (viewModel, _, _) =>
            {
                using (ReaderDeviceScope.Use(null))
                {
                    Assert.True(viewModel.IsReaderInitializing);

                    await viewModel.CheckReaderAsync();

                    Assert.False(viewModel.IsReaderInitializing);
                }
            });
        }

        [Fact]
        public async Task TheMonitorProbe_IsToldTheReaderIsBusy_WhileAScanHoldsIt()
        {
            await WithViewModel((viewModel, _, _) =>
            {
                viewModel.IsReaderBusy = false;
                Assert.False(viewModel.ReaderBusyForProbe());

                Assert.True(viewModel.ReaderGate.TryEnter());
                Assert.True(viewModel.ReaderBusyForProbe());
                viewModel.ReaderGate.Exit();

                viewModel.IsReaderBusy = true;
                Assert.True(viewModel.ReaderBusyForProbe());

                return Task.CompletedTask;
            });
        }
    }
}
