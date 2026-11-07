using System;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>How an actual number is compared with the expected one.</summary>
    internal enum NumericRelation
    {
        /// <summary>The actual value equals the expected value.</summary>
        Equal,

        /// <summary>The actual value is greater than or equal to the expected value.</summary>
        ActualAtLeastExpected,

        /// <summary>The actual value is less than or equal to the expected value.</summary>
        ActualAtMostExpected,
    }

    /// <summary>
    /// The comparisons of the DESFire check tasks, kept in one place so that every task states what it compares with the
    /// same words. A field that is a value, such as a file type, a communication mode or an access right (a key number,
    /// "free" or "never"), is compared for equality only: a bit operation on it is meaningless.
    /// </summary>
    internal static class DesfireValueComparison
    {
        /// <summary>The four independent flags of the key-settings byte of an application.</summary>
        internal const byte KeySettingFlagsMask = 0x0F;

        /// <summary>
        /// Determines whether any of the four key-settings flags is ticked. <see cref="DesfireAppSettingsCompareMode.AllSelected"/>
        /// and <see cref="DesfireAppSettingsCompareMode.AnySelected"/> compare nothing without one.
        /// </summary>
        /// <param name="expected">The expected key-settings byte.</param>
        /// <returns><see langword="true"/> when at least one flag of <paramref name="expected"/> is set.</returns>
        internal static bool HasSelectedFlags(byte expected) => (expected & KeySettingFlagsMask) != 0;

        /// <summary>Compares the key-settings byte of an application with the expected one.</summary>
        /// <param name="actual">The byte read from the card.</param>
        /// <param name="expected">The expected byte: the ticked flags and, for <see cref="DesfireAppSettingsCompareMode.Exact"/>, the policy.</param>
        /// <param name="mode">How to compare. All and any of the selected flags look at the four flag bits only; with no flag ticked they do not match.</param>
        /// <returns><see langword="true"/> when the card matches under <paramref name="mode"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined mode.</exception>
        internal static bool MatchesKeySettings(byte actual, byte expected, DesfireAppSettingsCompareMode mode)
        {
            var actualFlags = actual & KeySettingFlagsMask;
            var expectedFlags = expected & KeySettingFlagsMask;

            switch (mode)
            {
                case DesfireAppSettingsCompareMode.AllSelected:
                    return expectedFlags != 0 && (actualFlags & expectedFlags) == expectedFlags;
                case DesfireAppSettingsCompareMode.AnySelected:
                    return expectedFlags != 0 && (actualFlags & expectedFlags) != 0;
                case DesfireAppSettingsCompareMode.Exact:
                    return actual == expected;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown key-settings compare mode.");
            }
        }

        /// <summary>Compares a number read from the card with the expected one.</summary>
        /// <param name="actual">The value read from the card.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="relation">How the two are related for the check to pass.</param>
        /// <returns><see langword="true"/> when the values are related as <paramref name="relation"/> says.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="relation"/> is not a defined relation.</exception>
        internal static bool MatchesNumber(long actual, long expected, NumericRelation relation)
        {
            switch (relation)
            {
                case NumericRelation.Equal:
                    return actual == expected;
                case NumericRelation.ActualAtLeastExpected:
                    return actual >= expected;
                case NumericRelation.ActualAtMostExpected:
                    return actual <= expected;
                default:
                    throw new ArgumentOutOfRangeException(nameof(relation), relation, "Unknown numeric relation.");
            }
        }
    }
}
