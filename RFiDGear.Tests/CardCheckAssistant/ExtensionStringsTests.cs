using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using RFiDGear.Extensions.CardCheckAssistant.Localization;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The page's English and German texts must stay in step, and the language choice must be predictable.</summary>
    public class ExtensionStringsTests
    {
        [Fact]
        public void BothLanguages_HaveExactlyTheSameKeys()
        {
            var english = ExtensionStrings.AllKeys.ToHashSet(StringComparer.Ordinal);
            var german = ExtensionStrings.GermanKeys.ToHashSet(StringComparer.Ordinal);

            Assert.Empty(english.Except(german));
            Assert.Empty(german.Except(english));
        }

        [Fact]
        public void EveryDeclaredKey_HasAText_AndEveryTextHasAKey()
        {
            var declared = typeof(ExtensionStrings.Keys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => (string)field.GetRawConstantValue()!)
                .ToHashSet(StringComparer.Ordinal);

            Assert.True(declared.SetEquals(ExtensionStrings.AllKeys), "a declared key has no text, or a text has no declared key");
        }

        [Fact]
        public void NoTextIsEmpty()
        {
            foreach (var key in ExtensionStrings.AllKeys)
            {
                Assert.False(string.IsNullOrWhiteSpace(ExtensionStrings.Get(key, "en-US")), "English " + key);
                Assert.False(string.IsNullOrWhiteSpace(ExtensionStrings.Get(key, "de-DE")), "German " + key);
            }
        }

        [Fact]
        public void BothLanguages_UseTheSamePlaceholdersForEveryKey()
        {
            foreach (var key in ExtensionStrings.AllKeys)
            {
                Assert.Equal(Placeholders(ExtensionStrings.Get(key, "en-US")), Placeholders(ExtensionStrings.Get(key, "de-DE")));
            }
        }

        [Fact]
        public void TheGermanTexts_AreTranslated_ExceptThoseThatAreTheSameInBothLanguages()
        {
            var sameInBoth = new HashSet<string>(StringComparer.Ordinal)
            {
                ExtensionStrings.Keys.ColumnPosition,
                ExtensionStrings.Keys.RowOk,
                ExtensionStrings.Keys.RowUnknown,
            };

            foreach (var key in ExtensionStrings.AllKeys.Where(key => !sameInBoth.Contains(key)))
            {
                Assert.NotEqual(ExtensionStrings.Get(key, "en-US"), ExtensionStrings.Get(key, "de-DE"));
            }
        }

        [Theory]
        [InlineData("de-DE", true)]
        [InlineData("de", true)]
        [InlineData("DE-at", true)]
        [InlineData("de_CH", true)]
        [InlineData(" de-DE ", true)]
        [InlineData("en-US", false)]
        [InlineData("fr-FR", false)]
        [InlineData("dex", false)]
        [InlineData("d", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        public void OnlyGermanCulturesSelectGerman(string? cultureName, bool expected)
        {
            Assert.Equal(expected, ExtensionStrings.IsGerman(cultureName));
        }

        [Fact]
        public void AnUnknownKey_ComesBackAsItself_SoAMissingTextIsVisibleNotBlank()
        {
            Assert.Equal("NoSuchKey", ExtensionStrings.Get("NoSuchKey", "de-DE"));
            Assert.Equal("NoSuchKey", ExtensionStrings.Get("NoSuchKey", "en-US"));
        }

        [Fact]
        public void TheBindingSource_ChangesLanguage_AndTellsEveryBoundText()
        {
            var strings = new LocalizedStrings("en-US");
            var raised = new List<string?>();
            strings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
            Assert.Equal("Run once", strings[ExtensionStrings.Keys.RunButton]);

            strings.Refresh("de-DE");

            Assert.Equal("Einmal ausführen", strings[ExtensionStrings.Keys.RunButton]);
            Assert.Equal(new[] { "Item[]" }, raised);

            strings.Refresh(null);   // no language at all is English
            Assert.Equal("Run once", strings[ExtensionStrings.Keys.RunButton]);
        }

        [Fact]
        public void Format_FillsInThePlaceholders()
        {
            var strings = new LocalizedStrings("en-US");

            Assert.Equal("2 of 5 tasks failed", strings.Format(ExtensionStrings.Keys.SummaryFormat, 2, 5));
        }

        private static string Placeholders(string text) =>
            string.Join(",", Regex.Matches(text, @"\{(\d+)\}").Select(match => match.Groups[1].Value).Distinct().OrderBy(value => value));
    }
}
