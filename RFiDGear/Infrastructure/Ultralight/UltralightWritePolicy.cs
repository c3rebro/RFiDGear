using System;

namespace RFiDGear.Infrastructure.Ultralight
{
    /// <summary>Why a write to a range of pages is not allowed.</summary>
    public enum UltralightWriteViolation
    {
        /// <summary>Nothing: the write is allowed.</summary>
        None = 0,

        /// <summary>The range is empty, starts before page 0 or does not fit a 32-bit page number.</summary>
        EmptyOrInvalidRange = 1,

        /// <summary>The range reaches a page that the chip does not have (or that no chip has).</summary>
        OutsideTheChip = 2,

        /// <summary>The range reaches a protected page and the task has not opted in to writing such pages.</summary>
        ProtectedPage = 3,

        /// <summary>The task opted in to writing protected pages without naming the chip: the pages differ per chip, so it must name it.</summary>
        ProtectedPagesNeedAnExpectedVariant = 4,

        /// <summary>The expectation is a number that is none of the <see cref="UltralightExpectation"/> values (from a project file, say): nothing is written on its account.</summary>
        UnknownExpectation = 5
    }

    /// <summary>The answer of <see cref="UltralightWritePolicy"/>.</summary>
    public readonly struct UltralightWriteDecision : IEquatable<UltralightWriteDecision>
    {
        internal UltralightWriteDecision(UltralightWriteViolation violation, int page)
        {
            Violation = violation;
            Page = page;
        }

        /// <summary>Gets a value that says whether the write is allowed.</summary>
        public bool IsAllowed => Violation == UltralightWriteViolation.None;

        /// <summary>Gets why the write is not allowed, or <see cref="UltralightWriteViolation.None"/>.</summary>
        public UltralightWriteViolation Violation { get; }

        /// <summary>Gets the first page of the range that is the reason, or -1 when there is none (an allowed write, an invalid range).</summary>
        public int Page { get; }

        /// <summary>Whether two decisions are the same: the same violation at the same page.</summary>
        /// <param name="left">The first decision.</param>
        /// <param name="right">The second decision.</param>
        /// <returns><c>true</c> when they are equal.</returns>
        public static bool operator ==(UltralightWriteDecision left, UltralightWriteDecision right) => left.Equals(right);

        /// <summary>Whether two decisions differ.</summary>
        /// <param name="left">The first decision.</param>
        /// <param name="right">The second decision.</param>
        /// <returns><c>true</c> when they are not equal.</returns>
        public static bool operator !=(UltralightWriteDecision left, UltralightWriteDecision right) => !left.Equals(right);

        /// <inheritdoc />
        public bool Equals(UltralightWriteDecision other) => Violation == other.Violation && Page == other.Page;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is UltralightWriteDecision other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine((int)Violation, Page);
    }

    /// <summary>
    /// The one place that decides whether a range of pages may be written. Pages 0 to 3 (UID, lock bytes, OTP) and the lock,
    /// configuration, password and key pages after the user memory are protected: they can only be written when the task says so
    /// and names the chip, because a wrong write there can lock the chip, its data or its password for good. The check is made
    /// twice: before the card is asked, with what the expectation of the task knows, and again with the layout of the chip that
    /// was found, which is the one that counts.
    /// </summary>
    public static class UltralightWritePolicy
    {
        /// <summary>
        /// The check before the card is asked.
        /// </summary>
        /// <param name="expected">The chip the task expects.</param>
        /// <param name="startPage">The first page.</param>
        /// <param name="pageCount">The number of pages.</param>
        /// <param name="allowProtectedPages">Whether the task opted in to writing protected pages.</param>
        /// <returns>
        /// For a named chip the same answer as the check with its layout. For <see cref="UltralightExpectation.Auto"/> only what is
        /// true for every chip: pages 0 to 3 are protected, no page is at or above <see cref="UltralightMemoryMap.MaxPageCount"/>,
        /// and protected pages cannot be allowed at all.
        /// </returns>
        public static UltralightWriteDecision Check(UltralightExpectation expected, int startPage, int pageCount, bool allowProtectedPages)
        {
            if (!IsValidRange(startPage, pageCount))
            {
                return new UltralightWriteDecision(UltralightWriteViolation.EmptyOrInvalidRange, -1);
            }

            if (expected.TryGetVariant(out var variant))
            {
                return Check(UltralightMemoryMap.For(variant), startPage, pageCount, allowProtectedPages);
            }

            if (expected != UltralightExpectation.Auto)
            {
                return new UltralightWriteDecision(UltralightWriteViolation.UnknownExpectation, -1);
            }

            // Any chip. The pages differ per chip, so protected pages cannot be allowed without naming it.
            if (allowProtectedPages)
            {
                return new UltralightWriteDecision(UltralightWriteViolation.ProtectedPagesNeedAnExpectedVariant, -1);
            }

            // True for every chip: pages 0 to 3 are protected, and no chip has the page MaxPageCount or a later one. The first of
            // the two in ascending order is the lower pages.
            if (startPage < UltralightMemoryMap.FirstUserPage)
            {
                return new UltralightWriteDecision(UltralightWriteViolation.ProtectedPage, startPage);
            }

            if (startPage + pageCount - 1 >= UltralightMemoryMap.MaxPageCount)
            {
                return new UltralightWriteDecision(UltralightWriteViolation.OutsideTheChip, Math.Max(startPage, UltralightMemoryMap.MaxPageCount));
            }

            return new UltralightWriteDecision(UltralightWriteViolation.None, -1);
        }

        /// <summary>
        /// The check with the layout of the chip that was found.
        /// </summary>
        /// <param name="map">The layout.</param>
        /// <param name="startPage">The first page.</param>
        /// <param name="pageCount">The number of pages.</param>
        /// <param name="allowProtectedPages">Whether the task opted in to writing protected pages.</param>
        /// <returns>The first page of the range that is not allowed, in ascending order, and why; or an allowed write.</returns>
        public static UltralightWriteDecision Check(UltralightMemoryMap map, int startPage, int pageCount, bool allowProtectedPages)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!IsValidRange(startPage, pageCount))
            {
                return new UltralightWriteDecision(UltralightWriteViolation.EmptyOrInvalidRange, -1);
            }

            if (startPage >= map.PageCount)
            {
                return new UltralightWriteDecision(UltralightWriteViolation.OutsideTheChip, startPage);
            }

            // Ascending, so the first page that is a reason is the one reported. Never further than the first page after the chip,
            // which is a reason in itself: a long range does not make this slow.
            var last = Math.Min(startPage + pageCount - 1, map.PageCount);

            for (var page = startPage; page <= last; page++)
            {
                if (!map.Contains(page))
                {
                    return new UltralightWriteDecision(UltralightWriteViolation.OutsideTheChip, page);
                }

                if (!allowProtectedPages && map.IsProtectedPage(page))
                {
                    return new UltralightWriteDecision(UltralightWriteViolation.ProtectedPage, page);
                }
            }

            return new UltralightWriteDecision(UltralightWriteViolation.None, -1);
        }

        // At least one page, from a page number that exists, and an end that fits a 32-bit number.
        private static bool IsValidRange(int startPage, int pageCount)
        {
            return startPage >= 0 && pageCount >= 1 && startPage <= int.MaxValue - pageCount;
        }
    }
}
