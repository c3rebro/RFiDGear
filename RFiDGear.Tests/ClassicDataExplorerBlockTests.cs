using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Tests.KeySlots;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The block selection of the MIFARE Classic Data Explorer ("Block 0", "Block 1", "Block 2", "Sector Trailer"). The trailer is
    /// the last block of the sector: block 3 below sector 32, block 15 from sector 32 on. The selection used to be taken as the
    /// index 3 in every sector, which in the sectors of 16 blocks is a data block: the real trailer (keys and access bits) of
    /// sectors 32 to 39 could not be selected, and "Sector Trailer" wrote the trailer's bytes into data block 3.
    /// </summary>
    public class ClassicDataExplorerBlockTests
    {
        // The transport configuration of a trailer: key A, the access bits FF078069 (consistent), key B.
        private static readonly byte[] Trailer =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0x80, 0x69, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
        };

        private static MifareClassicSetupViewModel NewViewModel(string sector) => new MifareClassicSetupViewModel
        {
            SelectedTaskType = TaskType_MifareClassicTask.WriteData,
            CurrentTaskIndex = "0",
            SelectedClassicSectorCurrent = sector,
            ClassicKeyAKeyCurrent = "111111111111",
            ClassicKeyBKeyCurrent = "222222222222"
        };

        [Theory]
        [InlineData(0, DataExplorer_DataBlock.Block3, 3)]
        [InlineData(1, DataExplorer_DataBlock.Block3, 3)]
        [InlineData(31, DataExplorer_DataBlock.Block3, 3)]
        [InlineData(32, DataExplorer_DataBlock.Block3, 15)]
        [InlineData(39, DataExplorer_DataBlock.Block3, 15)]
        [InlineData(32, DataExplorer_DataBlock.Block0, 0)]
        [InlineData(32, DataExplorer_DataBlock.Block2, 2)]
        [InlineData(5, DataExplorer_DataBlock.Block1, 1)]
        [InlineData(-1, DataExplorer_DataBlock.Block3, 3)]
        [InlineData(40, DataExplorer_DataBlock.Block3, 3)]
        [InlineData(99, DataExplorer_DataBlock.Block3, 3)]
        public void TheSelection_NamesTheBlockOfTheSectorThatItSays(int sector, DataExplorer_DataBlock selection, int expected)
        {
            Assert.Equal(expected, RFiDGear.Infrastructure.Classic.DataExplorerBlocks.IndexInSector(selection, sector));
        }

        [Theory]
        [InlineData("1", DataExplorer_DataBlock.Block3, 7, 3)]
        [InlineData("31", DataExplorer_DataBlock.Block3, 127, 3)]
        [InlineData("32", DataExplorer_DataBlock.Block3, 143, 15)]
        [InlineData("39", DataExplorer_DataBlock.Block3, 255, 15)]
        [InlineData("32", DataExplorer_DataBlock.Block0, 128, 0)]
        [InlineData("32", DataExplorer_DataBlock.Block2, 130, 2)]
        [InlineData("1", DataExplorer_DataBlock.Block1, 5, 1)]
        public async Task TheSelectedBlock_IsWrittenAtTheChipBlockThatItNames(string sector, DataExplorer_DataBlock selection, int chipBlock, int index)
        {
            var recording = new KeyRecordingReader();
            ERROR error = ERROR.Empty;

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vm = NewViewModel(sector);
                vm.SelectedDataBlockToReadWrite = selection;
                vm.ChildNodeViewModelTemp.Children[index].MifareClassicDataBlock.Data = Trailer.ToArray();

                using (ReaderDeviceScope.Use(recording))
                {
                    await vm.CommandDelegator.ExecuteAsync(TaskType_MifareClassicTask.WriteData);
                }

                error = vm.CurrentTaskErrorLevel;
            });

            var write = Assert.Single(recording.BlockWrites);
            Assert.Equal(chipBlock, write.Block);
            Assert.Equal(Trailer, write.Data);
            Assert.Equal(ERROR.NoError, error);
        }

        [Theory]
        [InlineData("1", DataExplorer_DataBlock.Block3, 3)]
        [InlineData("31", DataExplorer_DataBlock.Block3, 3)]
        [InlineData("32", DataExplorer_DataBlock.Block3, 15)]
        [InlineData("39", DataExplorer_DataBlock.Block3, 15)]
        [InlineData("32", DataExplorer_DataBlock.Block1, 1)]
        public async Task TheFocus_IsOnTheRowOfTheSelectedBlock(string sector, DataExplorer_DataBlock selection, int expectedRow)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var vm = NewViewModel(sector);
                vm.SelectedDataBlockToReadWrite = selection;

                var focused = Assert.Single(vm.ChildNodeViewModelTemp.Children, row => row.IsFocused);
                Assert.Equal(expectedRow, focused.DataBlockNumber);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task SwitchingToASectorOf16Blocks_TellsTheViewThatTheRowsAreNew()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var vm = NewViewModel("1");
                var changed = new System.Collections.Generic.List<string>();
                vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

                vm.SelectedClassicSectorCurrent = "32";

                // The grids are bound to these two nodes; they are new objects for a sector of 16 blocks.
                Assert.Contains(nameof(vm.ChildNodeViewModelTemp), changed);
                Assert.Contains(nameof(vm.ChildNodeViewModelFromChip), changed);
                Assert.Equal(16, vm.ChildNodeViewModelTemp.Children.Count);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task TheFocusOnTheTrailer_FollowsTheSectorFromFourBlocksToSixteenAndBack()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var vm = NewViewModel("1");
                vm.SelectedDataBlockToReadWrite = DataExplorer_DataBlock.Block3;

                vm.SelectedClassicSectorCurrent = "32";
                Assert.Equal(15, Assert.Single(vm.ChildNodeViewModelTemp.Children, row => row.IsFocused).DataBlockNumber);

                vm.SelectedClassicSectorCurrent = "2";
                Assert.Equal(3, Assert.Single(vm.ChildNodeViewModelTemp.Children, row => row.IsFocused).DataBlockNumber);

                return Task.CompletedTask;
            });
        }
    }
}
