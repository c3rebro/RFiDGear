#nullable enable
using System.Globalization;
using System.Linq;
using System.Resources;

using RFiDGear.Services.SecureKeys;
using RFiDGear.ViewModel;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// What the key manager lists for the key stores: the names and tooltips of the two built-in providers come from the
    /// resources of the language, an extension keeps its own name, and the order is the order a person expects.
    /// </summary>
    public class KeySlotProviderCatalogTests
    {
        private static readonly ResourceManager Manifest =
            new ResourceManager("RFiDGear.Resources.Manifest", typeof(MainWindowViewModel).Assembly);

        private static readonly SecureKeyProviderInfo CredentialManager = new SecureKeyProviderInfo(WindowsCredentialKeyProvider.ProviderId, "Windows Credential Manager");
        private static readonly SecureKeyProviderInfo ExternalApplication = new SecureKeyProviderInfo(BridgeKeyProvider.ProviderId, "Connected external application");

        // A text function that names the key it was asked for, so a test sees which resource was used.
        private static KeySlotProviderCatalog Marked() => new KeySlotProviderCatalog(key => key == "keySlotProviderToolTipExtension" ? "extension {0}" : "<" + key + ">");

        private static KeySlotProviderCatalog InLanguage(string culture) =>
            new KeySlotProviderCatalog(key => Manifest.GetString(key, new CultureInfo(culture)) ?? string.Empty);

        [Fact]
        public void The_built_in_providers_take_name_and_tooltip_from_the_resources_by_their_id()
        {
            var items = Marked().Describe(new[] { ExternalApplication, CredentialManager });

            var credential = items.Single(item => item.Id == WindowsCredentialKeyProvider.ProviderId);
            Assert.Equal("<keySlotProviderNameCredentialManager>", credential.DisplayName);
            Assert.Equal("<keySlotProviderToolTipCredentialManager>", credential.ToolTip);
            Assert.True(credential.IsBuiltIn);

            var external = items.Single(item => item.Id == BridgeKeyProvider.ProviderId);
            Assert.Equal("<keySlotProviderNameBridgeClient>", external.DisplayName);
            Assert.Equal("<keySlotProviderToolTipBridgeClient>", external.ToolTip);
            Assert.True(external.IsBuiltIn);
        }

        [Fact]
        public void The_name_the_registry_reports_for_a_built_in_provider_is_not_shown()
        {
            // The registry's names are English constants; the person sees the name of the language.
            var items = Marked().Describe(new[] { CredentialManager, ExternalApplication });

            Assert.DoesNotContain(items, item => item.DisplayName == CredentialManager.DisplayName || item.DisplayName == ExternalApplication.DisplayName);
        }

        [Fact]
        public void The_credential_manager_comes_first_then_the_external_application_then_the_extensions_by_name()
        {
            var items = Marked().Describe(new[]
            {
                new SecureKeyProviderInfo("zeta", "Zeta vault"),
                ExternalApplication,
                new SecureKeyProviderInfo("alpha", "alpha vault"),
                CredentialManager,
            });

            Assert.Equal(
                new[] { WindowsCredentialKeyProvider.ProviderId, BridgeKeyProvider.ProviderId, "alpha", "zeta" },
                items.Select(item => item.Id).ToArray());
        }

        [Fact]
        public void An_extension_keeps_its_own_name_and_gets_a_generic_tooltip_that_names_its_id()
        {
            var item = Assert.Single(Marked().Describe(new[] { new SecureKeyProviderInfo("acme.vault", "Acme vault") }));

            Assert.Equal("Acme vault", item.DisplayName);
            Assert.Equal("extension acme.vault", item.ToolTip);
            Assert.False(item.IsBuiltIn);
        }

        [Fact]
        public void An_extension_without_a_name_is_listed_by_its_id()
        {
            var item = Assert.Single(Marked().Describe(new[] { new SecureKeyProviderInfo("acme.vault", "  ") }));

            Assert.Equal("acme.vault", item.DisplayName);
        }

        [Fact]
        public void No_providers_give_an_empty_list()
        {
            Assert.Empty(Marked().Describe(System.Array.Empty<SecureKeyProviderInfo>()));
        }

        [Fact]
        public void The_status_text_follows_what_the_bridge_says()
        {
            var catalog = Marked();

            Assert.Equal("<keySlotSupplierStatusOff>", catalog.StatusText(KeySupplierAvailability.Off));
            Assert.Equal("<keySlotSupplierStatusNoSupplier>", catalog.StatusText(KeySupplierAvailability.NoSupplier));
            Assert.Equal("<keySlotSupplierStatusConnected>", catalog.StatusText(KeySupplierAvailability.Connected));
        }

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public void Every_text_exists_in_the_language_and_a_tooltip_is_short_enough_to_read(string culture)
        {
            var catalog = InLanguage(culture);
            var items = catalog.Describe(new[] { CredentialManager, ExternalApplication, new SecureKeyProviderInfo("acme.vault", "Acme vault") });

            foreach (var item in items)
            {
                Assert.False(string.IsNullOrWhiteSpace(item.DisplayName), item.Id + " name (" + culture + ")");
                Assert.False(string.IsNullOrWhiteSpace(item.ToolTip), item.Id + " tooltip (" + culture + ")");

                // A tooltip is a sentence or two. The window wraps it, but a wall of text is not read.
                Assert.True(item.ToolTip.Length <= 200, item.Id + " tooltip is " + item.ToolTip.Length + " characters long (" + culture + ")");
            }

            var statuses = new[] { KeySupplierAvailability.Off, KeySupplierAvailability.NoSupplier, KeySupplierAvailability.Connected }
                .Select(catalog.StatusText)
                .ToArray();

            Assert.All(statuses, text => Assert.False(string.IsNullOrWhiteSpace(text)));
            Assert.Equal(3, statuses.Distinct().Count());
        }

        [Theory]
        [InlineData("en", "log")]
        [InlineData("de", "Protokoll")]
        public void The_off_text_names_both_causes_and_where_to_look(string culture, string logWord)
        {
            // Off is a bridge that is switched off and one that is on but could not start (the pipe is taken): the text must
            // not claim the first, and says where the reason of the second is written.
            var text = InLanguage(culture).StatusText(KeySupplierAvailability.Off);

            Assert.Contains("EXTERNALCONTROL=1", text);
            Assert.Contains(logWord, text);
        }

        [Fact]
        public void The_two_languages_give_different_texts()
        {
            var english = InLanguage("en").Describe(new[] { CredentialManager, ExternalApplication });
            var german = InLanguage("de").Describe(new[] { CredentialManager, ExternalApplication });

            for (var index = 0; index < english.Count; index++)
            {
                Assert.NotEqual(english[index].DisplayName, german[index].DisplayName);
                Assert.NotEqual(english[index].ToolTip, german[index].ToolTip);
            }
        }
    }
}
