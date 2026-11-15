using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.Contracts;
using RFiDGear.Services.SecureKeys;
using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// The keys of the Ultralight family as key slots: the ids that projects will store, how long each key is, and the name of
    /// the kind of key a provider is told about. The key of an Ultralight C is a 16-byte two-key triple DES key, the password
    /// of an EV1 and an NTAG 4 bytes and its PACK 2 bytes.
    /// </summary>
    public class UltralightKeySlotTests
    {
        public static IEnumerable<object[]> HexLengths() => new[]
        {
            new object[] { SecureKeyAlgorithm.Des, 32 },
            new object[] { SecureKeyAlgorithm.TripleDes2K, 32 },
            new object[] { SecureKeyAlgorithm.TripleDes3K, 48 },
            new object[] { SecureKeyAlgorithm.Aes128, 32 },
            new object[] { SecureKeyAlgorithm.Classic6Byte, 12 },
            new object[] { SecureKeyAlgorithm.UltralightPassword, 8 },
            new object[] { SecureKeyAlgorithm.UltralightPack, 4 },
        };

        [Theory]
        [MemberData(nameof(HexLengths))]
        public void AKey_HasAsManyHexDigitsAsItsAlgorithmSays(SecureKeyAlgorithm algorithm, int hexLength)
        {
            var need = new KeyNeed("slot", algorithm, SecureKeyUsage.Authenticate, null, null);

            Assert.Equal(hexLength, need.HexLength);
        }

        [Fact]
        public void EveryAlgorithm_HasALengthHere_SoANewOneIsNeverTakenForA16ByteKey()
        {
            // HexLength answers 32 for what it does not list. A new algorithm has to be added above on purpose.
            var listed = HexLengths().Select(row => (SecureKeyAlgorithm)row[0]).ToList();

            Assert.Equal(
                Enum.GetValues(typeof(SecureKeyAlgorithm)).Cast<SecureKeyAlgorithm>().OrderBy(a => (int)a),
                listed.OrderBy(a => (int)a));
        }

        [Fact]
        public void TheAlgorithmsOfTheUltralightKeys_KeepTheirNumbers()
        {
            // The numbers are part of the contract of the extensions: the values that exist keep theirs, the new ones come after.
            Assert.Equal(4, (int)SecureKeyAlgorithm.Classic6Byte);
            Assert.Equal(5, (int)SecureKeyAlgorithm.UltralightPassword);
            Assert.Equal(6, (int)SecureKeyAlgorithm.UltralightPack);
        }

        [Fact]
        public void TheSlotIds_AreThoseThatProjectsStore_AndAreUnique()
        {
            // Stored in project files: renaming one unbinds every project that uses it.
            Assert.Equal("ultralight.c.key", SecureKeySlots.UltralightCKey);
            Assert.Equal("ultralight.password", SecureKeySlots.UltralightPassword);
            Assert.Equal("ultralight.pack", SecureKeySlots.UltralightPack);
            Assert.Equal(new[] { "ultralight.c.key", "ultralight.password", "ultralight.pack" }, SecureKeySlots.Ultralight);

            var all = SecureKeySlots.Desfire.Concat(SecureKeySlots.Classic).Concat(SecureKeySlots.Ultralight).ToList();

            Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void EverySlotOfAnUltralightTask_HasANameInBothLanguages()
        {
            foreach (var culture in new[] { "en", "de" })
            {
                var text = TestText.In(culture);

                foreach (var slot in SecureKeySlots.Ultralight)
                {
                    var key = KeySlotNames.ResourceKeyOf(slot);

                    Assert.NotNull(key);
                    Assert.False(string.IsNullOrWhiteSpace(text(key!)), slot + " (" + culture + ")");
                }
            }
        }

        [Theory]
        [InlineData(6, SecureKeyAlgorithm.Classic6Byte)]
        [InlineData(24, SecureKeyAlgorithm.TripleDes3K)]
        [InlineData(16, SecureKeyAlgorithm.Aes128)]
        [InlineData(4, SecureKeyAlgorithm.UltralightPassword)]
        [InlineData(2, SecureKeyAlgorithm.UltralightPack)]
        public void AKeyThatTheMigrationChecks_IsAskedForByTheAlgorithmOfItsLength(int keyBytes, SecureKeyAlgorithm expected)
        {
            var request = KeyMigration.RequestFor("cards/main", SecureKeySlots.UltralightPassword, keyBytes);

            Assert.Equal(expected, request.Algorithm);
        }
    }
}
