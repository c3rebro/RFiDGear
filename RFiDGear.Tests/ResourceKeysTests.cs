using RFiDGear.Infrastructure;
using RFiDGear.ViewModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The texts of the Check: App Config, Check: App Key Count and Check: File Settings dialogs. A resource key that
    /// does not exist gives an empty text and no error, so a typo in a key, or a language that was forgotten, would only
    /// show as a blank label.
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
            "textBoxStatusFileSettingsNoReader",
            "labelFileSettingsCompareAll",
            "labelFileSettingsCompareAny",
            "ENUM.NumericRelation.Equal",
            "ENUM.NumericRelation.ActualAtLeastExpected",
            "ENUM.NumericRelation.ActualAtMostExpected",
            "toolTipCheckBoxFileSettingCompare",
            "toolTipComboBoxFileSizeRelation",
            "textBoxStatusFileSettingsNothingSelected",
            "textBoxStatusFileSettingsDiffering",
            "textBoxStatusFileSettingNameFileType",
            "textBoxStatusFileSettingNameSize",
            "textBoxStatusFileSettingNameCommunicationMode",
            "textBoxStatusFileSettingNameRead",
            "textBoxStatusFileSettingNameWrite",
            "textBoxStatusFileSettingNameReadWrite",
            "textBoxStatusFileSettingNameChange",
            "buttonContentMifareDesfireFileMasteringCheckFileSettings",
            "ENUM.TaskType_MifareDesfireTask.CheckFileSettings",
            "labelContentMifareDesfireCommunicationMode",
            "labelContentMifareDesfireBytes",
            "textBoxStatusFileSettingsInvalidKeyNumber",
            "textBoxStatusFileSettingsUnknownCommunicationMode",
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
            "statusHeaderReaderIsInitializing",
            "menuItemExtensionsHeader",
            "textBoxStatusKeyUnavailable",
            "buttonContentKeySlotUseReference",
            "buttonContentKeySlotUseKey",
            "toolTipKeySlotMode",
            "toolTipKeySlotReference",
            "toolTipKeySlotProvider",
            "buttonContentKeySlotStoreKey",
            "toolTipKeySlotStoreKey",
            "captionKeySlot",
            "messageKeySlotReplaceKey",
            "windowTitleKeySlotStoreKey",
            "labelKeySlotStoreKeyPrompt",
            "buttonContentKeySlotStoreKeyOk",
            "buttonContentKeySlotStoreKeyCancel",
            "messageKeySlotKeyStored",
            "messageKeySlotKeyNotStored",
            "messageKeySlotKeyInvalid",
            "keySlotProviderNameCredentialManager",
            "keySlotProviderToolTipCredentialManager",
            "keySlotProviderNameBridgeClient",
            "keySlotProviderToolTipBridgeClient",
            "keySlotProviderToolTipExtension",
            "keySlotSupplierStatusOff",
            "keySlotSupplierStatusNoSupplier",
            "keySlotSupplierStatusConnected",
            "menuItemMigrateProjectKeys",
            "captionKeyMigration",
            "windowCaptionKeyMigrationSource",
            "windowCaptionKeyMigrationTarget",
            "messageKeyMigrationIntro",
            "messageKeyMigrationDone",
            "messageKeyMigrationNotMoved",
            "messageKeyMigrationNothing",
            "messageKeyMigrationFailed",
            "messageKeyMigrationSameFile",
            "messageKeyMigrationTargetExists",
            "messageKeyMigrationSourceUnreadable",
            "messageKeyMigrationKeyStoreRefused",
            "messageKeyMigrationKeyStoreUnavailable",
            "messageKeyMigrationReadBackDiffers",
            "messageKeyMigrationTargetNotWritten",
            "messageKeyMigrationVerificationFailed",
            "messageKeyMigrationCancelled",
            "messageKeyMigrationUnexpected",
            "messageKeyMigrationAskDelete",
            "messageKeyMigrationDeleted",
            "messageKeyMigrationNotDeleted",
            "messageKeyMigrationUndone",
            "messageKeyMigrationNotUndone",
            "labelEnableExternalControl",
            "toolTipEnableExternalControl",
            "labelExternalControlStatusBar",
            "messageBoxCloseDuringBridgeRunCaption",
            "messageBoxCloseDuringBridgeRunMessage",
        };

        // The unit of a size, and the INIT of the status bar, read the same in both languages.
        private static readonly string[] SameInBothLanguages = { "labelContentMifareDesfireBytes", "statusHeaderReaderIsInitializing" };

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
            if (!SameInBothLanguages.Contains(key))
            {
                Assert.NotEqual(english, german);
            }
        }

        [Theory]
        [MemberData(nameof(AllKeys))]
        public void EveryKey_HasTheSamePlaceholdersInBothLanguages(string key)
        {
            var english = Placeholders(Manifest.GetString(key, new CultureInfo("en")));
            var german = Placeholders(Manifest.GetString(key, new CultureInfo("de")));

            Assert.Equal(english, german);
        }

        public static IEnumerable<object[]> EveryErrorCode() =>
            Enum.GetNames(typeof(ERROR))
                .Where(name => name != "AuthenticationError") // the obsolete alias is mapped to AuthFailure
                .Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(EveryErrorCode))]
        public void EveryErrorCode_HasAnEntryInBothLanguages_SoTheConditionListIsNeverBlank(string name)
        {
            // The list of conditions in a task is built from the enum, unfiltered; a missing resource is a blank entry.
            Assert.False(string.IsNullOrWhiteSpace(Manifest.GetString("ENUM.ERROR." + name, new CultureInfo("en"))), name + " (en)");
            Assert.False(string.IsNullOrWhiteSpace(Manifest.GetString("ENUM.ERROR." + name, new CultureInfo("de"))), name + " (de)");
        }

        private static string[] Placeholders(string? text) =>
            Regex.Matches(text ?? string.Empty, @"\{(\d+)(?:[:,][^}]*)?\}")
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
    }
}
