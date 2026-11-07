using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class TemplateSelectionTests
    {
        private static ReportAnalysis Analysis(bool supported, bool programmable, string memory) =>
            new ReportAnalysis { Supported = supported, Programmable = programmable, FreeMemoryText = memory, ChipType = "type", Notes = "notes" };

        private static readonly string[] AllDesfire = { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV", "MD4000L_AV", "MD10000L_AV", "MD32000L_AV" };

        public static IEnumerable<object?[]> Table()
        {
            // supported, programmable, free memory field, expected verdict, expected templates, expected preselection
            yield return new object?[] { true, true, "1760", ChipVerdict.Programmable, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV", "MD4000L_AV" }, "MD4000L_AV" };
            yield return new object?[] { true, true, "7168 Bytes", ChipVerdict.Programmable, AllDesfire, "MD4000L_AV" };
            yield return new object?[] { true, true, "1759", ChipVerdict.Programmable, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV" }, "MDBasic" };
            yield return new object?[] { true, true, "1,2,3", ChipVerdict.Programmable, new[] { "MCBasic" }, "MCBasic" };
            yield return new object?[] { true, true, "1,2,3,4,5,6,7,8,9,10,11", ChipVerdict.Programmable, new[] { "MCBasic", "MC1200L", "MC3800L", "MC1000L_AV" }, "MC1000L_AV" };
            yield return new object?[] { true, true, "224", ChipVerdict.SupportedOnly, Array.Empty<string>(), null };
            yield return new object?[] { true, true, "N/A", ChipVerdict.SupportedOnly, Array.Empty<string>(), null };
            yield return new object?[] { true, true, "", ChipVerdict.SupportedOnly, Array.Empty<string>(), null };
            yield return new object?[] { true, false, "7168", ChipVerdict.NotSupported, Array.Empty<string>(), null };
            yield return new object?[] { false, false, "", ChipVerdict.NotSupported, Array.Empty<string>(), null };
            yield return new object?[] { false, true, "7168", ChipVerdict.Indeterminate, Array.Empty<string>(), null };
        }

        [Theory]
        [MemberData(nameof(Table))]
        public void Select_AppliesAllTheRulesToAFirstReport(bool supported, bool programmable, string memory, ChipVerdict verdict, string[] templates, string? preselected)
        {
            var selection = TemplateRules.Select(Analysis(supported, programmable, memory));

            Assert.Equal(verdict, selection.Verdict);
            Assert.Equal(templates, selection.Available.Select(t => t.Name));
            Assert.Equal(preselected, selection.Default?.Name);
        }

        [Theory]
        [MemberData(nameof(Table))]
        public void Select_IsMadeOfTheSingleRules_NotOfAnythingElse(bool supported, bool programmable, string memory, ChipVerdict verdict, string[] templates, string? preselected)
        {
            var parsed = TemplateRules.ParseFreeMemory(memory);
            var selection = TemplateRules.Select(Analysis(supported, programmable, memory));

            Assert.Equal(parsed, selection.Memory);
            Assert.Equal(TemplateRules.Classify(supported, programmable, parsed), selection.Verdict);
            Assert.NotNull(templates);
            Assert.Equal(preselected, TemplateRules.DefaultTemplate(selection.Available)?.Name);
            Assert.Equal(verdict, selection.Verdict);
        }

        [Fact]
        public void Select_OnlyAProgrammableChipGetsTemplates_EvenWithPlentyOfMemory()
        {
            Assert.Empty(TemplateRules.Select(Analysis(true, false, "7168")).Available);
            Assert.Empty(TemplateRules.Select(Analysis(false, true, "7168")).Available);
            Assert.Empty(TemplateRules.Select(Analysis(false, false, "7168")).Available);
        }

        [Fact]
        public void Select_TheListOfAvailableTemplates_CannotBeChangedByAnyoneAfterwards()
        {
            var selection = TemplateRules.Select(Analysis(true, true, "1760"));
            var before = selection.Available.Select(t => t.Name).ToArray();

            Assert.Throws<NotSupportedException>(() => ((IList<LsmTemplate>)selection.Available).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<LsmTemplate>)selection.Available).Add(LsmTemplateCatalog.All[0]));
            Assert.False(selection.Available is List<LsmTemplate>);
            Assert.False(selection.Available is LsmTemplate[]);
            Assert.Equal(before, selection.Available.Select(t => t.Name));
        }

        [Fact]
        public void Select_ChangingTheListThatWasPassedIn_ChangesNothing()
        {
            var passedIn = new List<LsmTemplate> { LsmTemplateCatalog.All[0] };
            var selection = new TemplateSelection(ChipVerdict.Programmable, new FreeMemory(MemoryKind.Desfire, 1760), passedIn, null);

            passedIn.Clear();

            Assert.Single(selection.Available);
        }

        [Fact]
        public void Select_ThePreselectedTemplate_IsOneOfTheAvailableOnes()
        {
            foreach (var bytes in new[] { 225, 352, 1760, 3232, 7168, 20000 })
            {
                var selection = TemplateRules.Select(Analysis(true, true, bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)));

                Assert.Contains(selection.Default, selection.Available);
            }
        }

        [Fact]
        public void Select_WithoutAnAnalysis_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => TemplateRules.Select(null!));
        }

        [Fact]
        public void ReportAnalysis_DefaultsAreEmptyNotNull()
        {
            var empty = new ReportAnalysis();

            Assert.False(empty.Supported);
            Assert.False(empty.Programmable);
            Assert.Equal(string.Empty, empty.ChipType);
            Assert.Equal(string.Empty, empty.FreeMemoryText);
            Assert.Equal(string.Empty, empty.Notes);
        }
    }
}
