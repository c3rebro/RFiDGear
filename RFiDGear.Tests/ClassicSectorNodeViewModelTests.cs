using System;
using System.Linq;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Models;
using RFiDGear.ViewModel;
using RFiDGear.ViewModel.TaskSetupViewModels;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The node of a sector in the dialogs: it has one node per block of the sector it stands for, so a sector of a 4K from 32 on (16 blocks)
    /// does not find 4 nodes when its blocks are shown, and a node that is no block is not lost when the blocks are made new.
    /// </summary>
    public class ClassicSectorNodeViewModelTests
    {
        private static Task OnTheDialogThread(CARD_TYPE cardType, Action<RFiDChipChildLayerViewModel> check) =>
            StaTestRunner.RunOnStaThreadAsync(() =>
            {
                check(new RFiDChipChildLayerViewModel(new MifareClassicSectorModel(4), null, cardType, null, true));

                return Task.CompletedTask;
            });

        [Fact]
        public async Task ASectorOfA4K_FromSector32On_HasSixteenBlockNodes()
        {
            await OnTheDialogThread(CARD_TYPE.Mifare4K, node =>
            {
                Assert.Equal(4, node.Children.Count);

                node.SectorNumber = 33;

                Assert.Equal(16, node.Children.Count);
                Assert.Equal(Enumerable.Range(0, 16), node.Children.Select(child => child.MifareClassicDataBlock.DataBlockNumberSectorBased));
            });
        }

        [Fact]
        public async Task ASectorBackBelow32_HasFourBlockNodesAgain()
        {
            await OnTheDialogThread(CARD_TYPE.Mifare4K, node =>
            {
                node.SectorNumber = 39;
                node.SectorNumber = 5;

                Assert.Equal(4, node.Children.Count);
                Assert.Equal(Enumerable.Range(0, 4), node.Children.Select(child => child.MifareClassicDataBlock.DataBlockNumberSectorBased));
            });
        }

        [Fact]
        public async Task ASectorOfTheSameKind_KeepsItsNodes()
        {
            await OnTheDialogThread(CARD_TYPE.Mifare4K, node =>
            {
                var before = node.Children.ToList();

                node.SectorNumber = 6;

                Assert.Equal(before, node.Children);

                node.SectorNumber = 35;

                var large = node.Children.ToList();

                node.SectorNumber = 38;

                Assert.Equal(large, node.Children);
            });
        }

        [Fact]
        public async Task ANodeThatIsNoBlock_IsKept_WhenTheBlocksAreMadeNew()
        {
            await OnTheDialogThread(CARD_TYPE.Mifare4K, node =>
            {
                var key = new RFiDChipGrandChildLayerViewModel("Key: FFFFFFFFFFFF");

                node.Children.Add(key);
                node.SectorNumber = 34;

                Assert.Contains(key, node.Children);
                Assert.Equal(16, node.Children.Count(child => child.MifareClassicDataBlock != null));

                // The nodes of the blocks come first, by the number of the block, and the key after them: the sector is read by index.
                Assert.All(node.Children.Take(16), child => Assert.NotNull(child.MifareClassicDataBlock));
                Assert.Equal(Enumerable.Range(0, 16), node.Children.Take(16).Select(child => child.MifareClassicDataBlock.DataBlockNumberSectorBased));
                Assert.Same(key, node.Children[16]);
            });
        }

        [Fact]
        public async Task ANodeThatShowsNoBlocks_IsLeftAsItIs_WhenTheSectorChanges()
        {
            // The dialog of a MAD holds the node of the application and no node of a block: a new sector must not put blocks in front of it.
            await OnTheDialogThread(CARD_TYPE.Mifare4K, node =>
            {
                var application = new RFiDChipGrandChildLayerViewModel("Application");

                node.Children.Clear();
                node.Children.Add(application);

                node.SectorNumber = 33;

                Assert.Single(node.Children);
                Assert.Same(application, node.Children[0]);
            });
        }

        [Fact]
        public async Task TwoNodesThatShareTheModel_BothGetTheNodesOfTheirSector_AndBothGetThemBack()
        {
            // The dialog has one node for the chip and one for the task, and they stand for the same sector model: the sector is changed on the model
            // (the selection of the dialog does that) before either node is told, so each node has to look at its own nodes.
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var model = new MifareClassicSectorModel(4);
                var fromChip = new RFiDChipChildLayerViewModel(model, null, CARD_TYPE.Mifare4K, null, true);
                var temp = new RFiDChipChildLayerViewModel(model, null, CARD_TYPE.Mifare4K, null, true);

                model.SectorNumber = 33;
                fromChip.SectorNumber = 33;
                temp.SectorNumber = 33;

                Assert.Equal(16, fromChip.Children.Count);
                Assert.Equal(16, temp.Children.Count);

                model.SectorNumber = 4;
                fromChip.SectorNumber = 4;
                temp.SectorNumber = 4;

                Assert.Equal(4, fromChip.Children.Count);
                Assert.Equal(4, temp.Children.Count);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task SelectingASectorOfFourBlocksAfterOneOfSixteen_MakesTheNodesOfTheDialogRightAtOnce()
        {
            // What is typed after the selection goes into a node of a block that the sector has: the nodes are not made new when the command runs.
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareClassicSetupViewModel();

                task.SelectedClassicSectorCurrent = "33";

                Assert.Equal(16, task.ChildNodeViewModelFromChip.Children.Count);
                Assert.Equal(16, task.ChildNodeViewModelTemp.Children.Count);

                task.SelectedClassicSectorCurrent = "4";

                Assert.Equal(4, task.ChildNodeViewModelFromChip.Children.Count);
                Assert.Equal(4, task.ChildNodeViewModelTemp.Children.Count);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ANodeThatTheSectorWasChangedUnderneath_IsMadeRightWhenItIsToldTheSameNumber()
        {
            await OnTheDialogThread(CARD_TYPE.Mifare4K, node =>
            {
                node.SectorModel.SectorNumber = 35;

                // The number is the one that the model has: the nodes are still those of a sector of 4 blocks.
                node.SectorNumber = 35;

                Assert.Equal(16, node.Children.Count);
            });
        }

        [Fact]
        public async Task AChipThatHasNoLargeSectors_KeepsFourNodes_WhateverTheNumber()
        {
            await OnTheDialogThread(CARD_TYPE.Mifare1K, node =>
            {
                node.SectorNumber = 33;

                Assert.Equal(4, node.Children.Count);
            });
        }

        [Fact]
        public async Task ANegativeNumber_ChangesNothing()
        {
            await OnTheDialogThread(CARD_TYPE.Mifare4K, node =>
            {
                var before = node.Children.ToList();

                node.SectorNumber = -1;

                Assert.Equal(4, node.SectorNumber);
                Assert.Equal(before, node.Children);
            });
        }
    }
}
