using RFiDGear.Infrastructure;

using System;
using System.Linq;
using System.Reflection;

using Xunit;

namespace RFiDGear.Tests.KeySlots
{
    /// <summary>
    /// The validation of a DESFire key answers for that key only. It used to leave its result in a static property that
    /// the reader provider read later, so a key that failed validation could be replaced by the one validated before.
    /// </summary>
    public class DesfireKeyFormattingTests
    {
        private const string ValidAesKey = "00112233445566778899aabbccddeeff";
        private const string OtherValidAesKey = "ffeeddccbbaa99887766554433221100";

        [Fact]
        public void A_valid_key_comes_back_as_upper_case_pairs_separated_by_spaces()
        {
            var error = CustomConverter.TryFormatMifareDesfireKey(ValidAesKey, DESFireKeyType.DF_KEY_AES, out var formatted);

            Assert.Equal(KEY_ERROR.NO_ERROR, error);
            Assert.Equal("00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF", formatted);
        }

        [Fact]
        public void A_key_that_already_has_spaces_is_formatted_the_same()
        {
            var error = CustomConverter.TryFormatMifareDesfireKey("00 11 22 33 44 55 66 77 88 99 aa bb cc dd ee ff", DESFireKeyType.DF_KEY_AES, out var formatted);

            Assert.Equal(KEY_ERROR.NO_ERROR, error);
            Assert.Equal("00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF", formatted);
        }

        [Fact]
        public void A_3K3DES_key_needs_48_digits()
        {
            var tooShort = CustomConverter.TryFormatMifareDesfireKey(ValidAesKey, DESFireKeyType.DF_KEY_3K3DES, out _);
            var right = CustomConverter.TryFormatMifareDesfireKey(ValidAesKey + "0011223344556677", DESFireKeyType.DF_KEY_3K3DES, out var formatted);

            Assert.Equal(KEY_ERROR.KEY_HAS_WRONG_LENGTH, tooShort);
            Assert.Equal(KEY_ERROR.NO_ERROR, right);
            Assert.Equal(24, formatted.Split(' ').Length);
        }

        [Theory]
        [InlineData(null, KEY_ERROR.KEY_IS_EMPTY)]
        [InlineData("", KEY_ERROR.KEY_IS_EMPTY)]
        [InlineData("00112233445566778899aabbccddeeXX", KEY_ERROR.KEY_HAS_WRONG_FORMAT)]
        [InlineData("00112233445566778899aabbccddee", KEY_ERROR.KEY_HAS_WRONG_LENGTH)]
        public void An_invalid_key_is_refused_and_nothing_comes_back(string? keyHex, KEY_ERROR expected)
        {
            var error = CustomConverter.TryFormatMifareDesfireKey(keyHex!, DESFireKeyType.DF_KEY_AES, out var formatted);

            Assert.Equal(expected, error);
            Assert.Equal(string.Empty, formatted);
        }

        [Fact]
        public void The_validating_method_without_a_result_answers_as_before()
        {
            Assert.Equal(KEY_ERROR.NO_ERROR, CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(ValidAesKey));
            Assert.Equal(KEY_ERROR.KEY_IS_EMPTY, CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(string.Empty));
            Assert.Equal(KEY_ERROR.KEY_HAS_WRONG_LENGTH, CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(ValidAesKey, DESFireKeyType.DF_KEY_3K3DES));
        }

        [Fact]
        public void A_key_that_fails_never_gets_the_previous_key_back()
        {
            Assert.Equal("00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF", CustomConverter.FormatMifareDesfireKeyOrThrow(ValidAesKey, DESFireKeyType.DF_KEY_AES));

            Assert.Throws<ArgumentException>(() => CustomConverter.FormatMifareDesfireKeyOrThrow(string.Empty, DESFireKeyType.DF_KEY_AES));
            Assert.Throws<ArgumentException>(() => CustomConverter.FormatMifareDesfireKeyOrThrow("not a key", DESFireKeyType.DF_KEY_AES));
            Assert.Throws<ArgumentException>(() => CustomConverter.FormatMifareDesfireKeyOrThrow(OtherValidAesKey, DESFireKeyType.DF_KEY_3K3DES));
        }

        [Theory]
        [InlineData("not a key at all")]
        [InlineData("00112233445566778899aabbccddee")]
        public void The_refusal_names_the_problem_and_never_the_key(string keyHex)
        {
            var thrown = Assert.Throws<ArgumentException>(() => CustomConverter.FormatMifareDesfireKeyOrThrow(keyHex, DESFireKeyType.DF_KEY_AES));

            Assert.DoesNotContain(keyHex, thrown.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("00112233", thrown.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("KEY_HAS_WRONG", thrown.Message, StringComparison.Ordinal);
            Assert.Equal("keyHex", thrown.ParamName);
        }

        [Fact]
        public void The_converter_keeps_no_state_between_calls()
        {
            // The side channel was a static property. A guard on the shape stops it from coming back under another name.
            const BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var type = typeof(CustomConverter);

            var mutableFields = type.GetFields(all).Where(field => !field.IsInitOnly && !field.IsLiteral).Select(field => field.Name);
            var settableProperties = type.GetProperties(all).Where(property => property.SetMethod != null).Select(property => property.Name);

            Assert.Empty(mutableFields);
            Assert.Empty(settableProperties);
        }
    }
}
