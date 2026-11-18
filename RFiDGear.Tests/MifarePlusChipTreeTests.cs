using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The chip tree of the main window for a MIFARE Plus. A Plus in security level 1 answers like a Classic chip and has the sectors of
    /// its size (the quick check reads them like those of a Classic chip); a Plus in security level 3 has no Classic commands and gets
    /// the one node that the PC/SC reader has always shown. The Elatec provider reported a Plus as a plain <see cref="GenericChipModel"/>,
    /// which the node of the family took for a <see cref="MifareClassicChipModel"/> with an <c>as</c> cast: it was <c>null</c>, and the
    /// copy threw.
    /// </summary>
    public class MifarePlusChipTreeTests
    {
        private const string Uid = "04AABBCCDDEEFF";

        [Theory]
        [InlineData(CARD_TYPE.MifarePlus_SL1_1K, 16)]
        [InlineData(CARD_TYPE.MifarePlus_SL1_2K, 32)]
        [InlineData(CARD_TYPE.MifarePlus_SL1_4K, 40)]
        [InlineData(CARD_TYPE.MifarePlus_SL3_1K, 1)]
        [InlineData(CARD_TYPE.MifarePlus_SL3_2K, 1)]
        [InlineData(CARD_TYPE.MifarePlus_SL3_4K, 1)]
        public async Task TheNodeOfAPlusChip_IsMadeFromAGenericModelToo(CARD_TYPE type, int children)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var node = MainWindowViewModel.CreateParentNode(new GenericChipModel(Uid, type), new ObservableCollection<IDialogViewModel>());

                Assert.NotNull(node);
                Assert.Equal(type, node.CardType);
                Assert.Equal(Uid, node.UID);
                Assert.Equal(children, node.Children.Count);
            });
        }

        [Theory]
        [InlineData(CARD_TYPE.MifarePlus_SL1_1K, 16)]
        [InlineData(CARD_TYPE.MifarePlus_SL1_4K, 40)]
        [InlineData(CARD_TYPE.Mifare4K, 40)]
        [InlineData(CARD_TYPE.MifarePlus_SL3_2K, 1)]
        public async Task TheSectorsOfAPlusChip_AreNumberedLikeThoseOfAClassicChip(CARD_TYPE type, int sectors)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var node = MainWindowViewModel.CreateParentNode(new MifareClassicChipModel(Uid, type), new ObservableCollection<IDialogViewModel>());

                // Blank rows (the header of a Plus was left out) cannot be told apart.
                Assert.Equal(Enumerable.Range(0, sectors).Select(i => $"Sector: [{i}]"), node.Children.Select(child => child.ChildNodeHeader));
            });
        }

        [Theory]
        [InlineData(CARD_TYPE.Mifare1K, true)]
        [InlineData(CARD_TYPE.Mifare4K, true)]
        [InlineData(CARD_TYPE.SmartMX_Mifare_4K, true)] // the family of the Classic chips, as before
        [InlineData(CARD_TYPE.MifarePlus_SL1_1K, true)] // a TWN4 reported these as Mifare1K, 2K and 4K, and the quick check was for them
        [InlineData(CARD_TYPE.MifarePlus_SL1_2K, true)]
        [InlineData(CARD_TYPE.MifarePlus_SL1_4K, true)]
        [InlineData(CARD_TYPE.MifarePlus_SL3_4K, false)] // no Classic commands
        [InlineData(CARD_TYPE.MifarePlus_SL2_4K, false)]
        [InlineData(CARD_TYPE.MifarePlus_SL0_1K, false)]
        [InlineData(CARD_TYPE.DESFireEV1_4K, false)]
        [InlineData(CARD_TYPE.MifareUltralight, false)]
        [InlineData(CARD_TYPE.NTAG_215, false)]
        [InlineData(CARD_TYPE.NOTAG, false)]
        public void TheQuickCheckOfTheMainWindow_IsTheClassicOneForClassicAndPlusInSecurityLevel1(CARD_TYPE type, bool expected)
        {
            Assert.Equal(expected, MainWindowViewModel.IsClassicQuickCheckable(type));
        }

        [Theory]
        [InlineData(CARD_TYPE.MifarePlus_SL1_4K, 40)]
        [InlineData(CARD_TYPE.Mifare4K, 40)]
        [InlineData(CARD_TYPE.Mifare1K, 16)]
        public async Task TheNodeOfAClassicModel_IsTheSameAsBefore(CARD_TYPE type, int children)
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var node = MainWindowViewModel.CreateParentNode(new MifareClassicChipModel(Uid, type), new ObservableCollection<IDialogViewModel>());

                Assert.NotNull(node);
                Assert.Equal(type, node.CardType);
                Assert.Equal(children, node.Children.Count);
            });
        }

        [Theory]
        // The Classic chips and the SmartMX Classic chips, as before.
        [InlineData(MifareChipSubType.MifareClassic, true)]
        [InlineData(MifareChipSubType.Mifare1K, true)]
        [InlineData(MifareChipSubType.Mifare4K, true)]
        [InlineData(MifareChipSubType.SmartMX_Mifare_4K, true)]
        // The Plus in every level: the PC/SC provider makes a Classic model of all of them.
        [InlineData(MifareChipSubType.MifarePlus_SL0_1K, true)]
        [InlineData(MifareChipSubType.MifarePlus_SL1_2K, true)]
        [InlineData(MifareChipSubType.MifarePlus_SL1_4K, true)]
        [InlineData(MifareChipSubType.MifarePlus_SL2_4K, true)]
        [InlineData(MifareChipSubType.MifarePlus_SL3_4K, true)]
        // Everything else keeps its own model.
        [InlineData(MifareChipSubType.DESFireEV1_4K, false)]
        [InlineData(MifareChipSubType.SmartMX_DESFireEV1_8K, false)]
        [InlineData(MifareChipSubType.DESFireLight, false)]
        [InlineData(MifareChipSubType.MifareUltralight, false)]
        [InlineData(MifareChipSubType.NTAG_215, false)]
        [InlineData(MifareChipSubType.MifareMini, false)]
        [InlineData(MifareChipSubType.Unspecified, false)]
        public void IsClassicFamily_IsTrueForClassicAndPlusOnly(MifareChipSubType subType, bool expected)
        {
            Assert.Equal(expected, ElatecNetProvider.IsClassicFamily(subType));
        }

        [Fact]
        public void IsClassicFamily_TakesEverySubTypeThatItIsGiven()
        {
            // No sub type may throw (the application is built with overflow checks): the members of the enum, DESFireLight (0x8000) included.
            var answers = System.Enum.GetValues<MifareChipSubType>().Select(ElatecNetProvider.IsClassicFamily).ToList();

            Assert.Contains(true, answers);
            Assert.Contains(false, answers);
        }
    }
}
