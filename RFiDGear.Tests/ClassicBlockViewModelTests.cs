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
    /// A block of a Classic sector that no key could read has no data, and the dialog shows nothing for it. What the dialog shows and what the
    /// user types for such a block must not throw, and typed data starts a block of its own.
    /// </summary>
    public class ClassicBlockViewModelTests
    {
        private static async Task OnTheDialogThread(System.Action<RFiDChipGrandChildLayerViewModel> check)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var block = new MifareClassicDataBlockModel(4, 0) { Data = new byte[16] };
                var viewModel = new RFiDChipGrandChildLayerViewModel(block, (MifareClassicSetupViewModel)null!);

                check(viewModel);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ABlockWithoutData_ShowsNothing_AndDoesNotThrow()
        {
            await OnTheDialogThread(viewModel =>
            {
                viewModel.MifareClassicDataBlock.Data = null;

                Assert.Equal(string.Empty, viewModel.DataAsHexString);
                Assert.Equal(string.Empty, viewModel.DataAsCharString);
            });
        }

        [Fact]
        public async Task ABlockThatWasRead_ShowsItsData()
        {
            await OnTheDialogThread(viewModel =>
            {
                viewModel.MifareClassicDataBlock.Data = Enumerable.Range(0x41, 16).Select(i => (byte)i).ToArray();

                Assert.Equal("4142434445464748494A4B4C4D4E4F50", viewModel.DataAsHexString);
                Assert.Equal("ABCDEFGHIJKLMNOP", viewModel.DataAsCharString);
            });
        }

        [Fact]
        public async Task TheDataOfABlockThatWasNotRead_ShowsAgain_WhenTheBlockIsRead()
        {
            await OnTheDialogThread(viewModel =>
            {
                viewModel.MifareClassicDataBlock.Data = null;
                Assert.Equal(string.Empty, viewModel.DataAsHexString);

                viewModel.MifareClassicDataBlock.Data = Enumerable.Repeat((byte)0x7A, 16).ToArray();

                Assert.Equal(string.Concat(Enumerable.Repeat("7A", 16)), viewModel.DataAsHexString);
            });
        }

        [Fact]
        public async Task HexTypedForABlockWithoutData_StartsABlockOfItsOwn()
        {
            await OnTheDialogThread(viewModel =>
            {
                viewModel.MifareClassicDataBlock.Data = null;

                viewModel.DataAsHexString = "00112233445566778899AABBCCDDEEFF";

                Assert.Equal(Enumerable.Range(0, 16).Select(i => (byte)(i * 17)).ToArray(), viewModel.MifareClassicDataBlock.Data);
                Assert.Equal("00112233445566778899AABBCCDDEEFF", viewModel.DataAsHexString);
            });
        }

        [Fact]
        public async Task CharactersTypedForABlockWithoutData_StartABlockOfTheirOwn()
        {
            await OnTheDialogThread(viewModel =>
            {
                viewModel.MifareClassicDataBlock.Data = null;

                viewModel.DataAsCharString = "ABCDEFGHIJKLMNOP";

                Assert.Equal(Enumerable.Range(0x41, 16).Select(i => (byte)i).ToArray(), viewModel.MifareClassicDataBlock.Data);
                Assert.Equal("ABCDEFGHIJKLMNOP", viewModel.DataAsCharString);
            });
        }

        [Fact]
        public async Task WhatIsTypedForABlockWithoutData_IsKeptAsTyped_UntilItIsAWholeBlock()
        {
            // The boxes refresh on every key: a text that is only a draft must still be there after the refresh, and it is no data.
            await OnTheDialogThread(viewModel =>
            {
                viewModel.ClearData();

                viewModel.DataAsHexString = "00";
                Assert.Equal("00", viewModel.DataAsHexString);
                viewModel.DataAsHexString = "0011";
                Assert.Equal("0011", viewModel.DataAsHexString);
                Assert.Null(viewModel.MifareClassicDataBlock.Data);

                viewModel.DataAsCharString = "A";
                Assert.Equal("A", viewModel.DataAsCharString);
                viewModel.DataAsCharString = "AB";
                Assert.Equal("AB", viewModel.DataAsCharString);
                Assert.Null(viewModel.MifareClassicDataBlock.Data);

                viewModel.DataAsHexString = "00112233445566778899AABBCCDDEEFF";
                Assert.NotNull(viewModel.MifareClassicDataBlock.Data);
                Assert.Equal("00112233445566778899AABBCCDDEEFF", viewModel.DataAsHexString);
            });
        }

        [Fact]
        public async Task ClearingABlock_EmptiesItAndWhatWasTypedForIt_AndItShowsItsDataAgainWhenItIsRead()
        {
            await OnTheDialogThread(viewModel =>
            {
                viewModel.ClearData();
                viewModel.DataAsHexString = "0011";
                viewModel.DataAsCharString = "AB";

                // Cleared again (a second read that finds it unreadable): the draft goes too.
                viewModel.ClearData();

                Assert.Null(viewModel.MifareClassicDataBlock.Data);
                Assert.Equal(string.Empty, viewModel.DataAsHexString);
                Assert.Equal(string.Empty, viewModel.DataAsCharString);

                // Read at last.
                viewModel.MifareClassicDataBlock.Data = Enumerable.Range(0x41, 16).Select(i => (byte)i).ToArray();

                Assert.Equal("4142434445464748494A4B4C4D4E4F50", viewModel.DataAsHexString);
                Assert.Equal("ABCDEFGHIJKLMNOP", viewModel.DataAsCharString);
            });
        }
    }
}
