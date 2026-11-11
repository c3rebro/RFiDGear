using System;
using System.Collections.Generic;
using System.IO;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class ProjectVariantsTests
    {
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "rfidgear-variants");

        private static readonly string Default = Path.Combine(Folder, "check.rfPrj");
        private static readonly string German = Path.Combine(Folder, "check.deutsch.rfPrj");
        private static readonly string English = Path.Combine(Folder, "check.englisch.rfPrj");

        private static string VariantOf(ReportLanguage language) => language == ReportLanguage.German ? German : English;

        private static Func<string, bool> Existing(params string[] files) =>
            path => Array.Exists(files, file => string.Equals(file, path, StringComparison.OrdinalIgnoreCase));

        [Fact]
        public void VariantWord_IsTheLowerCaseGermanWordForEachLanguage()
        {
            Assert.Equal("deutsch", ProjectVariants.VariantWord(ReportLanguage.German));
            Assert.Equal("englisch", ProjectVariants.VariantWord(ReportLanguage.English));
        }

        [Fact]
        public void VariantWord_OfAnUndefinedLanguage_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ProjectVariants.VariantWord((ReportLanguage)99));
        }

        [Theory]
        [InlineData(ReportLanguage.German)]
        [InlineData(ReportLanguage.English)]
        public void Resolve_WithBothVariantsPresent_PicksTheOneOfTheChosenLanguage(ReportLanguage language)
        {
            Assert.Equal(VariantOf(language), ProjectVariants.Resolve(Default, language, Existing(Default, German, English)));
        }

        [Theory]
        [InlineData(ReportLanguage.German)]
        [InlineData(ReportLanguage.English)]
        public void Resolve_WithoutAVariant_FallsBackToTheDefaultProject(ReportLanguage language)
        {
            Assert.Equal(Default, ProjectVariants.Resolve(Default, language, Existing(Default)));
        }

        [Fact]
        public void Resolve_OnlyTheOtherLanguagesVariantExists_FallsBackToTheDefaultProject()
        {
            Assert.Equal(Default, ProjectVariants.Resolve(Default, ReportLanguage.German, Existing(Default, English)));
            Assert.Equal(Default, ProjectVariants.Resolve(Default, ReportLanguage.English, Existing(Default, German)));
        }

        [Fact]
        public void Resolve_DoesNotRequireTheDefaultProjectToExist()
        {
            Assert.Equal(Default, ProjectVariants.Resolve(Default, ReportLanguage.German, Existing()));
        }

        [Fact]
        public void Resolve_AsksOnlyAboutTheVariantFile()
        {
            var asked = new List<string>();

            ProjectVariants.Resolve(Default, ReportLanguage.English, path =>
            {
                asked.Add(path);
                return false;
            });

            Assert.Equal(new[] { English }, asked);
        }

        [Fact]
        public void Resolve_KeepsTheExtensionOfTheName_AndUsesANameWithInnerDotsAsItIs()
        {
            var inner = Path.Combine(Folder, "my.project.v2.rfPrj");
            var innerGerman = Path.Combine(Folder, "my.project.v2.deutsch.rfPrj");
            var plain = Path.Combine(Folder, "check.xml");
            var plainGerman = Path.Combine(Folder, "check.deutsch.xml");

            // More than one dot: the project is used as it is, even though a file that looks like a variant exists.
            Assert.Equal(inner, ProjectVariants.Resolve(inner, ReportLanguage.German, Existing(innerGerman)));
            Assert.Equal(plainGerman, ProjectVariants.Resolve(plain, ReportLanguage.German, Existing(plainGerman)));
        }

        [Fact]
        public void Resolve_ForAProjectWithoutAFolder_StaysRelative()
        {
            Assert.Equal("check.englisch.rfPrj", ProjectVariants.Resolve("check.rfPrj", ReportLanguage.English, Existing("check.englisch.rfPrj")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Resolve_WithoutAProject_Throws(string? project)
        {
            Assert.Throws<ArgumentException>(() => ProjectVariants.Resolve(project!, ReportLanguage.German, Existing()));
        }

        [Fact]
        public void Resolve_WithoutAnExistenceCheck_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ProjectVariants.Resolve(Default, ReportLanguage.German, null!));
        }

        // ── a name with more than one dot, and what Settings shows ──

        [Theory]
        [InlineData("my.check.rfPrj")]
        [InlineData("check.deutsch.rfPrj")]
        [InlineData("check.french.rfPrj")]
        public void Resolve_OfANameWithMoreThanOneDot_UsesTheFileAsItIs_WithoutLookingForAVariant(string fileName)
        {
            var project = Path.Combine(Folder, fileName);
            var asked = false;

            var resolved = ProjectVariants.Resolve(project, ReportLanguage.German, _ =>
            {
                asked = true;
                return true;
            });

            Assert.Equal(project, resolved);
            Assert.False(asked);
        }

        private static IEnumerable<string> FolderWith(params string[] names)
        {
            var files = new List<string>();
            foreach (var name in names)
            {
                files.Add(Path.Combine(Folder, name));
            }

            return files;
        }

        [Fact]
        public void Inspect_OfAPlainProject_ListsEveryVariantNextToIt_SortedAndWithoutOtherFiles()
        {
            var info = ProjectVariants.Inspect(
                Default,
                _ => FolderWith("check.rfPrj", "check.french.rfPrj", "check.Deutsch.rfPrj", "other.deutsch.rfPrj", "check.deutsch.txt", "check.a.b.rfPrj", "checkx.rfPrj"));

            Assert.Equal(ProjectNameKind.Plain, info.Kind);
            Assert.Equal(new[] { "Deutsch", "french" }, info.Variants);
            Assert.Null(info.ReservedWord);
        }

        [Fact]
        public void Inspect_OfAProjectWithoutVariants_FindsNone()
        {
            var info = ProjectVariants.Inspect(Default, _ => FolderWith("check.rfPrj"));

            Assert.Equal(ProjectNameKind.Plain, info.Kind);
            Assert.Empty(info.Variants);
        }

        [Theory]
        [InlineData("my.check.rfPrj", ProjectNameKind.MoreThanOneDot, null)]
        [InlineData("check.french.rfPrj", ProjectNameKind.MoreThanOneDot, null)]
        [InlineData("check.deutsch.rfPrj", ProjectNameKind.LooksLikeVariant, "deutsch")]
        [InlineData("check.ENGLISCH.rfPrj", ProjectNameKind.LooksLikeVariant, "englisch")]
        public void Inspect_OfADottedName_SaysSoWithoutLookingAtTheFolder(string fileName, ProjectNameKind kind, string? word)
        {
            var info = ProjectVariants.Inspect(Path.Combine(Folder, fileName), _ => throw new InvalidOperationException("The folder must not be read."));

            Assert.Equal(kind, info.Kind);
            Assert.Equal(word, info.ReservedWord);
            Assert.Empty(info.Variants);
        }
    }
}
