using RFiDGear.Services.SecureKeys;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>The provider id ends up in a project file, so what counts as one is checked exactly.</summary>
    public class SecureKeySyntaxTests
    {
        [Theory]
        [InlineData("a")]
        [InlineData("acme.vault")]
        [InlineData("windows-credential-manager")]
        [InlineData("my_vault.2")]
        [InlineData("0")]
        public void A_provider_id_of_lower_case_letters_digits_and_dot_dash_underscore_is_valid(string id)
        {
            Assert.True(SecureKeySyntax.IsValidProviderId(id));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Upper")]
        [InlineData("has space")]
        [InlineData("slash/vault")]
        [InlineData("colon:vault")]
        [InlineData("ümlaut")]
        [InlineData("ends.with.newline\n")]
        [InlineData("tab\tinside")]
        public void Anything_else_is_not(string? id)
        {
            Assert.False(SecureKeySyntax.IsValidProviderId(id));
        }

        [Fact]
        public void A_provider_id_has_at_most_64_characters()
        {
            Assert.True(SecureKeySyntax.IsValidProviderId(new string('a', 64)));
            Assert.False(SecureKeySyntax.IsValidProviderId(new string('a', 65)));
        }
    }
}
