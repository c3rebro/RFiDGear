using System;
using System.IO;

using RFiDGear.HIL.Tests;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The settings file of the hardware tests (<c>hil.settings.json</c>, <c>RFiDGear.HIL.Tests/HilSettings.cs</c>, compiled
    /// into this project too): what it asks for, that a file with a problem asks for nothing and says why, where it is
    /// found, and what the skipped tests tell the person who has to edit it. None of this needs a reader.
    /// </summary>
    public sealed class HilSettingsTests : IDisposable
    {
        private const string Source = @"C:\somewhere\hil.settings.json";

        private readonly string _root = Directory.CreateTempSubdirectory("HilSettings").FullName;

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                // Left to the cleanup of the temp folder.
            }
        }

        [Fact]
        public void BothSettings_AreReadInAnyCase_AndTheFileMayHoldCommentsAndTrailingCommas()
        {
            var settings = HilSettings.Parse("// note\n{\n  \"ReaderScan\": true, // yes\n  \"INTERACTIVE\": true,\n}", Source);

            Assert.Null(settings.Problem);
            Assert.True(settings.ReaderScan);
            Assert.True(settings.Interactive);
            Assert.True(settings.HardwareRequested);
            Assert.True(settings.InteractiveRequested);
        }

        [Theory]
        [InlineData("{}", false, false)]
        [InlineData("{ \"readerScan\": true }", true, false)]
        [InlineData("{ \"readerScan\": false, \"interactive\": true }", false, true)]
        public void ASettingThatIsLeftOutIsOff(string json, bool readerScan, bool interactive)
        {
            var settings = HilSettings.Parse(json, Source);

            Assert.Null(settings.Problem);
            Assert.Equal((readerScan, interactive), (settings.ReaderScan, settings.Interactive));
        }

        [Theory]
        [InlineData("{}", false)]
        [InlineData("{ \"secureKeys\": true }", true)]
        [InlineData("{ \"SECUREKEYS\": true, \"readerScan\": false }", true)]
        [InlineData("{ \"secureKeys\": false, \"readerScan\": true, \"interactive\": true }", false)]
        public void TheSecureKeyCases_AreAskedForBySecureKeysAlone_AndNeedNeitherOfTheOtherSettings(string json, bool expected)
        {
            var settings = HilSettings.Parse(json, Source);

            Assert.Null(settings.Problem);
            Assert.Equal(expected, settings.SecureKeys);
            Assert.Equal(expected, settings.SecureKeysRequested);
        }

        [Fact]
        public void TheSecureKeyCases_AreNotSwitchedOnByTheOtherSettings_OrByTheSwitchOfTheRunner()
        {
            var settings = HilSettings.Parse("{ \"readerScan\": true, \"interactive\": true }", Source);
            Assert.False(settings.SecureKeysRequested);

            File.WriteAllText(Path.Combine(_root, HilSettings.FileName), "{ \"readerScan\": true }");
            Assert.False(HilSettings.Load(_root, "1").SecureKeysRequested);

            // The switch of the runner turns the questions on and leaves what the file says about the keys as it is.
            File.WriteAllText(Path.Combine(_root, HilSettings.FileName), "{ \"readerScan\": true, \"secureKeys\": true }");
            Assert.True(HilSettings.Load(_root, "1").SecureKeysRequested);
            Assert.True(HilSettings.Load(_root).SecureKeysRequested);
        }

        [Fact]
        public void AFileWithAMistake_NeverSwitchesTheSecureKeyCasesOn_AndTheSkippedCaseSaysWhatIsWrong()
        {
            var broken = HilSettings.Parse("{ \"secureKeys\": true, \"secureKey\": true }", Source);

            Assert.NotNull(broken.Problem);
            Assert.False(broken.SecureKeys);
            Assert.False(broken.SecureKeysRequested);
            Assert.Contains("secureKey", broken.SecureKeysSkipReason(), StringComparison.Ordinal);
            Assert.Contains(Source, broken.SecureKeysSkipReason(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheSkippedSecureKeyCases_NameTheFileTheSettingAndWhatTheyChange()
        {
            var off = HilSettings.Parse("{ \"readerScan\": true }", Source);
            var noFile = HilSettings.Load(_root);

            var reason = off.SecureKeysSkipReason();
            Assert.Contains("secureKeys", reason, StringComparison.Ordinal);
            Assert.Contains(Source, reason, StringComparison.Ordinal);
            Assert.Contains("Credential Manager", reason, StringComparison.Ordinal);
            Assert.Contains("keys of the test application", reason, StringComparison.Ordinal);

            Assert.Contains(HilSettings.SampleFileName, noFile.SecureKeysSkipReason(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheCasesThatMoveCards_NeedTheReaderScanTestsToo()
        {
            var settings = HilSettings.Parse("{ \"readerScan\": false, \"interactive\": true }", Source);

            Assert.False(settings.HardwareRequested);
            Assert.False(settings.InteractiveRequested);
        }

        [Theory]
        [InlineData("{ \"readerScan\": true, \"interactive\": true, \"reader_scan\": true }", "reader_scan")]
        [InlineData("{ \"secureKeys\": true, \"oops\": 1 }", "oops")]
        [InlineData("{ \"secureKeys\": \"yes\" }", "must be true or false")]
        [InlineData("{ \"secureKeys\": true, \"SecureKeys\": true }", "set twice")]
        [InlineData("{ \"readerScan\": true, \"interactive\": \"yes\" }", "must be true or false")]
        [InlineData("{ \"readerScan\": \"yes\", \"interactive\": true }", "must be true or false")]
        [InlineData("{ \"readerScan\": 1, \"interactive\": true }", "must be true or false")]
        [InlineData("{ \"readerScan\": true, \"readerScan\": false }", "set twice")]
        [InlineData("{ \"readerScan\": true, \"ReaderScan\": false }", "set twice")]
        [InlineData("{ \"readerScan\": true, \"interactive\": true, \"interactive\": true }", "set twice")]
        [InlineData("[ true ]", "one JSON object")]
        [InlineData("{ nonsense", "not valid JSON")]
        [InlineData("", "not valid JSON")]
        public void AFileWithAProblem_AsksForNothing_AndSaysWhy(string json, string expectedInTheProblem)
        {
            // A mistake must not switch a test on, not even for a setting that is written correctly before it.
            var settings = HilSettings.Parse(json, Source);

            Assert.NotNull(settings.Problem);
            Assert.Contains(expectedInTheProblem, settings.Problem, StringComparison.Ordinal);
            Assert.False(settings.ReaderScan);
            Assert.False(settings.Interactive);
            Assert.False(settings.SecureKeys);
            Assert.False(settings.HardwareRequested);
            Assert.False(settings.InteractiveRequested);
            Assert.False(settings.SecureKeysRequested);
        }

        [Theory]
        [InlineData("{ \"readerScan\": true }", "1", true)]
        [InlineData("{ \"readerScan\": true, \"interactive\": true }", null, true)]
        [InlineData("{ \"readerScan\": true }", null, false)]
        [InlineData("{ \"readerScan\": true }", "0", false)]
        [InlineData("{ \"readerScan\": false }", "1", false)]
        [InlineData("{ \"readerScan\": true, \"oops\": 1 }", "1", false)]
        public void TheRunnerCanTurnTheQuestionsOnForOneRun_ButNothingElse(string json, string? fromRunner, bool expected)
        {
            File.WriteAllText(Path.Combine(_root, HilSettings.FileName), json);

            Assert.Equal(expected, HilSettings.Load(_root, fromRunner).InteractiveRequested);
        }

        [Fact]
        public void TheVariableOfTheRunner_IsWhatTheTestsOfThisRunRead()
        {
            File.WriteAllText(Path.Combine(_root, HilSettings.FileName), "{ \"readerScan\": true }");
            var before = Environment.GetEnvironmentVariable(HilSettings.RunnerInteractiveVariable);
            try
            {
                Environment.SetEnvironmentVariable(HilSettings.RunnerInteractiveVariable, null);
                Assert.False(HilSettings.LoadForThisRun(_root).InteractiveRequested);

                Environment.SetEnvironmentVariable(HilSettings.RunnerInteractiveVariable, "1");
                Assert.True(HilSettings.LoadForThisRun(_root).InteractiveRequested);
            }
            finally
            {
                Environment.SetEnvironmentVariable(HilSettings.RunnerInteractiveVariable, before);
            }
        }

        [Fact]
        public void TheSwitchOfTheRunner_DoesNotTakeTheProblemOfAFileAway()
        {
            File.WriteAllText(Path.Combine(_root, HilSettings.FileName), "{ \"readerScan\": true, \"oops\": 1 }");

            var settings = HilSettings.Load(_root, "1");

            Assert.NotNull(settings.Problem);
            Assert.Contains("oops", settings.SkipReason(movesCards: true), StringComparison.Ordinal);
        }

        [Fact]
        public void TheRunnerAndTheTests_UseTheSameVariable()
        {
            // The name is written twice, in the script and in the tests; this is what keeps the two together.
            var script = FindFile("Run-HilTests.ps1");
            Assert.NotNull(script);

            Assert.Equal("RFIDGEAR_HIL_INTERACTIVE", HilSettings.RunnerInteractiveVariable);
            Assert.Contains("$env:" + HilSettings.RunnerInteractiveVariable, File.ReadAllText(script!), StringComparison.Ordinal);
        }

        [Fact]
        public void TheRunnerKnowsEverySettingOfTheTests()
        {
            // The runner reads the file with its own parser and rejects a name it does not know: a setting that only the
            // tests knew would make the runner refuse a file that the tests accept.
            var script = FindFile("Run-HilTests.ps1");
            Assert.NotNull(script);
            var text = File.ReadAllText(script!);

            Assert.Contains("foreach ($key in 'readerScan', 'interactive', 'secureKeys')", text, StringComparison.Ordinal);
            Assert.Contains("$name -ne 'securekeys'", text, StringComparison.Ordinal);
        }

        [Fact]
        public void WithoutAFile_TheRunnerCannotTurnAnythingOn()
        {
            var settings = HilSettings.Load(_root, "1");

            Assert.False(settings.HardwareRequested);
            Assert.False(settings.InteractiveRequested);
        }

        [Fact]
        public void TheNearestFileAbove_IsTheOneThatIsUsed()
        {
            var nested = Directory.CreateDirectory(Path.Combine(_root, "bin", "Debug", "net10.0-windows")).FullName;
            File.WriteAllText(Path.Combine(_root, HilSettings.FileName), "{ \"readerScan\": true }");

            Assert.Equal(Path.Combine(_root, HilSettings.FileName), HilSettings.Find(nested));
            Assert.True(HilSettings.Load(nested).HardwareRequested);

            // A file in a folder closer to the tests wins over the one above it.
            File.WriteAllText(Path.Combine(nested, HilSettings.FileName), "{ \"readerScan\": false }");

            Assert.Equal(Path.Combine(nested, HilSettings.FileName), HilSettings.Find(nested));
            Assert.False(HilSettings.Load(nested).HardwareRequested);
        }

        [Fact]
        public void WithoutAFile_NothingIsAskedFor_AndTheSkippedTestsNameTheSample()
        {
            var settings = HilSettings.Load(_root);

            Assert.Null(settings.Source);
            Assert.False(settings.HardwareRequested);
            Assert.Contains(HilSettings.SampleFileName, settings.SkipReason(movesCards: false), StringComparison.Ordinal);
        }

        [Fact]
        public void TheSkippedTests_NameTheFileAndTheSettingToChange()
        {
            var off = HilSettings.Parse("{ \"readerScan\": false }", Source);
            var noQuestions = HilSettings.Parse("{ \"readerScan\": true }", Source);
            var broken = HilSettings.Parse("{ \"readerscan\": 1 }", Source);

            Assert.Contains("readerScan", off.SkipReason(movesCards: false), StringComparison.Ordinal);
            Assert.Contains(Source, off.SkipReason(movesCards: false), StringComparison.Ordinal);

            // The cases that move cards tell the other setting only once the reader-scan tests are on.
            Assert.Contains("readerScan", off.SkipReason(movesCards: true), StringComparison.Ordinal);
            Assert.Contains("interactive", noQuestions.SkipReason(movesCards: true), StringComparison.Ordinal);
            Assert.Contains("-Interactive", noQuestions.SkipReason(movesCards: true), StringComparison.Ordinal);
            Assert.Contains(Source, noQuestions.SkipReason(movesCards: true), StringComparison.Ordinal);

            Assert.Contains("must be true or false", broken.SkipReason(movesCards: false), StringComparison.Ordinal);
        }

        [Fact]
        public void ASampleFileOfTheRepository_IsValid_AndAsksForTheReaderScanTestsOnly()
        {
            var sample = FindFile(HilSettings.SampleFileName);
            Assert.NotNull(sample);

            var settings = HilSettings.Parse(File.ReadAllText(sample!), sample!);

            Assert.Null(settings.Problem);
            Assert.True(settings.ReaderScan);
            Assert.False(settings.Interactive);

            // The cases that change keys are never on in the sample that a new checkout starts from.
            Assert.False(settings.SecureKeys);
            Assert.Contains("\"secureKeys\"", File.ReadAllText(sample!), StringComparison.Ordinal);
        }

        // The sample and the runner are in the folder of the solution, above the build folder of the tests.
        private static string? FindFile(string name)
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
