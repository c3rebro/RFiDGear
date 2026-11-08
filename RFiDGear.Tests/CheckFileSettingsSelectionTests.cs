using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Services.TaskExecution;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The choice of what the file-settings check compares: its defaults, how it is saved, how it is offered in the dialog
    /// and what the check does with it. The comparison itself is in <see cref="DesfireFileSettingsCheckerTests"/>.
    /// </summary>
    public class CheckFileSettingsSelectionTests
    {
        private const string ZeroAesKey = "00000000000000000000000000000000";

        private static readonly string[] SavedElements =
        {
            "IsFileTypeCompared", "IsFileSizeCompared", "IsFileCommunicationModeCompared", "IsFileReadRightCompared",
            "IsFileWriteRightCompared", "IsFileReadWriteRightCompared", "IsFileChangeRightCompared",
            "FileSettingsCompareMode", "FileSizeRelation",
        };

        [Fact]
        public async Task ANewTask_ComparesEverything_InAllMode_ByEquality()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();

                Assert.All(Ticks(viewModel), ticked => Assert.True(ticked));
                Assert.Equal(DesfireFileSettingsCompareMode.AllSelected, viewModel.FileSettingsCompareMode);
                Assert.True(viewModel.IsFileCompareModeAll);
                Assert.False(viewModel.IsFileCompareModeAny);
                Assert.Equal(NumericRelation.Equal, viewModel.FileSizeRelation);
            });
        }

        [Fact]
        public async Task TheChoice_SurvivesSavingAndLoadingAProject_ByName()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var source = ChangedChoice();

                var xml = Serialize(source);
                var loaded = Deserialize(xml);

                Assert.Contains("<FileSettingsCompareMode>AnySelected</FileSettingsCompareMode>", xml);
                Assert.Contains("<FileSizeRelation>ActualAtLeastExpected</FileSizeRelation>", xml);
                Assert.Contains("<IsFileSizeCompared>false</IsFileSizeCompared>", xml);

                // The radio buttons are views of the mode, not settings of their own.
                Assert.DoesNotContain("IsFileCompareModeAll", xml);
                Assert.DoesNotContain("IsFileCompareModeAny", xml);

                Assert.Equal(Ticks(source), Ticks(loaded));
                Assert.Equal(DesfireFileSettingsCompareMode.AnySelected, loaded.FileSettingsCompareMode);
                Assert.Equal(NumericRelation.ActualAtLeastExpected, loaded.FileSizeRelation);
            });
        }

        [Fact]
        public async Task AProjectWithoutTheChoice_StillComparesEverything()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // A task as the version before the choice saved it: none of the nine elements.
                var xml = Serialize(new MifareDesfireSetupViewModel { SelectedTaskType = TaskType_MifareDesfireTask.CheckFileSettings });
                foreach (var element in SavedElements)
                {
                    xml = Regex.Replace(xml, "<" + element + ">[^<]*</" + element + @">\s*", string.Empty);
                }

                Assert.DoesNotContain("IsFileSizeCompared", xml);

                var loaded = Deserialize(xml);

                Assert.All(Ticks(loaded), ticked => Assert.True(ticked));
                Assert.Equal(DesfireFileSettingsCompareMode.AllSelected, loaded.FileSettingsCompareMode);
                Assert.Equal(NumericRelation.Equal, loaded.FileSizeRelation);
            });
        }

        [Fact]
        public async Task ATaskThatIsEdited_KeepsTheChoice()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var source = ChangedChoice();

                var editor = new MifareDesfireSetupViewModel(source, new ObservableCollection<IDialogViewModel>());

                Assert.Equal(Ticks(source), Ticks(editor));
                Assert.Equal(DesfireFileSettingsCompareMode.AnySelected, editor.FileSettingsCompareMode);
                Assert.True(editor.IsFileCompareModeAny);
                Assert.Equal(NumericRelation.ActualAtLeastExpected, editor.FileSizeRelation);
            });
        }

        [Fact]
        public async Task TheRadioButtons_ActOnlyWhenTheyAreSetToTrue()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();

                viewModel.IsFileCompareModeAny = true;
                Assert.Equal(DesfireFileSettingsCompareMode.AnySelected, viewModel.FileSettingsCompareMode);
                Assert.False(viewModel.IsFileCompareModeAll);

                // The radio button that is switched off by the other one reports false; that must not undo the choice.
                viewModel.IsFileCompareModeAll = false;
                viewModel.IsFileCompareModeAny = false;
                Assert.Equal(DesfireFileSettingsCompareMode.AnySelected, viewModel.FileSettingsCompareMode);

                viewModel.IsFileCompareModeAll = true;
                Assert.Equal(DesfireFileSettingsCompareMode.AllSelected, viewModel.FileSettingsCompareMode);
                Assert.False(viewModel.IsFileCompareModeAny);
            });
        }

        // The size box and the combo boxes of the file tab are shared with other file tasks, which have no choice: a tick
        // that is left off must not switch them off there.
        [Theory]
        [InlineData(TaskType_MifareDesfireTask.CheckFileSettings, true)]
        [InlineData(TaskType_MifareDesfireTask.ReadData, false)]
        [InlineData(TaskType_MifareDesfireTask.WriteData, false)]
        [InlineData(TaskType_MifareDesfireTask.CreateFile, false)]
        [InlineData(TaskType_MifareDesfireTask.ChangeFileSettings, false)]
        public async Task TheInputs_FollowTheirCheckBox_OnlyWhereTheChoiceIsOffered(TaskType_MifareDesfireTask taskType, bool choiceIsOffered)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel { SelectedTaskType = taskType };
                viewModel.IsFileTypeCompared = false;
                viewModel.IsFileSizeCompared = false;
                viewModel.IsFileCommunicationModeCompared = false;
                viewModel.IsFileReadRightCompared = false;
                viewModel.IsFileWriteRightCompared = false;
                viewModel.IsFileReadWriteRightCompared = false;
                viewModel.IsFileChangeRightCompared = false;

                Assert.Equal(choiceIsOffered, viewModel.ShowFileSettingsSelection);
                Assert.Equal(!choiceIsOffered, viewModel.ShowFileCaptionLabels);
                Assert.Equal(!choiceIsOffered && viewModel.ShowFileAccessRights, viewModel.ShowFileAccessRightCaptionLabels);
                Assert.Equal(
                    new[] { !choiceIsOffered, !choiceIsOffered, !choiceIsOffered, !choiceIsOffered, !choiceIsOffered, !choiceIsOffered, !choiceIsOffered },
                    new[]
                    {
                        viewModel.IsFileTypeInputEnabled,
                        viewModel.IsFileSizeInputEnabled,
                        viewModel.IsFileCommunicationModeInputEnabled,
                        viewModel.IsFileReadRightInputEnabled,
                        viewModel.IsFileWriteRightInputEnabled,
                        viewModel.IsFileReadWriteRightInputEnabled,
                        viewModel.IsFileChangeRightInputEnabled,
                    });
            });
        }

        [Fact]
        public async Task ChangingATickOrTheTask_TellsTheView()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();
                var raised = new List<string?>();
                viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

                viewModel.IsFileSizeCompared = false;
                Assert.Contains(nameof(MifareDesfireSetupViewModel.IsFileSizeCompared), raised);
                Assert.Contains(nameof(MifareDesfireSetupViewModel.IsFileSizeInputEnabled), raised);

                raised.Clear();
                viewModel.IsFileSizeCompared = false;
                Assert.Empty(raised);

                viewModel.SelectedTaskType = TaskType_MifareDesfireTask.CheckFileSettings;
                Assert.Contains(nameof(MifareDesfireSetupViewModel.ShowFileSettingsSelection), raised);
                Assert.Contains(nameof(MifareDesfireSetupViewModel.IsFileSizeInputEnabled), raised);
            });
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task NothingSelected_IsRefused_BeforeTheReaderIsAsked(int mode)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                Untick(viewModel);
                viewModel.FileSettingsCompareMode = mode == 1 ? DesfireFileSettingsCompareMode.AnySelected : DesfireFileSettingsCompareMode.AllSelected;
                var reader = new FakeFileSettingsProvider { FileSettings = Settings() };

                await Run(viewModel, reader);

                Assert.Equal(ERROR.ProtocolConstraint, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(0, reader.AuthCalls);
                Assert.Equal(0, reader.GetFileSettingsCalls);
                Assert.Null(viewModel.LastFileSettingsComparison);
                Assert.Contains(TextOf("textBoxStatusFileSettingsNothingSelected"), viewModel.StatusText);
            });
        }

        // What is not compared is not looked at: it may be left in a state that would stop the check if it were compared.
        [Theory]
        [InlineData(1, false, ERROR.NoError)]
        [InlineData(1, true, ERROR.ProtocolConstraint)]
        [InlineData(2, false, ERROR.NoError)]
        [InlineData(2, true, ERROR.ProtocolConstraint)]
        [InlineData(3, false, ERROR.NoError)]
        [InlineData(3, true, ERROR.ProtocolConstraint)]
        public async Task OnlyWhatIsCompared_IsValidated(int setting, bool compared, ERROR expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                switch (setting)
                {
                    case 1:
                        viewModel.FileSizeCurrent = "0";
                        viewModel.IsFileSizeCompared = compared;
                        break;
                    case 2:
                        viewModel.SelectedDesfireFileCryptoMode = EncryptionMode.CM_UNKNOWN;
                        viewModel.IsFileCommunicationModeCompared = compared;
                        break;
                    default:
                        viewModel.SelectedDesfireFileType = FileType_MifareDesfireFileType.ValueFile;
                        viewModel.IsFileTypeCompared = compared;
                        break;
                }

                var reader = new FakeFileSettingsProvider { FileSettings = Settings() };

                await Run(viewModel, reader);

                Assert.Equal(expected, viewModel.CurrentTaskErrorLevel);
                Assert.Equal(expected == ERROR.ProtocolConstraint ? 0 : 1, reader.GetFileSettingsCalls);
            });
        }

        [Fact]
        public async Task TheChoice_IsTheOneTheButtonWasPressedWith_EvenWhenItIsChangedWhileTheCardAnswers()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // Only the size is compared, and it matches; the change key differs but is not compared.
                var viewModel = CreateViewModel();
                Untick(viewModel);
                viewModel.IsFileSizeCompared = true;
                var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var reader = new FakeFileSettingsProvider { FileSettings = Settings(change: 9), AuthenticationGate = release.Task };

                using (ReaderDeviceScope.Use(reader))
                {
                    var check = viewModel.CheckFileSettingsRelayCommand.ExecuteAsync(null);
                    Assert.Equal(1, reader.AuthCalls);

                    // Changed while the card is busy: the change key is compared now, and one match is enough.
                    viewModel.IsFileSizeCompared = false;
                    viewModel.IsFileChangeRightCompared = true;
                    viewModel.FileSettingsCompareMode = DesfireFileSettingsCompareMode.AnySelected;
                    viewModel.FileSizeRelation = NumericRelation.ActualAtMostExpected;

                    release.SetResult(true);
                    await check;
                }

                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
                var logged = ((ITaskOutcomeDetails)viewModel).GetOutcomeDetails();
                Assert.Equal((int)DesfireFileSettingKinds.FileSize, logged["SelectedSettings"]);
                Assert.False((bool)logged["CompareModeAny"]);
                Assert.Equal((int)NumericRelation.Equal, logged["FileSizeRelation"]);
            });
        }

        [Fact]
        public async Task ThePassLine_NamesTheMode_AndNoDifference()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings() });

                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);
                Assert.Contains(TextOf("textBoxStatusCompareModeAll"), viewModel.StatusText);
                Assert.Contains(TextOf("textBoxStatusComparePass"), viewModel.StatusText);
                Assert.DoesNotContain(TextOf("textBoxStatusFileSettingNameWrite"), viewModel.StatusText);
            });
        }

        [Fact]
        public async Task TheFailLine_NamesTheMode_AndTheComparedSettingsThatDiffer()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                Untick(viewModel);
                viewModel.IsFileWriteRightCompared = true;
                viewModel.IsFileChangeRightCompared = true;
                viewModel.IsFileSizeCompared = true;
                viewModel.FileSettingsCompareMode = DesfireFileSettingsCompareMode.AnySelected;

                // The write and the change key differ, the size does not: one match is enough to pass.
                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings(write: 5, change: 9) });
                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);

                // Nothing compared matches: the line says which of them differ.
                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings(size: 99, write: 5, change: 9) });

                Assert.Equal(ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
                Assert.Contains(TextOf("textBoxStatusCompareModeAny"), viewModel.StatusText);
                Assert.Contains(TextOf("textBoxStatusCompareFail"), viewModel.StatusText);

                // Exactly the settings that differ, in the order of the dialog. A name is part of another one ("write
                // access" is part of "read-write access"), so the whole list is compared.
                var names = string.Join(", ", TextOf("textBoxStatusFileSettingNameSize"), TextOf("textBoxStatusFileSettingNameWrite"), TextOf("textBoxStatusFileSettingNameChange"));
                Assert.EndsWith(TextOf("textBoxStatusFileSettingsDiffering").Replace("{1}", names), viewModel.StatusText.TrimEnd());
            });
        }

        // Each tick stands for its own setting, whichever way it is looked at: with only that setting ticked the check
        // passes when the card differs in every other setting, and fails, naming it, when the card differs in it.
        [Theory]
        [InlineData(0, "textBoxStatusFileSettingNameFileType")]
        [InlineData(1, "textBoxStatusFileSettingNameSize")]
        [InlineData(2, "textBoxStatusFileSettingNameCommunicationMode")]
        [InlineData(3, "textBoxStatusFileSettingNameRead")]
        [InlineData(4, "textBoxStatusFileSettingNameWrite")]
        [InlineData(5, "textBoxStatusFileSettingNameReadWrite")]
        [InlineData(6, "textBoxStatusFileSettingNameChange")]
        public async Task EachTick_StandsForItsOwnSetting(int setting, string nameKey)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                Untick(viewModel);
                Tick(viewModel, setting);

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Card(other => other != setting) });
                Assert.Equal(ERROR.NoError, viewModel.CurrentTaskErrorLevel);

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Card(other => other == setting) });
                Assert.Equal(ERROR.IsNotTrue, viewModel.CurrentTaskErrorLevel);
                Assert.EndsWith(TextOf("textBoxStatusFileSettingsDiffering").Replace("{1}", TextOf(nameKey)), viewModel.StatusText.TrimEnd());
            });
        }

        // The relation of the size is the one the dialog has when the button is pressed; the expected size is 16.
        [Theory]
        [InlineData(NumericRelation.Equal, 16u, ERROR.NoError)]
        [InlineData(NumericRelation.Equal, 17u, ERROR.IsNotTrue)]
        [InlineData(NumericRelation.ActualAtLeastExpected, 17u, ERROR.NoError)]
        [InlineData(NumericRelation.ActualAtLeastExpected, 15u, ERROR.IsNotTrue)]
        [InlineData(NumericRelation.ActualAtMostExpected, 15u, ERROR.NoError)]
        [InlineData(NumericRelation.ActualAtMostExpected, 17u, ERROR.IsNotTrue)]
        public async Task TheSizeRelation_OfTheDialog_ReachesTheCheck(NumericRelation relation, uint onTheCard, ERROR expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = CreateViewModel();
                Untick(viewModel);
                viewModel.IsFileSizeCompared = true;
                viewModel.FileSizeRelation = relation;

                await Run(viewModel, new FakeFileSettingsProvider { FileSettings = Settings(size: onTheCard) });

                Assert.Equal(expected, viewModel.CurrentTaskErrorLevel);
            });
        }

        // A task with a choice that is not the default, to see it saved, loaded and copied.
        private static MifareDesfireSetupViewModel ChangedChoice()
        {
            var viewModel = CreateViewModel();
            viewModel.IsFileSizeCompared = false;
            viewModel.IsFileWriteRightCompared = false;
            viewModel.FileSettingsCompareMode = DesfireFileSettingsCompareMode.AnySelected;
            viewModel.FileSizeRelation = NumericRelation.ActualAtLeastExpected;

            return viewModel;
        }

        // Ticks one setting: 0 file type, 1 size, 2 communication mode, 3 read, 4 write, 5 read-write, 6 change.
        private static void Tick(MifareDesfireSetupViewModel viewModel, int setting)
        {
            switch (setting)
            {
                case 0: viewModel.IsFileTypeCompared = true; break;
                case 1: viewModel.IsFileSizeCompared = true; break;
                case 2: viewModel.IsFileCommunicationModeCompared = true; break;
                case 3: viewModel.IsFileReadRightCompared = true; break;
                case 4: viewModel.IsFileWriteRightCompared = true; break;
                case 5: viewModel.IsFileReadWriteRightCompared = true; break;
                default: viewModel.IsFileChangeRightCompared = true; break;
            }
        }

        // A file that differs from what the dialog expects in the settings the predicate names (numbered as in Tick) and
        // in no other.
        private static DESFireFileSettings Card(Func<int, bool> differsIn) => Settings(
            size: differsIn(1) ? 99u : 16u,
            write: differsIn(4) ? 5 : 1,
            change: differsIn(6) ? 9 : 3,
            read: differsIn(3) ? 5 : 0,
            readWrite: differsIn(5) ? 5 : 2,
            fileType: differsIn(0) ? 1 : 0,
            mode: differsIn(2) ? 0 : 3);

        private static void Untick(MifareDesfireSetupViewModel viewModel)
        {
            viewModel.IsFileTypeCompared = false;
            viewModel.IsFileSizeCompared = false;
            viewModel.IsFileCommunicationModeCompared = false;
            viewModel.IsFileReadRightCompared = false;
            viewModel.IsFileWriteRightCompared = false;
            viewModel.IsFileReadWriteRightCompared = false;
            viewModel.IsFileChangeRightCompared = false;
        }

        private static bool[] Ticks(MifareDesfireSetupViewModel viewModel) => new[]
        {
            viewModel.IsFileTypeCompared,
            viewModel.IsFileSizeCompared,
            viewModel.IsFileCommunicationModeCompared,
            viewModel.IsFileReadRightCompared,
            viewModel.IsFileWriteRightCompared,
            viewModel.IsFileReadWriteRightCompared,
            viewModel.IsFileChangeRightCompared,
        };

        // The text of a status line without the time stamp that starts it.
        private static string TextOf(string resourceKey) => ResourceLoader.GetResource(resourceKey).Replace("{0}: ", string.Empty);

        private static string Serialize(MifareDesfireSetupViewModel viewModel)
        {
            using var writer = new StringWriter();
            new XmlSerializer(typeof(MifareDesfireSetupViewModel)).Serialize(writer, viewModel);

            return writer.ToString();
        }

        private static MifareDesfireSetupViewModel Deserialize(string xml)
        {
            using var reader = new StringReader(xml);

            return (MifareDesfireSetupViewModel)new XmlSerializer(typeof(MifareDesfireSetupViewModel)).Deserialize(reader)!;
        }

        private static async Task Run(MifareDesfireSetupViewModel viewModel, FakeFileSettingsProvider reader)
        {
            using (ReaderDeviceScope.Use(reader))
            {
                await viewModel.CheckFileSettingsCommand();
            }
        }

        // A file as the task expects it, unless a row says otherwise.
        private static DESFireFileSettings Settings(uint size = 16, int write = 1, int change = 3, int read = 0, int readWrite = 2, int fileType = 0, int mode = 3)
        {
            return new DESFireFileSettings
            {
                FileType = (byte)fileType,
                comSett = (byte)mode,
                accessRights = new[] { (byte)(read | (write << 4)), (byte)(readWrite | (change << 4)) },
                dataFile = new DataFileSetting { fileSize = size }
            };
        }

        private static MifareDesfireSetupViewModel CreateViewModel()
        {
            return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.CheckFileSettings,
                AppNumberCurrent = "1",
                FileNumberCurrent = "2",
                FileSizeCurrent = "16",
                DesfireAppKeyCurrent = ZeroAesKey,
                SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                SelectedDesfireAppKeyNumberCurrent = "0",
                SelectedDesfireFileType = FileType_MifareDesfireFileType.StdDataFile,
                SelectedDesfireFileCryptoMode = EncryptionMode.CM_ENCRYPT,
                SelectedDesfireFileAccessRightRead = TaskAccessRights.AR_KEY0,
                SelectedDesfireFileAccessRightWrite = TaskAccessRights.AR_KEY1,
                SelectedDesfireFileAccessRightReadWrite = TaskAccessRights.AR_KEY2,
                SelectedDesfireFileAccessRightChange = TaskAccessRights.AR_KEY3
            };
        }
    }
}
