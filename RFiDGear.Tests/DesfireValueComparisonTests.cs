using RFiDGear.Infrastructure.Tasks;
using System;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>The truth tables of the comparisons the DESFire check tasks share.</summary>
    public class DesfireValueComparisonTests
    {
        // All selected: every ticked flag must be set, more are fine, the policy nibble is not part of it.
        [Theory]
        [InlineData(0x0B, 0x0B, true)]
        [InlineData(0x0F, 0x0B, true)]
        [InlineData(0x09, 0x0B, false)]
        [InlineData(0xEB, 0x0B, true)]
        [InlineData(0x0B, 0xFB, true)]
        [InlineData(0x00, 0x00, false)]
        [InlineData(0x0F, 0xE0, false)]
        public void AllSelected_NeedsEveryTickedFlagAndAtLeastOne(int actual, int expected, bool matches)
        {
            Assert.Equal(matches, DesfireValueComparison.MatchesKeySettings((byte)actual, (byte)expected, DesfireAppSettingsCompareMode.AllSelected));
        }

        // Any selected: one ticked flag is enough, a flag that is not ticked and the policy nibble do not count.
        [Theory]
        [InlineData(0x01, 0x0B, true)]
        [InlineData(0x0F, 0x0B, true)]
        [InlineData(0x04, 0x0B, false)]
        [InlineData(0xF4, 0x0B, false)]
        [InlineData(0xF1, 0x0B, true)]
        [InlineData(0x0B, 0x00, false)]
        [InlineData(0x0B, 0xE0, false)]
        public void AnySelected_NeedsOneTickedFlagAndAtLeastOne(int actual, int expected, bool matches)
        {
            Assert.Equal(matches, DesfireValueComparison.MatchesKeySettings((byte)actual, (byte)expected, DesfireAppSettingsCompareMode.AnySelected));
        }

        // Exact: the whole byte, so an extra flag and a different policy both fail, and "none ticked" is a statement.
        [Theory]
        [InlineData(0x0B, 0x0B, true)]
        [InlineData(0x0F, 0x0B, false)]
        [InlineData(0xEB, 0x0B, false)]
        [InlineData(0xEB, 0xEB, true)]
        [InlineData(0xFB, 0xEB, false)]
        [InlineData(0x00, 0x00, true)]
        [InlineData(0x01, 0x00, false)]
        public void Exact_NeedsTheWholeByte(int actual, int expected, bool matches)
        {
            Assert.Equal(matches, DesfireValueComparison.MatchesKeySettings((byte)actual, (byte)expected, DesfireAppSettingsCompareMode.Exact));
        }

        [Theory]
        [InlineData(0x00, false)]
        [InlineData(0xF0, false)]
        [InlineData(0x01, true)]
        [InlineData(0xE8, true)]
        public void HasSelectedFlags_LooksAtTheFourFlagBitsOnly(int expected, bool hasFlags)
        {
            Assert.Equal(hasFlags, DesfireValueComparison.HasSelectedFlags((byte)expected));
        }

        [Fact]
        public void MatchesKeySettings_RejectsAnUnknownMode()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DesfireValueComparison.MatchesKeySettings(0x0B, 0x0B, (DesfireAppSettingsCompareMode)99));
        }

        [Theory]
        [InlineData((int)NumericRelation.Equal, 5, 5, true)]
        [InlineData((int)NumericRelation.Equal, 4, 5, false)]
        [InlineData((int)NumericRelation.Equal, 6, 5, false)]
        [InlineData((int)NumericRelation.ActualAtLeastExpected, 5, 5, true)]
        [InlineData((int)NumericRelation.ActualAtLeastExpected, 6, 5, true)]
        [InlineData((int)NumericRelation.ActualAtLeastExpected, 4, 5, false)]
        [InlineData((int)NumericRelation.ActualAtMostExpected, 5, 5, true)]
        [InlineData((int)NumericRelation.ActualAtMostExpected, 4, 5, true)]
        [InlineData((int)NumericRelation.ActualAtMostExpected, 6, 5, false)]
        public void MatchesNumber_FollowsTheRelation(int relation, long actual, long expected, bool matches)
        {
            Assert.Equal(matches, DesfireValueComparison.MatchesNumber(actual, expected, (NumericRelation)relation));
        }

        [Fact]
        public void MatchesNumber_IsNotFooledByTheEdgesOfTheRange()
        {
            // A file size is an unsigned 32-bit number; compared as a long it cannot wrap in a build without overflow checks.
            Assert.True(DesfireValueComparison.MatchesNumber(uint.MaxValue, uint.MaxValue, NumericRelation.Equal));
            Assert.True(DesfireValueComparison.MatchesNumber(uint.MaxValue, 1, NumericRelation.ActualAtLeastExpected));
            Assert.False(DesfireValueComparison.MatchesNumber(uint.MaxValue, 1, NumericRelation.ActualAtMostExpected));
            Assert.True(DesfireValueComparison.MatchesNumber(long.MinValue, long.MaxValue, NumericRelation.ActualAtMostExpected));
        }

        [Fact]
        public void MatchesNumber_RejectsAnUnknownRelation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DesfireValueComparison.MatchesNumber(1, 1, (NumericRelation)99));
        }
    }
}
