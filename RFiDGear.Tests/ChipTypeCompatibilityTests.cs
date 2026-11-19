using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The chip type of the Ultralight EV1 that the application called <c>MifareUltralightC_EV1</c> is called
    /// <c>MifareUltralight_EV1</c> since the library renamed it. A project stores a chip type by name (the chip type a Generic
    /// chip task is set to, and the type of a chip), and the serializer rejects a name that the enum no longer has, so the old
    /// name is renamed in the project text before it is read, where a chip type is stored and nowhere else.
    /// </summary>
    public class ChipTypeCompatibilityTests
    {
        private static ProjectManager NewManager() => new ProjectManager(Path.Combine(Path.GetTempPath(), "rfidgear-chiptype-" + Guid.NewGuid().ToString("N")));

        [Fact]
        public void TheOldNameOfTheEv1ChipType_IsRenamed_WhereAChipTypeIsStored()
        {
            const string Legacy =
                "<SelectedChipType>MifareUltralightC_EV1</SelectedChipType>" +
                "<Chip><CardType>MifareUltralightC_EV1</CardType></Chip>";

            var normalized = NewManager().NormalizeLegacyProjectXml(Legacy);

            Assert.Equal(
                "<SelectedChipType>MifareUltralight_EV1</SelectedChipType>" +
                "<Chip><CardType>MifareUltralight_EV1</CardType></Chip>",
                normalized);
        }

        [Fact]
        public void TheOldNameOfTheEv1ChipType_IsLeftAloneAnywhereElse()
        {
            // A description, a reference or a provider id may contain the name, and a chip type that is not the old one stays.
            const string Xml =
                "<Reference>MifareUltralightC_EV1</Reference><ProviderId>x.MifareUltralightC_EV1</ProviderId>" +
                "<SelectedTaskDescription>MifareUltralightC_EV1 test</SelectedTaskDescription>" +
                "<SelectedChipType>MifareUltralightC</SelectedChipType><CardType>MifareUltralight_EV1</CardType>" +
                "<SelectedChipType><![CDATA[<SelectedChipType>MifareUltralightC_EV1</SelectedChipType>]]></SelectedChipType>";

            Assert.Equal(Xml, NewManager().NormalizeLegacyProjectXml(Xml));
        }

        [Fact]
        public void TheErrorCodeOfAnOldProject_IsStillNormalized_BesideTheChipType()
        {
            const string Legacy =
                "<SelectedExecuteConditionErrorLevel>AuthenticationError</SelectedExecuteConditionErrorLevel>" +
                "<SelectedChipType>MifareUltralightC_EV1</SelectedChipType>";

            Assert.Equal(
                "<SelectedExecuteConditionErrorLevel>AuthFailure</SelectedExecuteConditionErrorLevel>" +
                "<SelectedChipType>MifareUltralight_EV1</SelectedChipType>",
                NewManager().NormalizeLegacyProjectXml(Legacy));
        }

        [Fact]
        public async Task AProjectWithAGenericChipTaskOfTheOldChipType_LoadsOnlyThroughTheNormalization()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var project = new ChipTaskHandlerModel();
                project.TaskCollection.Add(new GenericChipTaskViewModel { SelectedChipType = CARD_TYPE.MifareUltralight_EV1, CurrentTaskIndex = "0" });
                string xml;

                using (var writer = new StringWriter())
                {
                    new XmlSerializer(typeof(ChipTaskHandlerModel)).Serialize(writer, project);
                    xml = writer.ToString();
                }

                var legacy = xml.Replace("<SelectedChipType>MifareUltralight_EV1</SelectedChipType>", "<SelectedChipType>MifareUltralightC_EV1</SelectedChipType>");
                Assert.NotEqual(xml, legacy);

                // What the loader would have done before: the serializer does not know the old name.
                Assert.Throws<InvalidOperationException>(() => Load(legacy));

                var loaded = Load(NewManager().NormalizeLegacyProjectXml(legacy));
                var task = Assert.IsType<GenericChipTaskViewModel>(Assert.Single(loaded.TaskCollection));
                Assert.Equal(CARD_TYPE.MifareUltralight_EV1, task.SelectedChipType);
            });
        }

        // ── What a Generic chip task that checks for "Mifare Ultralight" says to an NTAG ──

        [Theory]
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.MifareUltralight, true)]
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.MifareUltralightC, true)]
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.MifareUltralight_EV1, true)]
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.MifareUltralight_EV1_MF0UL11, true)]
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.MifareUltralight_EV1_MF0UL21, true)]
        // Before Elatec.NET 0.7.0-beta.2 an NTAG was reported as MifareUltralight, so a project that checks for "Mifare Ultralight"
        // has been passing for the NTAGs that it works with, and its branches depend on it. It still passes.
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.NTAG_213, true)]
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.NTAG_215, true)]
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.NTAG_216, true)]
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.Mifare1K, false)]
        [InlineData(CARD_TYPE.MifareUltralight, CARD_TYPE.DESFireEV1, false)]
        // The other checks are what they were: a family is the family, and any other type is that exact type.
        [InlineData(CARD_TYPE.NTAG_210, CARD_TYPE.NTAG_215, true)]
        [InlineData(CARD_TYPE.NTAG_210, CARD_TYPE.MifareUltralight, false)]
        [InlineData(CARD_TYPE.NTAG_215, CARD_TYPE.NTAG_215, true)]
        [InlineData(CARD_TYPE.NTAG_215, CARD_TYPE.NTAG_213, false)]
        [InlineData(CARD_TYPE.NTAG_215, CARD_TYPE.MifareUltralight, false)]
        [InlineData(CARD_TYPE.MifareUltralightC, CARD_TYPE.MifareUltralightC, true)]
        [InlineData(CARD_TYPE.MifareUltralightC, CARD_TYPE.MifareUltralight, false)]
        [InlineData(CARD_TYPE.MifareClassic, CARD_TYPE.Mifare1K, true)]
        [InlineData(CARD_TYPE.Mifare1K, CARD_TYPE.Mifare1K, true)]
        [InlineData(CARD_TYPE.Mifare1K, CARD_TYPE.Mifare4K, false)]
        public void AChipTypeCheck_SaysWhatItAlwaysSaid_AndStillPassesAnNtagForMifareUltralight(CARD_TYPE selected, CARD_TYPE found, bool expected)
        {
            Assert.Equal(expected, GenericChipTaskViewModel.ChipTypeMatches(selected, found));
        }

        // ── The same for a MIFARE Plus in security level 1, which a TWN4 reported as a Classic chip until Elatec.NET 0.7.0 ──

        [Theory]
        // A project that checks for "Mifare Classic", or for the Classic type of the size, has been passing for it, and still does.
        [InlineData(CARD_TYPE.MifareClassic, CARD_TYPE.MifarePlus_SL1_1K, true)]
        [InlineData(CARD_TYPE.MifareClassic, CARD_TYPE.MifarePlus_SL1_2K, true)]
        [InlineData(CARD_TYPE.MifareClassic, CARD_TYPE.MifarePlus_SL1_4K, true)]
        [InlineData(CARD_TYPE.Mifare1K, CARD_TYPE.MifarePlus_SL1_1K, true)]
        [InlineData(CARD_TYPE.Mifare2K, CARD_TYPE.MifarePlus_SL1_2K, true)]
        [InlineData(CARD_TYPE.Mifare4K, CARD_TYPE.MifarePlus_SL1_4K, true)]
        // The size still counts, and a Plus in another level has no Classic commands.
        [InlineData(CARD_TYPE.Mifare1K, CARD_TYPE.MifarePlus_SL1_4K, false)]
        [InlineData(CARD_TYPE.Mifare4K, CARD_TYPE.MifarePlus_SL1_2K, false)]
        [InlineData(CARD_TYPE.MifareClassic, CARD_TYPE.MifarePlus_SL3_4K, false)]
        [InlineData(CARD_TYPE.Mifare4K, CARD_TYPE.MifarePlus_SL3_4K, false)]
        [InlineData(CARD_TYPE.MifareClassic, CARD_TYPE.MifarePlus_SL2_4K, false)]
        [InlineData(CARD_TYPE.MifareClassic, CARD_TYPE.MifarePlus_SL0_1K, false)]
        // A check for a Plus type is exact (the family is the family), and a Classic chip is not a Plus.
        [InlineData(CARD_TYPE.MifarePlus_SL1_4K, CARD_TYPE.MifarePlus_SL1_4K, true)]
        [InlineData(CARD_TYPE.MifarePlus_SL1_4K, CARD_TYPE.MifarePlus_SL1_2K, false)]
        [InlineData(CARD_TYPE.MifarePlus_SL1_4K, CARD_TYPE.Mifare4K, false)]
        [InlineData(CARD_TYPE.MifarePlus, CARD_TYPE.MifarePlus_SL1_4K, true)]
        [InlineData(CARD_TYPE.MifarePlus, CARD_TYPE.MifarePlus_SL3_2K, true)]
        [InlineData(CARD_TYPE.MifarePlus, CARD_TYPE.Mifare4K, false)]
        // Nothing else passes for a Classic chip.
        [InlineData(CARD_TYPE.MifareClassic, CARD_TYPE.DESFireEV1, false)]
        [InlineData(CARD_TYPE.Mifare4K, CARD_TYPE.NOTAG, false)]
        public void AChipTypeCheck_StillPassesAPlusInSecurityLevel1ForTheClassicChipOfItsSize(CARD_TYPE selected, CARD_TYPE found, bool expected)
        {
            Assert.Equal(expected, GenericChipTaskViewModel.ChipTypeMatches(selected, found));
        }

        // ── A SmartMX that presents itself as DESFire: the reader names it by its GetVersion answer, a check names the kind ──

        [Theory]
        // Every SmartMX DESFire chip, whatever its generation and size, is a "SmartMX DESFire" (the generic type is its own family).
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFire, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFireEV0_256, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFireEV0_4K, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFireEV1_256, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFireEV1_4K, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFireEV1_8K, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFireEV2_2K, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFireEV2_32K, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFireEV3_2K, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_DESFireEV3_32K, true)]
        // Nothing else is one: not a DESFire chip itself, not a SmartMX that is a Classic chip, not another family.
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.DESFireEV0, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.DESFireEV0_4K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.DESFireEV1, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.DESFireEV1_2K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.DESFireEV1_8K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.DESFireEV2_32K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.DESFireEV3_32K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.DESFireLight, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_Mifare_1K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.SmartMX_Mifare_4K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.MifarePlus_SL3_4K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.Mifare4K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.NTAG_215, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFire, CARD_TYPE.NOTAG, false)]
        // The other checks are what they were: a family is the family, and any other type is that exact type.
        [InlineData(CARD_TYPE.DESFireEV1, CARD_TYPE.SmartMX_DESFireEV1_4K, true)]
        [InlineData(CARD_TYPE.DESFireEV1, CARD_TYPE.DESFireEV1_2K, true)]
        [InlineData(CARD_TYPE.DESFireEV0, CARD_TYPE.SmartMX_DESFireEV1_4K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFireEV1_4K, CARD_TYPE.SmartMX_DESFireEV1_4K, true)]
        [InlineData(CARD_TYPE.SmartMX_DESFireEV1_4K, CARD_TYPE.SmartMX_DESFireEV1_8K, false)]
        [InlineData(CARD_TYPE.SmartMX_DESFireEV1_4K, CARD_TYPE.SmartMX_DESFire, false)]
        [InlineData(CARD_TYPE.DESFireEV1_4K, CARD_TYPE.SmartMX_DESFireEV1_4K, false)]
        public void AChipTypeCheckForASmartMxDesfire_PassesForEverySmartMxThatPresentsItselfAsDesfire(CARD_TYPE selected, CARD_TYPE found, bool expected)
        {
            Assert.Equal(expected, GenericChipTaskViewModel.ChipTypeMatches(selected, found));
        }

        [Theory]
        [InlineData(CARD_TYPE.NTAG_215, ERROR.NoError)]
        [InlineData(CARD_TYPE.MifareUltralight_EV1_MF0UL21, ERROR.NoError)]
        [InlineData(CARD_TYPE.Mifare1K, ERROR.IsNotTrue)]
        public async Task TheTaskThatChecksForMifareUltralight_RunsTheSameCheck_ForTheChipThatIsOnTheReader(CARD_TYPE onTheReader, ERROR expected)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = new GenericChipTaskViewModel { SelectedChipType = CARD_TYPE.MifareUltralight, CurrentTaskIndex = "0" };

                using (ReaderDeviceScope.Use(new KeySlots.KeyRecordingReader()))
                {
                    await task.CheckChipType.ExecuteAsync(new GenericChipModel("04AABBCCDDEEFF", onTheReader));
                }

                Assert.Equal(expected, task.CurrentTaskErrorLevel);
                Assert.Equal(expected == ERROR.NoError, task.IsTaskCompletedSuccessfully);
            });
        }

        private static ChipTaskHandlerModel Load(string xml)
        {
            using var reader = new StringReader(xml);

            return (ChipTaskHandlerModel)new XmlSerializer(typeof(ChipTaskHandlerModel)).Deserialize(reader)!;
        }
    }
}
