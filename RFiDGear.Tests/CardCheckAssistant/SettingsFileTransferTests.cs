using System;
using System.IO;
using System.Linq;

using RFiDGear.Extensions.CardCheckAssistant.Services;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    public sealed class SettingsFileTransferTests : IDisposable
    {
        private readonly string _folder = Directory.CreateTempSubdirectory("CardCheckSettingsFile").FullName;

        public void Dispose() => Directory.Delete(_folder, recursive: true);

        private string File(string name) => Path.Combine(_folder, name);

        private static SettingsData Everything() => new SettingsData
        {
            OutputFolder = "D:\\reports",
            OneFolderPerJob = true,
            RemoveTemporaryReports = true,
            ReportLanguage = ReportLanguageChoice.English,
            DefaultProjectPath = "D:\\p\\check.rfPrj",
            MemoryProjectPath = "D:\\p\\memory.rfPrj",
            Database = new DatabaseData("srv\\inst", "orders", "dbo.T_Orders"),
            UseStubJobs = true,
            TextBlocks = new[] { new TextBlockData("Gru\u00df", "Zeile 1\nZeile 2 \u2013 \"quoted\""), new TextBlockData("Two", string.Empty) },
        };

        [Fact]
        public void WhatIsExported_IsImportedAgain_FieldForField()
        {
            var transfer = new SettingsFileTransfer();
            var data = Everything();

            Assert.Equal(SettingsTransferOutcome.Done, transfer.Export(File("s.json"), data));
            var back = transfer.Import(File("s.json"));

            Assert.Equal(SettingsTransferOutcome.Done, back.Outcome);
            Assert.Equal(data with { TextBlocks = Array.Empty<TextBlockData>() }, back.Data! with { TextBlocks = Array.Empty<TextBlockData>() });
            Assert.Equal(data.TextBlocks, back.Data!.TextBlocks.ToArray());
        }

        [Fact]
        public void AnExportOverAnExistingFile_ReplacesIt_AndLeavesNoTemporaryFile()
        {
            var transfer = new SettingsFileTransfer();
            System.IO.File.WriteAllText(File("s.json"), "old");

            Assert.Equal(SettingsTransferOutcome.Done, transfer.Export(File("s.json"), Everything()));

            Assert.Equal(SettingsTransferOutcome.Done, transfer.Import(File("s.json")).Outcome);
            Assert.Equal(new[] { "s.json" }, Directory.GetFiles(_folder).Select(Path.GetFileName));
        }

        [Fact]
        public void AnExportToAFolderThatIsNotThere_Fails_WithoutThrowing()
        {
            Assert.Equal(SettingsTransferOutcome.Failed, new SettingsFileTransfer().Export(Path.Combine(_folder, "nope", "s.json"), Everything()));
        }

        [Theory]
        [InlineData("not json at all")]
        [InlineData("{}")]
        [InlineData("[1,2,3]")]
        [InlineData("{\"format\":\"Something.Else\",\"version\":1,\"settings\":{}}")]
        [InlineData("{\"format\":\"RFiDGear.CardCheckAssistant.Settings\",\"version\":1}")]
        [InlineData("{\"format\":\"RFiDGear.CardCheckAssistant.Settings\",\"version\":1,\"settings\":{\"textBlocks\":7}}")]
        public void AFileThatIsNoSettingsFile_IsRecognisedAsSuch(string text)
        {
            System.IO.File.WriteAllText(File("x.json"), text);

            var result = new SettingsFileTransfer().Import(File("x.json"));

            Assert.Equal(SettingsTransferOutcome.NotSettings, result.Outcome);
            Assert.Null(result.Data);
        }

        [Fact]
        public void AFileOfANewerVersion_IsNotRead()
        {
            System.IO.File.WriteAllText(File("n.json"), "{\"format\":\"" + SettingsFileTransfer.FormatName + "\",\"version\":" + (SettingsFileTransfer.CurrentVersion + 1) + ",\"settings\":{}}");

            Assert.Equal(SettingsTransferOutcome.NewerVersion, new SettingsFileTransfer().Import(File("n.json")).Outcome);
        }

        [Fact]
        public void AHugeFile_IsNotRead_AndAMissingOneFails()
        {
            using (var stream = System.IO.File.Create(File("big.json")))
            {
                stream.SetLength(SettingsFileTransfer.MaxBytes + 1);
            }

            var transfer = new SettingsFileTransfer();

            Assert.Equal(SettingsTransferOutcome.TooLarge, transfer.Import(File("big.json")).Outcome);
            Assert.Equal(SettingsTransferOutcome.Failed, transfer.Import(File("missing.json")).Outcome);
            Assert.Equal(SettingsTransferOutcome.Failed, transfer.Import(string.Empty).Outcome);
        }
    }
}
