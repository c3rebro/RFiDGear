using System.Linq;
using System.Text.RegularExpressions;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>
    /// The template catalog against the old assistant's table (<see cref="LegacyTemplateRows"/>, generated from
    /// its source) and against its own display texts.
    /// </summary>
    public sealed class LsmTemplateCatalogTests
    {
        private static readonly (string Text, int SizeInBytes, int SizeInFreeSectorsCount)[] RealLegacyRows =
            LegacyTemplateRows.Rows.Where(row => row.Text != "N/A").ToArray();

        private static string NameOf(string displayText) => displayText.Split(' ')[0];

        [Fact]
        public void TheLegacyTableHasFourteenRows_AndTheFirstIsThePlaceholder()
        {
            Assert.Equal(14, LegacyTemplateRows.Rows.Length);
            Assert.Equal("N/A", LegacyTemplateRows.Rows[0].Text);
            Assert.Equal(13, RealLegacyRows.Length);
        }

        [Fact]
        public void Catalog_HoldsEveryLegacyTemplateInLegacyOrder_AndNotThePlaceholder()
        {
            Assert.Equal(RealLegacyRows.Select(row => row.Text), LsmTemplateCatalog.All.Select(t => t.DisplayText));
            Assert.Equal(RealLegacyRows.Select(row => NameOf(row.Text)), LsmTemplateCatalog.All.Select(t => t.Name));
            Assert.DoesNotContain(LsmTemplateCatalog.All, t => t.Name == "N/A");
        }

        [Fact]
        public void Catalog_SizesAreTheLegacyOnes_ExceptTheCorrectedMD10000L_AV()
        {
            foreach (var row in RealLegacyRows)
            {
                var template = LsmTemplateCatalog.All.Single(t => t.Name == NameOf(row.Text));
                var expectedBytes = template.Name == "MD10000L_AV" ? 3232 : row.SizeInBytes;

                Assert.Equal(expectedBytes, template.SizeInBytes);
                Assert.Equal(row.SizeInFreeSectorsCount, template.SizeInFreeSectors);
            }
        }

        [Fact]
        public void TheLegacyTableCarriedTheWrongSizeForMD10000L_AV_ACopyOfTheRowAbove()
        {
            // The reason for the one deliberate difference: the old table says 1760 here, its own text says 3232.
            var legacy = LegacyTemplateRows.Rows.Single(row => row.Text.StartsWith("MD10000L_AV", System.StringComparison.Ordinal));
            var above = LegacyTemplateRows.Rows.Single(row => row.Text.StartsWith("MD4000L_AV", System.StringComparison.Ordinal));

            Assert.Equal(1760, legacy.SizeInBytes);
            Assert.Equal(above.SizeInBytes, legacy.SizeInBytes);
            Assert.Equal(3232, LsmTemplateCatalog.All.Single(t => t.Name == "MD10000L_AV").SizeInBytes);
        }

        [Fact]
        public void Catalog_EverySizeAgreesWithWhatItsOwnDisplayTextSays()
        {
            // This check would have caught the 1760.
            foreach (var template in LsmTemplateCatalog.All)
            {
                if (template.Family == TemplateFamily.Desfire)
                {
                    var bytes = Regex.Match(template.DisplayText, @"= (\d+) Bytes");
                    Assert.True(bytes.Success, template.DisplayText);
                    Assert.Equal(int.Parse(bytes.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), template.SizeInBytes);
                    Assert.Equal(0, template.SizeInFreeSectors);
                }
                else
                {
                    var sectors = Regex.Match(template.DisplayText, @"Speicherbedarf: (\d+) Sektor");
                    Assert.True(sectors.Success, template.DisplayText);
                    Assert.Equal(int.Parse(sectors.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), template.SizeInFreeSectors);
                    Assert.Equal(0, template.SizeInBytes);
                }
            }
        }

        [Fact]
        public void Catalog_FamilyFollowsThePrefixOfTheName_AndNamesAreUnique()
        {
            Assert.All(LsmTemplateCatalog.All, t =>
                Assert.Equal(t.Name.StartsWith("MC", System.StringComparison.Ordinal) ? TemplateFamily.Classic : TemplateFamily.Desfire, t.Family));
            Assert.Equal(LsmTemplateCatalog.All.Count, LsmTemplateCatalog.All.Select(t => t.Name).Distinct().Count());
        }

        [Fact]
        public void Catalog_IsSortedByNeedWithinEachFamily_SoSelectionCanKeepDisplayOrder()
        {
            var desfire = LsmTemplateCatalog.All.Where(t => t.Family == TemplateFamily.Desfire).Select(t => t.SizeInBytes).ToList();
            var classic = LsmTemplateCatalog.All.Where(t => t.Family == TemplateFamily.Classic).Select(t => t.SizeInFreeSectors).ToList();

            Assert.Equal(desfire.OrderBy(x => x), desfire);
            Assert.Equal(classic.OrderBy(x => x), classic);
        }

        [Fact]
        public void TheSmallestDesfireTemplateNeeds224Bytes_ButTheGateIs225()
        {
            // Kept as the old assistant had it: a chip with exactly 224 bytes free is turned away.
            Assert.Equal(224, LsmTemplateCatalog.All.Where(t => t.Family == TemplateFamily.Desfire).Min(t => t.SizeInBytes));
            Assert.Equal(225, TemplateRules.DesfireMinimumFreeBytes);
        }

        [Fact]
        public void Template_NeedsAName()
        {
            Assert.Throws<System.ArgumentException>(() => new LsmTemplate(" ", "text", TemplateFamily.Classic, 0, 1));
        }
    }
}
