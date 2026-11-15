using CommunityToolkit.Mvvm.Input;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.KeySlots;
using RFiDGear.Tests.SecureKeys;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What the dialog of an Ultralight task shows and does, apart from how it looks: the grid of pages (one row per page of the data
    /// of a write, one per page of a read, with what the last read gave), the rows that change the data of the task, what turns the
    /// key boxes on, and the two buttons that run the task by hand. The rows are a view of the task and no part of the project.
    /// </summary>
    public class UltralightDialogViewModelTests : IDisposable
    {
        public UltralightDialogViewModelTests()
        {
            KeyResolutionScope.SetDefault(null);
        }

        public void Dispose()
        {
            KeyResolutionScope.SetDefault(null);
        }

        private static MifareUltralightSetupViewModel Write(string hex, int start = 4, UltralightExpectation chip = UltralightExpectation.Auto) =>
            new MifareUltralightSetupViewModel
            {
                SelectedTaskType = TaskType_MifareUltralightTask.WriteData, CurrentTaskIndex = "0", StartPage = start, WriteDataHex = hex, ExpectedChip = chip,
            };

        private static MifareUltralightSetupViewModel Read(int start = 4, int count = 2, UltralightExpectation chip = UltralightExpectation.Auto) =>
            new MifareUltralightSetupViewModel
            {
                SelectedTaskType = TaskType_MifareUltralightTask.ReadData, CurrentTaskIndex = "0", StartPage = start, PageCount = count, ExpectedChip = chip,
            };

        private static KeyRecordingReader Reader() => new KeyRecordingReader { Connected = true };

        // ── The rows of a write ──

        [Fact]
        public async Task AWrite_HasOneRowPerPageOfItsData_NumberedFromTheStartPage()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D401020304", start: 6);

                Assert.Equal(new[] { 6, 7 }, task.PageRows.Select(row => row.PageNumber));
                Assert.Equal(new[] { "A1B2C3D4", "01020304" }, task.PageRows.Select(row => row.Hex));
                Assert.All(task.PageRows, row =>
                {
                    Assert.True(row.IsValid);
                    Assert.False(row.IsReadOnly);
                });
            });
        }

        [Fact]
        public async Task ATaskWithoutData_HasNoRows()
        {
            await StaTestRunner.RunOnStaThreadAsync(() => Assert.Empty(Write(string.Empty).PageRows));
        }

        [Fact]
        public async Task ARowThatIsEdited_ChangesTheDataOfTheTask()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D401020304");

                task.PageRows[1].Hex = "ff 00 ff 00";

                // Typed as it comes: no white space, upper case, as the task keeps it.
                Assert.Equal("FF00FF00", task.PageRows[1].Hex);
                Assert.Equal("A1B2C3D4FF00FF00", task.WriteDataHex);
                Assert.Equal(2, task.PageRows.Count);
            });
        }

        [Theory]
        [InlineData("A1B", false)]
        [InlineData("A1B2C3D4", true)]
        [InlineData("A1B2C3D4E", false)]
        [InlineData("A1B2C3DZ", false)]
        [InlineData("", false)]
        public async Task ARow_IsValid_WhenItHasEightHexadecimalDigits(string hex, bool valid)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("00000000");

                task.PageRows[0].Hex = hex;

                Assert.Equal(valid, task.PageRows[0].IsValid);
            });
        }

        [Fact]
        public async Task AddingAPage_AppendsAnEmptyRowThatIsNotValid_AndKeepsTheData()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4");

                Assert.True(task.AddPageCommand.CanExecute(null));
                task.AddPageCommand.Execute(null);

                Assert.Equal(new[] { 4, 5 }, task.PageRows.Select(row => row.PageNumber));
                Assert.Equal("A1B2C3D4", task.PageRows[0].Hex);
                Assert.Equal(string.Empty, task.PageRows[1].Hex);
                Assert.False(task.PageRows[1].IsValid);

                // A grid with a row that is not whole is no data that a run takes: it has the mark of the rows in front of it.
                Assert.StartsWith("|", task.WriteDataHex, StringComparison.Ordinal);
                Assert.False(UltralightPayload.TryParse(task.WriteDataHex, out _));

                // And it is typed into like any other.
                task.PageRows[1].Hex = "01020304";
                Assert.Equal("A1B2C3D401020304", task.WriteDataHex);
            });
        }

        // ── What the grid shows is what is written ──

        [Fact]
        public async Task TwoRowsThatAreNotWhole_AreNotOneWholePageOfData()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4");
                task.PageRows[0].Hex = "A1B2";
                task.AddPageCommand.Execute(null);
                task.PageRows[1].Hex = "C3D4";

                Assert.Equal(new bool?[] { false, false }, task.PageRows.Select(row => row.IsValid));

                // Joined, the two rows would be A1B2C3D4: a whole page that the grid does not show.
                Assert.False(UltralightPayload.TryParse(task.WriteDataHex, out _));
                Assert.Equal(new[] { "A1B2", "C3D4" }, task.PageRows.Select(row => row.Hex));
            });
        }

        [Fact]
        public async Task AnEmptyRowBeforeAFullOne_DoesNotMoveTheDataUpAPage()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write(string.Empty, start: 6);
                task.AddPageCommand.Execute(null);
                task.AddPageCommand.Execute(null);
                task.PageRows[1].Hex = "A1B2C3D4";

                // The grid shows page 6 empty and page 7 full. The data must not be the one page A1B2C3D4, which a run would write to page 6.
                Assert.Equal(new[] { 6, 7 }, task.PageRows.Select(row => row.PageNumber));
                Assert.Equal(new[] { string.Empty, "A1B2C3D4" }, task.PageRows.Select(row => row.Hex));
                Assert.False(UltralightPayload.TryParse(task.WriteDataHex, out _));
            });
        }

        [Fact]
        public async Task ARowThatIsNotWhole_IsKeptAsItIs_WhenTheTypeChanges_WhenTheTaskIsCopied_AndWhenTheDataIsSetAgain()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4", start: 6);
                task.PageRows[0].Hex = "A1B2";
                task.AddPageCommand.Execute(null);
                task.AddPageCommand.Execute(null);
                task.PageRows[2].Hex = "C3D4";
                var shown = task.PageRows.Select(row => (row.PageNumber, row.Hex)).ToList();

                // The type changes there and back.
                task.SelectedTaskType = TaskType_MifareUltralightTask.ReadData;
                task.SelectedTaskType = TaskType_MifareUltralightTask.WriteData;
                Assert.Equal(shown, task.PageRows.Select(row => (row.PageNumber, row.Hex)));

                // The dialog copies the task (the project that is saved and loaded sets the same text).
                var editor = new MifareUltralightSetupViewModel(task, new ObservableCollection<IDialogViewModel>());
                Assert.Equal(shown, editor.PageRows.Select(row => (row.PageNumber, row.Hex)));

                var loaded = Write(string.Empty, start: 6);
                loaded.WriteDataHex = task.WriteDataHex;
                Assert.Equal(shown, loaded.PageRows.Select(row => (row.PageNumber, row.Hex)));
                Assert.False(UltralightPayload.TryParse(loaded.WriteDataHex, out _));
            });
        }

        [Fact]
        public async Task WhenEveryRowIsWholeAgain_TheDataIsThePlainDigitsAgain()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4");
                task.AddPageCommand.Execute(null);
                Assert.StartsWith("|", task.WriteDataHex, StringComparison.Ordinal);

                task.PageRows[1].Hex = "01020304";

                Assert.Equal("A1B2C3D401020304", task.WriteDataHex);
                Assert.True(UltralightPayload.TryParse(task.WriteDataHex, out var data));
                Assert.Equal(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4, 1, 2, 3, 4 }, data);

                // And removing the row that is not whole has the same effect.
                task.AddPageCommand.Execute(null);
                Assert.StartsWith("|", task.WriteDataHex, StringComparison.Ordinal);
                task.RemovePageCommand.Execute(task.PageRows[2]);
                Assert.Equal("A1B2C3D401020304", task.WriteDataHex);
            });
        }

        [Fact]
        public async Task TheMarkOfTheRows_CannotBeTypedIntoARow()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4");

                task.PageRows[0].Hex = "A|1";

                Assert.Equal("A1", task.PageRows[0].Hex);
                Assert.DoesNotContain("|", task.PageRows[0].Hex, StringComparison.Ordinal);
            });
        }

        [Fact]
        public async Task AButtonThatWritesAGridWithARowThatIsNotWhole_WritesNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = Write("A1B2C3D4", start: 9);
                task.PageRows[0].Hex = "A1B2";
                task.AddPageCommand.Execute(null);
                task.PageRows[1].Hex = "C3D4";
                var reader = Reader();

                using (ReaderDeviceScope.Use(reader))
                {
                    await task.WriteDataCommand.ExecuteAsync(null);
                }

                Assert.Empty(reader.UltralightCalls);
                Assert.Equal(ERROR.ProtocolConstraint, task.CurrentTaskErrorLevel);
            });
        }

        [Fact]
        public async Task RemovingAPage_RenumbersTheRowsBehindIt_AndShortensTheData()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4" + "01020304" + "FFEEDDCC");

                task.RemovePageCommand.Execute(task.PageRows[1]);

                Assert.Equal(new[] { 4, 5 }, task.PageRows.Select(row => row.PageNumber));
                Assert.Equal(new[] { "A1B2C3D4", "FFEEDDCC" }, task.PageRows.Select(row => row.Hex));
                Assert.Equal("A1B2C3D4FFEEDDCC", task.WriteDataHex);
            });
        }

        [Fact]
        public async Task ARowThatIsNotOfTheGrid_IsIgnoredByRemove()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4");
                var other = Write("01020304").PageRows[0];

                task.RemovePageCommand.Execute(other);
                task.RemovePageCommand.Execute(null);

                Assert.Single(task.PageRows);
                Assert.Equal("A1B2C3D4", task.WriteDataHex);
            });
        }

        [Fact]
        public async Task ChangingTheStartPage_RenumbersTheRows_AndKeepsTheData()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D401020304");

                task.StartPage = 10;

                Assert.Equal(new[] { 10, 11 }, task.PageRows.Select(row => row.PageNumber));
                Assert.Equal("A1B2C3D401020304", task.WriteDataHex);
            });
        }

        [Fact]
        public async Task ChangingTheDataOfTheTask_ChangesTheRows()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4");

                task.WriteDataHex = "01020304 05060708 090A0B0C";

                Assert.Equal(new[] { "01020304", "05060708", "090A0B0C" }, task.PageRows.Select(row => row.Hex));
            });
        }

        [Fact]
        public async Task DataThatIsNoWholePages_ShowsTheLastRowAsNotValid()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4A1B");

                Assert.Equal(new bool?[] { true, false }, task.PageRows.Select(row => row.IsValid));
            });
        }

        // ── How many pages the grid can have ──

        [Fact]
        public async Task TheGrid_CannotHaveMorePagesThanTheNamedChipHas()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                // An Ultralight has 16 pages; from page 4 that is 12 rows.
                var task = Write(string.Concat(Enumerable.Repeat("00000000", 11)), chip: UltralightExpectation.Plain);

                Assert.True(task.AddPageCommand.CanExecute(null));
                task.AddPageCommand.Execute(null);
                Assert.Equal(12, task.PageRows.Count);
                Assert.False(task.AddPageCommand.CanExecute(null));

                task.AddPageCommand.Execute(null);
                Assert.Equal(12, task.PageRows.Count);
            });
        }

        [Fact]
        public async Task WithoutANamedChip_TheGridCanHaveThePagesOfTheBiggestChip()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write(string.Empty);

                while (task.AddPageCommand.CanExecute(null))
                {
                    task.AddPageCommand.Execute(null);
                }

                // 231 pages of an NTAG216, from page 4.
                Assert.Equal(231 - 4, task.PageRows.Count);
            });
        }

        [Fact]
        public async Task TheRowsOfARead_AreCappedAtThePagesOfTheBiggestChip()
        {
            await StaTestRunner.RunOnStaThreadAsync(() => Assert.Equal(231, Read(start: 0, count: 100000).PageRows.Count));
        }

        [Fact]
        public async Task AddingAPage_IsOffForARead()
        {
            await StaTestRunner.RunOnStaThreadAsync(() => Assert.False(Read().AddPageCommand.CanExecute(null)));
        }

        // ── The rows of a read ──

        [Fact]
        public async Task ARead_HasOneEmptyRowPerPageAskedFor_ThatCannotBeEdited()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Read(start: 8, count: 3);

                Assert.Equal(new[] { 8, 9, 10 }, task.PageRows.Select(row => row.PageNumber));
                Assert.All(task.PageRows, row =>
                {
                    Assert.Equal(string.Empty, row.Hex);
                    Assert.True(row.IsReadOnly);
                    Assert.Null(row.IsValid);
                });
            });
        }

        [Fact]
        public async Task AfterARead_TheRowsShowWhatTheChipGave()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = Read();
                var reader = Reader();

                using (ReaderDeviceScope.Use(reader))
                {
                    await ((IAsyncRelayCommand)task.ReadDataCommand).ExecuteAsync(null);
                }

                // The reader of the test answers with the position of each byte: page 4 holds the bytes 16 to 19.
                Assert.Equal(new[] { "10111213", "14151617" }, task.PageRows.Select(row => row.Hex));
                Assert.Equal(ERROR.NoError, task.CurrentTaskErrorLevel);
            });
        }

        [Fact]
        public async Task ARowOutsideTheLastRead_ShowsNothing_AndAChangeOfThePageCountKeepsTheRest()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = Read();

                using (ReaderDeviceScope.Use(Reader()))
                {
                    await ((IAsyncRelayCommand)task.ReadDataCommand).ExecuteAsync(null);
                }

                task.PageCount = 3;

                Assert.Equal(new[] { "10111213", "14151617", string.Empty }, task.PageRows.Select(row => row.Hex));

                task.StartPage = 5;

                // The bytes belong to the pages they were read from: page 5 is the second of the read, and nothing was read of 6 and 7.
                Assert.Equal(new[] { "14151617", string.Empty, string.Empty }, task.PageRows.Select(row => row.Hex));
            });
        }

        [Fact]
        public async Task ARead_ThatStopsOnTheWay_ShowsThePagesThatWereRead()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = Read(count: 3);
                var reader = Reader();
                reader.ScriptedUltralightResult = new UltralightResult(ERROR.PermissionDenied, new byte[] { 1, 2, 3, 4 }, UltralightVariant.Ntag215, false, 1, 5);

                using (ReaderDeviceScope.Use(reader))
                {
                    await ((IAsyncRelayCommand)task.ReadDataCommand).ExecuteAsync(null);
                }

                Assert.Equal(new[] { "01020304", string.Empty, string.Empty }, task.PageRows.Select(row => row.Hex));
                Assert.Equal(ERROR.PermissionDenied, task.CurrentTaskErrorLevel);
            });
        }

        [Fact]
        public async Task ChangingTheTaskType_ChangesTheRows()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4");
                task.PageCount = 3;

                task.SelectedTaskType = TaskType_MifareUltralightTask.ReadData;
                Assert.Equal(3, task.PageRows.Count);
                Assert.All(task.PageRows, row => Assert.True(row.IsReadOnly));

                task.SelectedTaskType = TaskType_MifareUltralightTask.WriteData;
                Assert.Equal(new[] { "A1B2C3D4" }, task.PageRows.Select(row => row.Hex));
                Assert.All(task.PageRows, row => Assert.False(row.IsReadOnly));

                task.SelectedTaskType = TaskType_MifareUltralightTask.None;
                Assert.Empty(task.PageRows);
            });
        }

        // ── The rows are no part of the project ──

        [Fact]
        public void TheRows_AreNoPartOfTheProject()
        {
            foreach (var name in new[] { nameof(MifareUltralightSetupViewModel.PageRows), nameof(MifareUltralightSetupViewModel.IsWriteTask), nameof(MifareUltralightSetupViewModel.IsReadTask), nameof(MifareUltralightSetupViewModel.UsesUltralightCKey), nameof(MifareUltralightSetupViewModel.UsesPassword) })
            {
                var property = typeof(MifareUltralightSetupViewModel).GetProperty(name);

                Assert.NotNull(property);
                Assert.NotNull(property.GetCustomAttribute<XmlIgnoreAttribute>());
            }
        }

        [Fact]
        public async Task ADialogThatEditsATask_StartsWithTheRowsOfTheTask_AndTheTaskIsNotChangedByIt()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var original = Write("A1B2C3D401020304", start: 8);
                original.Authentication = UltralightAuth.UltralightC;
                original.ExpectedChip = UltralightExpectation.C;

                var editor = new MifareUltralightSetupViewModel(original, new ObservableCollection<IDialogViewModel>());

                Assert.Equal(new[] { 8, 9 }, editor.PageRows.Select(row => row.PageNumber));
                Assert.Equal(new[] { "A1B2C3D4", "01020304" }, editor.PageRows.Select(row => row.Hex));

                editor.PageRows[0].Hex = "FFFFFFFF";
                editor.RemovePageCommand.Execute(editor.PageRows[1]);

                Assert.Equal("FFFFFFFF", editor.WriteDataHex);
                Assert.Equal("A1B2C3D401020304", original.WriteDataHex);
                Assert.Equal(2, original.PageRows.Count);
            });
        }

        [Fact]
        public async Task ADialogThatEditsATask_StartsWithEverythingTheTaskHolds()
        {
            // The dialog copies the task property by property. A property that throws stops the copy at that property, and everything
            // declared behind it stays at its default: the dialog opens empty, and OK writes the defaults over the task.
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var original = new MifareUltralightSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareUltralightTask.ReadData,
                    CurrentTaskIndex = "3",
                    SelectedTaskDescription = "read the card",
                    SelectedExecuteConditionTaskIndex = "2",
                    SelectedExecuteConditionErrorLevel = ERROR.PermissionDenied,
                    StartPage = 12,
                    PageCount = 5,
                    ExpectedChip = UltralightExpectation.Ntag215,
                    Authentication = UltralightAuth.Password,
                    AllowProtectedPages = true,
                    UltralightPassword = "A1B2C3D4",
                    UltralightPack = "5566",
                };
                original.BindKeySlot(SecureKeySlots.UltralightCKey, "provider", "ref/c");

                var editor = new MifareUltralightSetupViewModel(original, new ObservableCollection<IDialogViewModel>());

                Assert.Equal(TaskType_MifareUltralightTask.ReadData, editor.SelectedTaskType);
                Assert.Equal("3", editor.CurrentTaskIndex);
                Assert.Equal("read the card", editor.SelectedTaskDescription);
                Assert.Equal("2", editor.SelectedExecuteConditionTaskIndex);
                Assert.Equal(ERROR.PermissionDenied, editor.SelectedExecuteConditionErrorLevel);
                Assert.Equal(12, editor.StartPage);
                Assert.Equal(5, editor.PageCount);
                Assert.Equal(UltralightExpectation.Ntag215, editor.ExpectedChip);
                Assert.Equal(UltralightAuth.Password, editor.Authentication);
                Assert.True(editor.AllowProtectedPages);
                Assert.Equal("A1B2C3D4", editor.UltralightPassword);
                Assert.Equal("5566", editor.UltralightPack);
                Assert.True(editor.IsKeyBound(SecureKeySlots.UltralightCKey));
                Assert.Equal(5, editor.PageRows.Count);
            });
        }

        // ── What turns the key boxes on ──

        [Theory]
        [InlineData(UltralightAuth.None, false, false)]
        [InlineData(UltralightAuth.UltralightC, true, false)]
        [InlineData(UltralightAuth.Password, false, true)]
        public async Task TheKeyBoxesOfTheAuthenticationInUse_AreOn_AndTheOthersAreOff(UltralightAuth auth, bool usesKey, bool usesPassword)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Read();
                var changes = new System.Collections.Generic.List<string>();
                task.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

                task.Authentication = auth;

                Assert.Equal(usesKey, task.UsesUltralightCKey);
                Assert.Equal(usesPassword, task.UsesPassword);

                if (auth != UltralightAuth.None)
                {
                    Assert.Contains(nameof(MifareUltralightSetupViewModel.UsesUltralightCKey), changes);
                    Assert.Contains(nameof(MifareUltralightSetupViewModel.UsesPassword), changes);
                }
            });
        }

        [Theory]
        [InlineData(TaskType_MifareUltralightTask.ReadData, true, false)]
        [InlineData(TaskType_MifareUltralightTask.WriteData, false, true)]
        [InlineData(TaskType_MifareUltralightTask.None, false, false)]
        public async Task ThePagesOfAReadAndOfAWrite_AreToldApart(TaskType_MifareUltralightTask type, bool read, bool write)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareUltralightSetupViewModel { SelectedTaskType = type };

                Assert.Equal(read, task.IsReadTask);
                Assert.Equal(write, task.IsWriteTask);
            });
        }

        // ── The two buttons that run the task by hand ──

        [Fact]
        public async Task TheButtons_AreOn_WhenNothingRuns()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = Write("A1B2C3D4");

                Assert.True(task.ReadDataCommand.CanExecute(null));
                Assert.True(task.WriteDataCommand.CanExecute(null));

                // The same command each time, so that what listens to it is told.
                Assert.Same(task.ReadDataCommand, task.ReadDataCommand);
                Assert.Same(task.WriteDataCommand, task.WriteDataCommand);
            });
        }

        [Fact]
        public async Task TheButtons_AreBothOff_WhileAnOperationRuns_AndTheirListenersAreTold()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var provider = new BlockingKeyProvider(16);
                KeyResolutionScope.SetDefault(new KeyResolutionContext(provider.Registry(), "run-1", "0", null, interactive: false));
                var task = Read(chip: UltralightExpectation.C);
                task.Authentication = UltralightAuth.UltralightC;
                task.KeyBindings = new[] { new KeyBindingEntry { Slot = SecureKeySlots.UltralightCKey, ProviderId = BlockingKeyProvider.Id, Reference = "ref/c" } };
                var read = (IAsyncRelayCommand)task.ReadDataCommand;
                var readChanged = 0;
                var writeChanged = 0;
                read.CanExecuteChanged += (_, _) => readChanged++;
                task.WriteDataCommand.CanExecuteChanged += (_, _) => writeChanged++;

                using (ReaderDeviceScope.Use(Reader()))
                {
                    var running = read.ExecuteAsync(null);
                    Assert.Same(provider.Asked, await Task.WhenAny(provider.Asked, Task.Delay(TimeSpan.FromSeconds(10))));

                    Assert.False(read.CanExecute(null));
                    Assert.False(task.WriteDataCommand.CanExecute(null));
                    Assert.True(readChanged > 0);
                    Assert.True(writeChanged > 0);

                    provider.Release();
                    await running;
                }

                Assert.True(read.CanExecute(null));
                Assert.True(task.WriteDataCommand.CanExecute(null));
            });
        }

        [Fact]
        public async Task AButtonOfTheDialog_RunsTheTaskAsItIsShown()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // What the rows show is what the task holds, so what the button writes is what the person sees.
                var task = Write("A1B2C3D4", start: 9);
                task.PageRows[0].Hex = "01020304";
                var reader = Reader();

                using (ReaderDeviceScope.Use(reader))
                {
                    await ((IAsyncRelayCommand)task.WriteDataCommand).ExecuteAsync(null);
                }

                var call = Assert.Single(reader.UltralightCalls);
                Assert.Equal(nameof(KeyRecordingReader.WriteMifareUltralightPagesAsync), call.Method);
                Assert.Equal(9, call.StartPage);
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, call.Data);
            });
        }
    }
}
