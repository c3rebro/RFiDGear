#nullable enable
using System;
using System.IO;
using System.Linq;

using RFiDGear.Infrastructure;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Which help file belongs to which language, how it is started, and which window is the help. F1 depends on all three, and none of
    /// them can be seen from outside: a help that is started without the shell does not start at all, and the program then says nothing.
    /// </summary>
    public sealed class HelpFileTests
    {
        [Theory]
        [InlineData("german", "RFiDGear_de.chm")]
        [InlineData("German", "RFiDGear_de.chm")]
        [InlineData("GERMAN", "RFiDGear_de.chm")]
        [InlineData("english", "RFiDGear_en.chm")]
        [InlineData("English", "RFiDGear_en.chm")]
        [InlineData("", "RFiDGear_en.chm")]
        [InlineData("deutsch", "RFiDGear_en.chm")]
        [InlineData("klingon", "RFiDGear_en.chm")]
        public void TheHelpFile_FollowsTheLanguageOfTheSettings_AndIsEnglishForAnythingElse(string language, string expected)
        {
            Assert.Equal(expected, HelpFile.FileNameFor(language));
        }

        [Fact]
        public void TheHelpFile_ForNoLanguageAtAll_IsEnglish()
        {
            Assert.Equal(HelpFile.EnglishFileName, HelpFile.FileNameFor(null));
        }

        [Fact]
        public void ThePathOfTheHelpFile_IsInTheFolderOfTheProgram()
        {
            var folder = Path.Combine(Path.GetTempPath(), "RFiDGear program folder");

            Assert.Equal(Path.Combine(folder, "RFiDGear_de.chm"), HelpFile.PathFor("german", folder));
            Assert.Equal(Path.Combine(folder, "RFiDGear_en.chm"), HelpFile.PathFor("english", folder));
        }

        [Fact]
        public void TheHelpIsStartedByTheShell_BecauseAProcessThatIsNoShellCannotStartADocument()
        {
            var startInfo = HelpFile.StartInfoFor(@"C:\Program Files\RFiDGear\RFiDGear_de.chm");

            Assert.True(startInfo.UseShellExecute);
            Assert.Equal(@"C:\Program Files\RFiDGear\RFiDGear_de.chm", startInfo.FileName);
        }

        [Theory]
        [InlineData("RFiDGear Hilfe", true)]
        [InlineData("RFiDGear Hilfe - Willkommen", true)]
        [InlineData("RFiDGear Help", true)]
        [InlineData("RFiDGear Help - Welcome", true)]
        [InlineData("Help - Google Chrome", false)]
        [InlineData("Hilfe", false)]
        [InlineData("RFiDGear v2.1.1", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void OnlyTheWindowOfTheHelpOfRFiDGear_IsTheHelpWindow(string? title, bool expected)
        {
            // A browser tab that says "Help" must not keep the help from opening.
            Assert.Equal(expected, HelpFile.IsHelpWindowTitle(title));
        }

        [Fact]
        public void AHelpFileThatDoesNotExist_IsNotStarted()
        {
            var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".chm");

            Assert.False(HelpFile.TryOpen(missing));
        }

        [Fact]
        public void TheMessageThatTellsTheUserAboutAMissingHelp_ExistsInBothLanguages_AndNamesTheFile()
        {
            foreach (var language in new[] { "de", "en" })
            {
                var resx = System.Xml.Linq.XDocument.Load(Path.Combine(DotNetTool.RepositoryRoot(), "RFiDGear", "Resources", "Manifest." + language + ".resx"));
                string ValueOf(string name) => resx.Root!.Elements("data").Single(data => (string?)data.Attribute("name") == name).Element("value")!.Value;

                Assert.False(string.IsNullOrWhiteSpace(ValueOf("messageBoxHelpNotFoundCaption")));
                Assert.Contains("{0}", ValueOf("messageBoxHelpNotFoundMessage"), StringComparison.Ordinal);
            }
        }
    }
}
