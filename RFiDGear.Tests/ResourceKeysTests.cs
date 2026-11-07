using RFiDGear.ViewModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The texts of the Check: App Config and Check: App Key Count dialogs. A resource key that does not exist gives an
    /// empty text and no error, so a typo in a key, or a language that was forgotten, would only show as a blank label.
    /// </summary>
    public class ResourceKeysTests
    {
        private static readonly string[] Keys =
        {
            "labelExpectedDesfireKeySettings",
            "groupBoxHeaderMifareDesfireExpectedAppSettings",
            "labelAppSettingsCompareAll",
            "labelAppSettingsCompareAny",
            "labelAppSettingsCompareExact",
            "textBoxStatusNoKeySettingFlagSelected",
            "textBoxStatusInvalidApplicationId",
            "textBoxStatusFileSettingsUnsupportedFileType",
            "textBoxStatusFileSettingsInvalidInput",
            "textBoxStatusFileSettingsCompared",
            "textBoxStatusFileSettingsUnreadable",
            "labelExpectedChangeKeyPolicy",
            "labelContentMifareDesfireAppId",
            "labelContentMifareDesfireMaxKeys",
            "buttonContentMifareDesfirePerformCompare",
            "groupBoxHeaderMifareDesfireExpectedKeyCount",
            "textBoxStatusAppSettingsCompared",
            "textBoxStatusAppKeyCountCompared",
            "textBoxStatusAppSettingsUnreadable",
            "textBoxStatusCompareModeAll",
            "textBoxStatusCompareModeAny",
            "textBoxStatusCompareModeExact",
            "textBoxStatusComparePass",
            "textBoxStatusCompareFail",
        };

        private static readonly ResourceManager Manifest =
            new ResourceManager("RFiDGear.Resources.Manifest", typeof(MainWindowViewModel).Assembly);

        public static IEnumerable<object[]> AllKeys() => Keys.Select(key => new object[] { key });

        [Theory]
        [MemberData(nameof(AllKeys))]
        public void EveryKey_HasATextInBothLanguages_ThatIsTranslated(string key)
        {
            var english = Manifest.GetString(key, new CultureInfo("en"));
            var german = Manifest.GetString(key, new CultureInfo("de"));

            Assert.False(string.IsNullOrWhiteSpace(english), key + " (en)");
            Assert.False(string.IsNullOrWhiteSpace(german), key + " (de)");
            Assert.NotEqual(english, german);
        }

        [Theory]
        [MemberData(nameof(AllKeys))]
        public void EveryKey_HasTheSamePlaceholdersInBothLanguages(string key)
        {
            var english = Placeholders(Manifest.GetString(key, new CultureInfo("en")));
            var german = Placeholders(Manifest.GetString(key, new CultureInfo("de")));

            Assert.Equal(english, german);
        }

        private static string[] Placeholders(string? text) =>
            Regex.Matches(text ?? string.Empty, @"\{(\d+)(?:[:,][^}]*)?\}")
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
    }
}
