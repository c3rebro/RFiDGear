using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Models;
using RFiDGear.Tests.KeySlots;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The chip tree of the main window for a chip of the Ultralight family: one node per page of the chip's layout (an Ultralight has
    /// 16 pages, not 15; an NTAG215 has 135), the pages that a quick check reads with the page operations of the readers, and no pages
    /// for a chip whose layout is not known.
    /// </summary>
    public class UltralightChipTreeTests
    {
        private const string Uid = "04AABBCCDDEEFF";

        private static RFiDChipParentLayerViewModel NodeOf(CARD_TYPE type) =>
            MainWindowViewModel.CreateParentNode(new GenericChipModel(Uid, type), new ObservableCollection<IDialogViewModel>());

        [Theory]
        [InlineData(CARD_TYPE.MifareUltralight, 16)]
        [InlineData(CARD_TYPE.MifareUltralightC, 48)]
        [InlineData(CARD_TYPE.MifareUltralight_EV1_MF0UL11, 20)]
        [InlineData(CARD_TYPE.MifareUltralight_EV1_MF0UL21, 41)]
        [InlineData(CARD_TYPE.NTAG_213, 45)]
        [InlineData(CARD_TYPE.NTAG_215, 135)]
        [InlineData(CARD_TYPE.NTAG_216, 231)]
        public async Task TheNodeOfAChip_HasOneChildPerPageOfItsLayout(CARD_TYPE type, int pages)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var node = NodeOf(type);

                Assert.NotNull(node);
                Assert.Equal(type, node.CardType);
                Assert.Equal(Enumerable.Range(0, pages), node.Children.Select(child => child.PageNumber));
            });
        }

        [Theory]
        [InlineData(CARD_TYPE.MifareUltralight_EV1)]
        [InlineData(CARD_TYPE.NTAG_210)]
        [InlineData(CARD_TYPE.NTAG_212)]
        [InlineData(CARD_TYPE.NTAG_424)]
        [InlineData(CARD_TYPE.EM4102)]
        public async Task AChipWithoutAKnownLayout_IsInTheTreeAndHasNoPages(CARD_TYPE type)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var node = NodeOf(type);

                Assert.NotNull(node);
                Assert.Equal(type, node.CardType);
                Assert.Empty(node.Children);
            });
        }

        [Fact]
        public async Task ANodeForNoTag_IsNoNode()
        {
            await StaTestRunner.RunOnStaThreadAsync(() => Assert.Null(NodeOf(CARD_TYPE.NOTAG)));
        }

        [Theory]
        [InlineData(CARD_TYPE.MifareUltralight)]
        [InlineData(CARD_TYPE.NTAG_215)]
        public async Task APageOfTheTree_IsNamedByItsNumber(CARD_TYPE type)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var node = NodeOf(type);

                Assert.Equal("Page: 0", node.Children[0].ChildNodeHeader);
                Assert.Equal("Page: 7", node.Children[7].ChildNodeHeader);
            });
        }

        // ── The quick check ──

        [Fact]
        public async Task TheQuickCheck_ReadsAllPagesOfTheChipInOneCall_AndShowsTheBytesOfEachPage()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var node = NodeOf(CARD_TYPE.NTAG_215);
                var reader = new KeyRecordingReader();

                await node.ReadUltralightPagesAsync(reader);

                var call = Assert.Single(reader.UltralightCalls);
                Assert.Equal(nameof(KeyRecordingReader.ReadMifareUltralightPagesAsync), call.Method);
                Assert.Equal(0, call.StartPage);
                Assert.Equal(135, call.PageCount);
                Assert.Equal(UltralightExpectation.Ntag215, call.Access.Expected);
                Assert.Equal(UltralightAuth.None, call.Access.Auth);
                Assert.False(call.Access.AllowProtectedPages);

                // The reader of the test answers with the position of each byte: page 5 holds the bytes 20 to 23.
                var page = node.Children[5];
                Assert.Equal(new byte[] { 20, 21, 22, 23 }, page.PageModel.Data);
                Assert.True(page.IsAuthenticated);
                Assert.Equal("Data: 14151617", Assert.Single(page.Children).GrandChildNodeHeader);
            });
        }

        [Fact]
        public async Task TheQuickCheck_OfAnUltralightC_ReadsOnlyThePagesThatCanBeRead()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var node = NodeOf(CARD_TYPE.MifareUltralightC);
                var reader = new KeyRecordingReader();

                await node.ReadUltralightPagesAsync(reader);

                // Pages 44 to 47 hold the key, which cannot be read: a read that reached them would show page 0 for them.
                Assert.Equal(44, Assert.Single(reader.UltralightCalls).PageCount);
                Assert.True(node.Children[43].IsAuthenticated);

                // No mark: false would paint the key pages red, as a failure, and they were left out on purpose.
                Assert.Null(node.Children[44].IsAuthenticated);
                Assert.Empty(node.Children[44].Children);
                Assert.Equal(new byte[4], node.Children[44].PageModel.Data);
            });
        }

        [Fact]
        public async Task AReadThatStopsOnTheWay_ShowsThePagesBeforeIt_AndMarksTheOthersUnread()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var node = NodeOf(CARD_TYPE.MifareUltralight);
                var reader = new KeyRecordingReader
                {
                    ScriptedUltralightResult = new UltralightResult(ERROR.PermissionDenied, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, UltralightVariant.Plain, false, 2, 2),
                };

                await node.ReadUltralightPagesAsync(reader);

                Assert.Equal(new byte[] { 5, 6, 7, 8 }, node.Children[1].PageModel.Data);
                Assert.True(node.Children[0].IsAuthenticated);
                Assert.True(node.Children[1].IsAuthenticated);
                Assert.Null(node.Children[2].IsAuthenticated);
                Assert.Empty(node.Children[2].Children);
            });
        }

        [Fact]
        public async Task AReadThatFailsAfterASuccessfulOne_DoesNotKeepTheOldBytes()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var node = NodeOf(CARD_TYPE.MifareUltralight);
                var reader = new KeyRecordingReader();

                await node.ReadUltralightPagesAsync(reader);
                Assert.Equal(new byte[] { 20, 21, 22, 23 }, node.Children[5].PageModel.Data);

                // The second check gets two pages and then a refusal.
                reader.ScriptedUltralightResult = new UltralightResult(ERROR.PermissionDenied, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, UltralightVariant.Plain, false, 2, 2);
                await node.ReadUltralightPagesAsync(reader);

                Assert.Equal(new byte[] { 5, 6, 7, 8 }, node.Children[1].PageModel.Data);

                // What the first check showed of the other pages is gone: neither its bytes nor its mark nor its line.
                Assert.Equal(new byte[4], node.Children[5].PageModel.Data);
                Assert.Null(node.Children[5].IsAuthenticated);
                Assert.Empty(node.Children[5].Children);
            });
        }

        [Fact]
        public async Task ANodeWithoutPages_ReadsNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var node = NodeOf(CARD_TYPE.NTAG_212);
                var reader = new KeyRecordingReader();

                await node.ReadUltralightPagesAsync(reader);

                Assert.Empty(reader.UltralightCalls);
            });
        }

        [Fact]
        public async Task ASecondQuickCheck_ReplacesWhatTheFirstShowed()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var node = NodeOf(CARD_TYPE.MifareUltralight);
                var reader = new KeyRecordingReader();

                await node.ReadUltralightPagesAsync(reader);
                await node.ReadUltralightPagesAsync(reader);

                Assert.All(node.Children, child => Assert.Single(child.Children));
            });
        }
    }
}
