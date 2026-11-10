using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class LsmAnswersTests
    {
        private static LsmTemplate Template(string name) => LsmTemplateCatalog.All.Single(t => t.Name == name);

        // Every text field is filled, so a test shows which of them a rule leaves out.
        private static LsmAnswers Answered(bool programmed, bool passed, bool restricted) => new LsmAnswers
        {
            ProgrammingSucceeded = programmed,
            FunctionTestPassed = passed,
            HasRestrictions = restricted,
            RestrictionText = "Nur Tuer 1",
            SectorsUsed = "1-4",
        };

        private static void AssertFields(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual) =>
            Assert.Equal(expected.OrderBy(p => p.Key, StringComparer.Ordinal), actual.OrderBy(p => p.Key, StringComparer.Ordinal));

        // Golden: what the old assistant wrote for these answers (its Step2PageViewModel, "Fertigstellen" of a programmable chip).
        [Fact]
        public void ToFields_ForAClassicTemplate_WritesWhatTheOldAssistantWrote()
        {
            var result = Answered(programmed: true, passed: false, restricted: true).ToFields(Template("MC1000L_AV"));

            Assert.True(result.IsSuccess);
            AssertFields(
                new Dictionary<string, string>
                {
                    ["CheckBox_Detail_Reserved_2_1"] = "Yes",
                    ["CheckBox_Detail_Reserved_2_2"] = "Off",
                    ["CheckBox_Detail_Reserved_3_1"] = "Off",
                    ["CheckBox_Detail_Reserved_3_2"] = "Yes",
                    ["CheckBox_Detail_Reserved_4_1"] = "Yes",
                    ["CheckBox_Detail_Reserved_4_2"] = "Off",
                    ["TextBox_Notes_Site_2"] = "Nur Tuer 1",
                    ["ComboBox_UsedTemplate"] = "MC1000L_AV (Speicherbedarf: 11 Sektoren)",
                    ["TextBox_Detail_Mem_3"] = "1-4",
                },
                result.Value);
        }

        [Fact]
        public void ToFields_ForADesfireTemplateWithoutRestrictions_LeavesOutTheNotesAndTheSectors()
        {
            var result = Answered(programmed: false, passed: true, restricted: false).ToFields(Template("MD4000L_AV"));

            Assert.True(result.IsSuccess);
            AssertFields(
                new Dictionary<string, string>
                {
                    ["CheckBox_Detail_Reserved_2_1"] = "Off",
                    ["CheckBox_Detail_Reserved_2_2"] = "Yes",
                    ["CheckBox_Detail_Reserved_3_1"] = "Yes",
                    ["CheckBox_Detail_Reserved_3_2"] = "Off",
                    ["CheckBox_Detail_Reserved_4_1"] = "Off",
                    ["CheckBox_Detail_Reserved_4_2"] = "Yes",
                    ["ComboBox_UsedTemplate"] = "MD4000L_AV (Speicherbedarf: 1600 + 160 = 1760 Bytes)",
                },
                result.Value);
        }

        [Theory]
        [InlineData("MC1000L_AV", false)]
        [InlineData("MC1000L_AV", true)]
        [InlineData("MD4000L_AV", false)]
        [InlineData("MD4000L_AV", true)]
        public void ToFields_WritesTheNotesOnlyWhenRestricted_AndTheSectorsOnlyForAClassicTemplate_EachOnItsOwn(string template, bool restricted)
        {
            var result = Answered(true, true, restricted).ToFields(Template(template));

            Assert.True(result.IsSuccess);
            Assert.Equal(restricted, result.Value.ContainsKey("TextBox_Notes_Site_2"));
            Assert.Equal(template.StartsWith("MC", StringComparison.Ordinal), result.Value.ContainsKey("TextBox_Detail_Mem_3"));
        }

        [Fact]
        public void ToFields_WithRestrictionsAndAnEmptyText_WritesTheEmptyNotes_SoTheOperatorCanClearThem()
        {
            var result = (Answered(true, true, restricted: true) with { RestrictionText = string.Empty }).ToFields(Template("MD4000L_AV"));

            Assert.True(result.IsSuccess);
            Assert.Equal(string.Empty, result.Value["TextBox_Notes_Site_2"]);
        }

        [Fact]
        public void ToFields_WithRestrictionsAndNoText_LeavesTheNotesOfTheFirstReportAlone()
        {
            var result = (Answered(true, true, restricted: true) with { RestrictionText = null }).ToFields(Template("MD4000L_AV"));

            Assert.True(result.IsSuccess);
            Assert.False(result.Value.ContainsKey("TextBox_Notes_Site_2"));
            Assert.Equal("Yes", result.Value["CheckBox_Detail_Reserved_4_1"]);
        }

        [Theory]
        [InlineData(null, null, null, "answers.programming.missing")]
        [InlineData(null, true, true, "answers.programming.missing")]
        [InlineData(true, null, null, "answers.functionTest.missing")]
        [InlineData(true, true, null, "answers.restrictions.missing")]
        public void ToFields_WithAQuestionNotAnswered_NamesTheFirstOne_AndWritesNothing(bool? programmed, bool? passed, bool? restricted, string code)
        {
            var answers = new LsmAnswers { ProgrammingSucceeded = programmed, FunctionTestPassed = passed, HasRestrictions = restricted };

            var result = answers.ToFields(Template("MD4000L_AV"));

            Assert.False(result.IsSuccess);
            Assert.Equal(code, result.Failure!.Code);
            Assert.Equal(FailureKind.InvalidInput, result.Failure.Kind);
        }

        [Fact]
        public void ToFields_HandsOutAFixedSet_ThatTheCallerCannotChange()
        {
            var result = Answered(true, true, true).ToFields(Template("MC1000L_AV"));

            Assert.IsType<ReadOnlyDictionary<string, string>>(result.Value);
        }

        [Fact]
        public void ToFields_WithoutATemplate_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Answered(true, true, true).ToFields(null!));
        }
    }
}
