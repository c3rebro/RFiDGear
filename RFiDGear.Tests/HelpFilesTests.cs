#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using RFiDGear.Infrastructure;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The help of RFiDGear as it lies in the repository: the sources in <c>docs/chm_de</c> and <c>docs/chm_en</c>, the compiled files
    /// next to them, and the copy into the output folder. These tests read the real files, so a page that the project does not list, a
    /// link that leads nowhere, a German page without an English one, a window title that <see cref="HelpFile"/> would not recognise, or
    /// a help that the build does not copy cannot get into a release.
    /// </summary>
    public sealed class HelpFilesTests
    {
        private static string Docs => Path.Combine(DotNetTool.RepositoryRoot(), "docs");

        private static string FolderOf(string language) => Path.Combine(Docs, "chm_" + language);

        private static string ProjectOf(string language) => Path.Combine(FolderOf(language), "rfidgear_" + language + ".hhp");

        private static string ChmOf(string language) => Path.Combine(FolderOf(language), language == "de" ? HelpFile.GermanFileName : HelpFile.EnglishFileName);

        private static IEnumerable<string> ContentFiles(string language) =>
            Directory.GetFiles(Path.Combine(FolderOf(language), "Content")).Select(Path.GetFileName).OfType<string>().OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        private static IReadOnlyList<string> SectionOfProject(string language, string section)
        {
            var lines = File.ReadAllLines(ProjectOf(language));
            var start = Array.FindIndex(lines, line => line.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
            Assert.True(start >= 0, "the project of '" + language + "' has no [" + section + "] section");

            return lines.Skip(start + 1).TakeWhile(line => !line.TrimStart().StartsWith('[')).Where(line => line.Trim().Length > 0).ToList();
        }

        private static string OptionOfProject(string language, string name)
        {
            var prefix = name + "=";
            var line = SectionOfProject(language, "OPTIONS").SingleOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            Assert.True(line != null, "the project of '" + language + "' sets no " + name);

            return line!.Substring(prefix.Length).Trim();
        }

        private static IReadOnlyList<string> ListedFiles(string language) =>
            SectionOfProject(language, "FILES").Select(line => line.Trim().Replace('\\', '/')).ToList();

        private static IReadOnlyList<string> SitemapTargets(string language, string fileName)
        {
            var text = File.ReadAllText(Path.Combine(FolderOf(language), fileName));

            return Regex.Matches(text, "<param name=\"Local\" value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Select(match => match.Groups[1].Value.Replace('\\', '/')).ToList();
        }

        [Theory]
        [InlineData("de")]
        [InlineData("en")]
        public void EveryFileThatTheProjectListsExists(string language)
        {
            foreach (var entry in ListedFiles(language))
            {
                Assert.True(File.Exists(Path.Combine(FolderOf(language), entry.Replace('/', Path.DirectorySeparatorChar))), "'" + entry + "' is listed in the project of '" + language + "' and does not exist");
            }
        }

        [Theory]
        [InlineData("de")]
        [InlineData("en")]
        public void EveryFileOfTheContentFolder_IsListedByTheProject(string language)
        {
            var listed = ListedFiles(language).Select(entry => entry.Replace("Content/", string.Empty, StringComparison.Ordinal)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var name in ContentFiles(language))
            {
                Assert.True(listed.Contains(name), "'" + name + "' lies in docs/chm_" + language + "/Content and the project does not list it, so the help would not hold it");
            }
        }

        [Fact]
        public void TheGermanAndTheEnglishHelp_HaveTheSamePages()
        {
            Assert.Equal(ContentFiles("de"), ContentFiles("en"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(SitemapTargets("de", "Table of Contents.hhc"), SitemapTargets("en", "Table of Contents.hhc"), StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("de", "Table of Contents.hhc")]
        [InlineData("en", "Table of Contents.hhc")]
        [InlineData("de", "rfidgear_de.hhk")]
        [InlineData("en", "rfidgear_en.hhk")]
        public void TheContentsAndTheIndex_PointOnlyToPagesThatExist(string language, string sitemap)
        {
            var targets = SitemapTargets(language, sitemap);

            Assert.NotEmpty(targets);

            foreach (var target in targets)
            {
                Assert.True(File.Exists(Path.Combine(FolderOf(language), target.Replace('/', Path.DirectorySeparatorChar))), "'" + target + "' is in " + sitemap + " and does not exist");
            }
        }

        [Theory]
        [InlineData("de")]
        [InlineData("en")]
        public void EveryPage_IsInTheTableOfContents(string language)
        {
            var inContents = SitemapTargets(language, "Table of Contents.hhc").ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var page in ContentFiles(language).Where(name => name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.True(inContents.Contains("Content/" + page), "'" + page + "' is a page of the '" + language + "' help and the table of contents does not lead to it");
            }
        }

        [Theory]
        [InlineData("de")]
        [InlineData("en")]
        public void EveryLinkAndEveryImageOfAPage_LeadsToAFile(string language)
        {
            var content = Path.Combine(FolderOf(language), "Content");

            foreach (var page in Directory.GetFiles(content, "*.html"))
            {
                var text = File.ReadAllText(page);

                foreach (Match match in Regex.Matches(text, "(?:href|src)=\"([^\"]*)\"", RegexOptions.IgnoreCase))
                {
                    var target = match.Groups[1].Value;

                    if (target.Length == 0 || target.StartsWith('#') || Regex.IsMatch(target, "^[a-zA-Z][a-zA-Z0-9+.-]*:"))
                    {
                        continue;   // a reference inside the page, or to the outside (http, mailto, ...): not a file of the help
                    }

                    var file = target.Split('#')[0];
                    Assert.True(File.Exists(Path.Combine(content, file.Replace('/', Path.DirectorySeparatorChar))), "'" + Path.GetFileName(page) + "' links to '" + target + "', which does not exist");
                }
            }
        }

        [Theory]
        [InlineData("de")]
        [InlineData("en")]
        public void TheSources_AreAscii_SoThatTheCodePageOfTheProjectCannotSpoilThem(string language)
        {
            // Umlauts are written as entities (&auml;). The contents and the index are read in the code page of the project language.
            foreach (var file in Directory.GetFiles(FolderOf(language), "*", SearchOption.AllDirectories).Where(path => new[] { ".hhp", ".hhc", ".hhk", ".html", ".css" }.Contains(Path.GetExtension(path).ToLowerInvariant())))
            {
                var bytes = File.ReadAllBytes(file);
                var at = Array.FindIndex(bytes, value => value > 0x7F);

                Assert.True(at < 0, Path.GetFileName(file) + " has a character outside of ASCII at byte " + at + ": write it as an entity");
            }
        }

        [Theory]
        [InlineData("de")]
        [InlineData("en")]
        public void NothingIsLeftOfTheHelpOfTheOtherProduct(string language)
        {
            foreach (var file in Directory.GetFiles(FolderOf(language), "*.*", SearchOption.AllDirectories).Where(path => !path.EndsWith(".chm", StringComparison.OrdinalIgnoreCase)))
            {
                var text = File.ReadAllText(file);

                foreach (var forbidden in new[] { "Event Messenger", "EventMessenger", "KMTronic", "Telegram" })
                {
                    Assert.False(text.Contains(forbidden, StringComparison.OrdinalIgnoreCase), Path.GetFileName(file) + " still mentions '" + forbidden + "' (the help of another product)");
                }
            }
        }

        [Theory]
        [InlineData("de", HelpFile.GermanFileName, HelpFile.GermanWindowTitle, "0x407")]
        [InlineData("en", HelpFile.EnglishFileName, HelpFile.EnglishWindowTitle, "0x409")]
        public void TheProject_NamesTheFileAndTheWindowThatTheProgramLooksFor(string language, string fileName, string windowTitle, string languageId)
        {
            Assert.Equal(fileName, OptionOfProject(language, "Compiled file"));
            Assert.Equal(windowTitle, OptionOfProject(language, "Title"));
            Assert.StartsWith(languageId, OptionOfProject(language, "Language"), StringComparison.OrdinalIgnoreCase);
            Assert.True(HelpFile.IsHelpWindowTitle(OptionOfProject(language, "Title")), "the program would not recognise the window of the help it opened");

            // The window that the help opens in is defined once more in [WINDOWS]; its title has to be the same.
            var window = SectionOfProject(language, "WINDOWS").Single(line => line.StartsWith("Main=", StringComparison.Ordinal));
            Assert.StartsWith("Main=\"" + windowTitle + "\"", window, StringComparison.Ordinal);
            Assert.Equal("Main", OptionOfProject(language, "Default Window"));
        }

        [Theory]
        [InlineData("de")]
        [InlineData("en")]
        public void TheCompiledHelp_ExistsAndIsACompiledHtmlHelpFile(string language)
        {
            var chm = ChmOf(language);

            Assert.True(File.Exists(chm), "the compiled help is missing: run docs/Build-Help.ps1 and commit the result");

            var bytes = File.ReadAllBytes(chm);

            Assert.True(bytes.Length > 8000, "the compiled help is too small to hold a help");
            Assert.Equal("ITSF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public void TheHost_CopiesBothCompiledHelpsToItsOutput_UnderTheNamesThatItOpens()
        {
            var project = XDocument.Load(Path.Combine(DotNetTool.RepositoryRoot(), "RFiDGear", "RFiDGear.csproj"));
            var items = project.Descendants("None").Where(item => ((string?)item.Attribute("Include") ?? string.Empty).EndsWith(".chm", StringComparison.OrdinalIgnoreCase)).ToList();

            Assert.Equal(2, items.Count);

            foreach (var (language, name) in new[] { ("de", HelpFile.GermanFileName), ("en", HelpFile.EnglishFileName) })
            {
                var item = items.Single(candidate => string.Equals((string?)candidate.Attribute("Link"), name, StringComparison.OrdinalIgnoreCase));

                Assert.Equal(Path.GetFullPath(ChmOf(language)), Path.GetFullPath(Path.Combine(DotNetTool.RepositoryRoot(), "RFiDGear", ((string?)item.Attribute("Include"))!)), ignoreCase: true);
                Assert.Equal("PreserveNewest", item.Element("CopyToOutputDirectory")?.Value);
            }
        }
    }
}
