using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class TemplateRulesTests
    {
        private static FreeMemory Desfire(int bytes) => new FreeMemory(MemoryKind.Desfire, bytes);

        private static FreeMemory Classic(int sectors) => new FreeMemory(MemoryKind.Classic, sectors);

        private static string[] Names(FreeMemory memory) => TemplateRules.AvailableTemplates(memory).Select(t => t.Name).ToArray();

        // ── reading the free-memory field ───────────────────────────────────

        [Theory]
        [InlineData("1759", MemoryKind.Desfire, 1759)]
        [InlineData("1759 Bytes", MemoryKind.Desfire, 1759)]
        [InlineData("  1759 Bytes  ", MemoryKind.Desfire, 1759)]
        [InlineData(" 300", MemoryKind.Desfire, 300)]
        [InlineData("007", MemoryKind.Desfire, 7)]
        [InlineData("+300", MemoryKind.Desfire, 300)]
        [InlineData("-5", MemoryKind.Desfire, -5)]
        [InlineData("0", MemoryKind.Desfire, 0)]
        [InlineData("224", MemoryKind.Desfire, 224)]
        [InlineData("225", MemoryKind.Desfire, 225)]
        [InlineData("2147483647", MemoryKind.Desfire, int.MaxValue)]
        [InlineData("1,2,3", MemoryKind.Classic, 3)]
        [InlineData("1, 2", MemoryKind.Classic, 2)]
        [InlineData("1,2,", MemoryKind.Classic, 2)]
        [InlineData(",1,,2", MemoryKind.Classic, 2)]
        [InlineData("5,6", MemoryKind.Classic, 2)]
        [InlineData("S5, S6, S7", MemoryKind.Classic, 3)]
        [InlineData("١٢٣, 5", MemoryKind.Classic, 1)]
        [InlineData("none, 5, n/a, 6", MemoryKind.Classic, 2)]
        public void ParseFreeMemory_ReadsBytesOrCountsSectors(string field, MemoryKind kind, int amount)
        {
            Assert.Equal(new FreeMemory(kind, amount), TemplateRules.ParseFreeMemory(field));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("NA")]
        [InlineData("N/A")]
        [InlineData("none")]
        [InlineData(",")]
        [InlineData(" , , ")]
        [InlineData("99999999999")]
        [InlineData("-99999999999")]
        [InlineData("١٢٣")]
        public void ParseFreeMemory_ASaysNothingUsable_IsUnknown(string? field)
        {
            Assert.Equal(MemoryKind.Unknown, TemplateRules.ParseFreeMemory(field).Kind);
            Assert.Equal(0, TemplateRules.ParseFreeMemory(field).Amount);
        }

        [Fact]
        public void ParseFreeMemory_ASingleSectorWrittenAsABareNumber_IsReadAsBytes()
        {
            // The old assistant had this ambiguity too: the first word being a number means DESFire.
            Assert.Equal(Desfire(5), TemplateRules.ParseFreeMemory("5"));
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        [InlineData("ar-SA")]
        public void ParseFreeMemory_GivesTheSameResultInEveryCulture(string cultureName)
        {
            WithCulture(CultureInfo.GetCultureInfo(cultureName), () =>
            {
                Assert.Equal(Desfire(1759), TemplateRules.ParseFreeMemory("1759 Bytes"));
                Assert.Equal(Desfire(-5), TemplateRules.ParseFreeMemory("-5"));
                Assert.Equal(Classic(3), TemplateRules.ParseFreeMemory("1,2,3"));
            });
        }

        [Fact]
        public void ParseFreeMemory_DoesNotUseTheSignOfTheCurrentCulture()
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
            culture.NumberFormat.NegativeSign = "~";

            WithCulture(culture, () =>
            {
                Assert.Equal(Desfire(-5), TemplateRules.ParseFreeMemory("-5"));
                Assert.Equal(MemoryKind.Classic, TemplateRules.ParseFreeMemory("~5, 6").Kind);
            });
        }

        private static void WithCulture(CultureInfo culture, Action action)
        {
            var previous = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = culture;
                action();
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        // ── is there enough memory ──────────────────────────────────────────

        [Theory]
        [InlineData(MemoryKind.Desfire, 224, false)]
        [InlineData(MemoryKind.Desfire, 225, true)]
        [InlineData(MemoryKind.Desfire, 226, true)]
        [InlineData(MemoryKind.Desfire, 0, false)]
        [InlineData(MemoryKind.Desfire, -5, false)]
        [InlineData(MemoryKind.Classic, 0, false)]
        [InlineData(MemoryKind.Classic, 1, true)]
        [InlineData(MemoryKind.Classic, 43, true)]
        [InlineData(MemoryKind.Unknown, 0, false)]
        [InlineData(MemoryKind.Unknown, 5000, false)]
        public void HasEnoughMemory_AppliesTheGateOfEachKind(MemoryKind kind, int amount, bool expected)
        {
            Assert.Equal(expected, TemplateRules.HasEnoughMemory(new FreeMemory(kind, amount)));
        }

        // ── which templates fit ─────────────────────────────────────────────

        [Theory]
        [InlineData(224, new string[0])]
        [InlineData(225, new[] { "MDBasic" })]
        [InlineData(351, new[] { "MDBasic" })]
        [InlineData(352, new[] { "MDBasic", "MD1200L" })]
        [InlineData(703, new[] { "MDBasic", "MD1200L" })]
        [InlineData(704, new[] { "MDBasic", "MD1200L", "MD3800L" })]
        [InlineData(1183, new[] { "MDBasic", "MD1200L", "MD3800L" })]
        [InlineData(1184, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV" })]
        [InlineData(1759, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV" })]
        [InlineData(1760, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV", "MD4000L_AV" })]
        [InlineData(3231, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV", "MD4000L_AV" })]
        [InlineData(3232, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV", "MD4000L_AV", "MD10000L_AV" })]
        [InlineData(3233, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV", "MD4000L_AV", "MD10000L_AV" })]
        [InlineData(7167, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV", "MD4000L_AV", "MD10000L_AV" })]
        [InlineData(7168, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV", "MD4000L_AV", "MD10000L_AV", "MD32000L_AV" })]
        [InlineData(7169, new[] { "MDBasic", "MD1200L", "MD3800L", "MD2500L_AV", "MD4000L_AV", "MD10000L_AV", "MD32000L_AV" })]
        public void AvailableTemplates_Desfire_KeepsThoseThatNeedNoMoreBytesThanTheChipHas(int bytes, string[] expected)
        {
            Assert.Equal(expected, Names(Desfire(bytes)));
        }

        [Theory]
        [InlineData(0, new string[0])]
        [InlineData(1, new[] { "MCBasic" })]
        [InlineData(3, new[] { "MCBasic" })]
        [InlineData(4, new[] { "MCBasic", "MC1200L" })]
        [InlineData(10, new[] { "MCBasic", "MC1200L" })]
        [InlineData(11, new[] { "MCBasic", "MC1200L", "MC3800L", "MC1000L_AV" })]
        [InlineData(18, new[] { "MCBasic", "MC1200L", "MC3800L", "MC1000L_AV" })]
        [InlineData(19, new[] { "MCBasic", "MC1200L", "MC3800L", "MC1000L_AV", "MC2400L_AV" })]
        [InlineData(42, new[] { "MCBasic", "MC1200L", "MC3800L", "MC1000L_AV", "MC2400L_AV" })]
        [InlineData(43, new[] { "MCBasic", "MC1200L", "MC3800L", "MC1000L_AV", "MC2400L_AV", "MC8000L_AV" })]
        [InlineData(44, new[] { "MCBasic", "MC1200L", "MC3800L", "MC1000L_AV", "MC2400L_AV", "MC8000L_AV" })]
        public void AvailableTemplates_Classic_KeepsThoseThatNeedNoMoreSectorsThanTheChipHas(int sectors, string[] expected)
        {
            Assert.Equal(expected, Names(Classic(sectors)));
        }

        [Fact]
        public void AvailableTemplates_ForUnknownMemory_IsEmpty()
        {
            Assert.Empty(TemplateRules.AvailableTemplates(FreeMemory.Unknown));
        }

        [Fact]
        public void AvailableTemplates_NeverMixesTheFamilies()
        {
            Assert.All(TemplateRules.AvailableTemplates(Desfire(100000)), t => Assert.Equal(TemplateFamily.Desfire, t.Family));
            Assert.All(TemplateRules.AvailableTemplates(Classic(100)), t => Assert.Equal(TemplateFamily.Classic, t.Family));
        }

        // ── which one is preselected ────────────────────────────────────────

        [Theory]
        [InlineData(MemoryKind.Desfire, 7168, "MD4000L_AV")]
        [InlineData(MemoryKind.Desfire, 1760, "MD4000L_AV")]
        [InlineData(MemoryKind.Desfire, 1759, "MDBasic")]
        [InlineData(MemoryKind.Desfire, 225, "MDBasic")]
        [InlineData(MemoryKind.Classic, 43, "MC1000L_AV")]
        [InlineData(MemoryKind.Classic, 11, "MC1000L_AV")]
        [InlineData(MemoryKind.Classic, 10, "MCBasic")]
        [InlineData(MemoryKind.Classic, 1, "MCBasic")]
        public void DefaultTemplate_PrefersMD4000L_AV_ThenMC1000L_AV_ThenTheFirst(MemoryKind kind, int amount, string expected)
        {
            var available = TemplateRules.AvailableTemplates(new FreeMemory(kind, amount));

            Assert.Equal(expected, TemplateRules.DefaultTemplate(available)!.Name);
        }

        [Fact]
        public void DefaultTemplate_OfNothing_IsNull()
        {
            Assert.Null(TemplateRules.DefaultTemplate(Array.Empty<LsmTemplate>()));
            Assert.Throws<ArgumentNullException>(() => TemplateRules.DefaultTemplate(null!));
        }

        // ── the verdict ─────────────────────────────────────────────────────

        public static IEnumerable<object[]> VerdictTable()
        {
            // supported, programmable, memory, expected
            var enough = Desfire(1760);
            var tooLittle = Desfire(224);

            foreach (var memory in new[] { enough, tooLittle, FreeMemory.Unknown })
            {
                yield return new object[] { false, false, memory, ChipVerdict.NotSupported };
                yield return new object[] { true, false, memory, ChipVerdict.NotSupported };
                yield return new object[] { false, true, memory, ChipVerdict.Indeterminate };
            }

            yield return new object[] { true, true, enough, ChipVerdict.Programmable };
            yield return new object[] { true, true, Classic(1), ChipVerdict.Programmable };
            yield return new object[] { true, true, tooLittle, ChipVerdict.SupportedOnly };
            yield return new object[] { true, true, Classic(0), ChipVerdict.SupportedOnly };
            yield return new object[] { true, true, FreeMemory.Unknown, ChipVerdict.SupportedOnly };
        }

        [Theory]
        [MemberData(nameof(VerdictTable))]
        public void Classify_GivesTheVerdictOfTheTable(bool supported, bool programmable, FreeMemory memory, ChipVerdict expected)
        {
            Assert.Equal(expected, TemplateRules.Classify(supported, programmable, memory));
        }

        [Fact]
        public void Classify_HasAVerdictForEveryCombination()
        {
            var memories = new[] { Desfire(-1), Desfire(224), Desfire(225), Classic(0), Classic(1), FreeMemory.Unknown };

            foreach (var supported in new[] { false, true })
            {
                foreach (var programmable in new[] { false, true })
                {
                    foreach (var memory in memories)
                    {
                        Assert.True(Enum.IsDefined(typeof(ChipVerdict), TemplateRules.Classify(supported, programmable, memory)));
                    }
                }
            }
        }

        [Fact]
        public void Programmable_MeansAtLeastOneTemplateFitsAndOneIsPreselected()
        {
            var memories = Enumerable.Range(-5, 8200).Select(Desfire).Concat(Enumerable.Range(0, 70).Select(Classic)).Append(FreeMemory.Unknown);

            foreach (var memory in memories)
            {
                var programmable = TemplateRules.Classify(true, true, memory) == ChipVerdict.Programmable;
                var available = TemplateRules.AvailableTemplates(memory);

                Assert.Equal(programmable, available.Count > 0);
                Assert.Equal(programmable, TemplateRules.DefaultTemplate(available) != null);
            }
        }

        [Fact]
        public void NullMemory_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => TemplateRules.HasEnoughMemory(null!));
            Assert.Throws<ArgumentNullException>(() => TemplateRules.AvailableTemplates(null!));
        }
    }
}
