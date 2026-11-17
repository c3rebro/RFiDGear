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
