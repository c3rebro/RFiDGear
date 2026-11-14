using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.Infrastructure.Ultralight;
using Xunit;

namespace RFiDGear.Tests.Ultralight
{
    /// <summary>
    /// What may be written, and what may not without the task saying so. Writing the wrong page can lock a chip, its data or its
    /// password for good, so the matrix is pinned row by row for each variant.
    /// </summary>
    public class UltralightWritePolicyTests
    {
        private const UltralightWriteViolation Ok = UltralightWriteViolation.None;
        private const UltralightWriteViolation Empty = UltralightWriteViolation.EmptyOrInvalidRange;
        private const UltralightWriteViolation Outside = UltralightWriteViolation.OutsideTheChip;
        private const UltralightWriteViolation Protected = UltralightWriteViolation.ProtectedPage;
        private const UltralightWriteViolation NeedsChip = UltralightWriteViolation.ProtectedPagesNeedAnExpectedVariant;

        public static IEnumerable<object[]> Rows() => new[]
        {
            // NTAG215: user pages 4-129, protected 0-3 and 130-134, 135 pages.
            new object[] { UltralightExpectation.Ntag215, 4, 1, false, Ok, -1 },
            new object[] { UltralightExpectation.Ntag215, 129, 1, false, Ok, -1 },
            new object[] { UltralightExpectation.Ntag215, 4, 126, false, Ok, -1 },
            new object[] { UltralightExpectation.Ntag215, 130, 1, false, Protected, 130 },
            new object[] { UltralightExpectation.Ntag215, 129, 2, false, Protected, 130 },
            new object[] { UltralightExpectation.Ntag215, 3, 2, false, Protected, 3 },
            new object[] { UltralightExpectation.Ntag215, 0, 1, false, Protected, 0 },
            new object[] { UltralightExpectation.Ntag215, 130, 5, true, Ok, -1 },
            new object[] { UltralightExpectation.Ntag215, 0, 135, true, Ok, -1 },
            new object[] { UltralightExpectation.Ntag215, 134, 2, true, Outside, 135 },
            new object[] { UltralightExpectation.Ntag215, 135, 1, true, Outside, 135 },
            new object[] { UltralightExpectation.Ntag215, 135, 1, false, Outside, 135 },
            new object[] { UltralightExpectation.Ntag215, 0, 0, false, Empty, -1 },
            new object[] { UltralightExpectation.Ntag215, 4, 0, true, Empty, -1 },
            new object[] { UltralightExpectation.Ntag215, -1, 2, false, Empty, -1 },
            new object[] { UltralightExpectation.Ntag215, 4, -3, false, Empty, -1 },
            new object[] { UltralightExpectation.Ntag215, int.MaxValue, 2, false, Empty, -1 },
            new object[] { UltralightExpectation.Ntag215, 1, int.MaxValue, true, Empty, -1 },

            // Ultralight: 16 pages, user pages 4-15.
            new object[] { UltralightExpectation.Plain, 15, 1, false, Ok, -1 },
            new object[] { UltralightExpectation.Plain, 16, 1, false, Outside, 16 },
            new object[] { UltralightExpectation.Plain, 14, 3, false, Outside, 16 },
            new object[] { UltralightExpectation.Plain, 2, 1, false, Protected, 2 },
            new object[] { UltralightExpectation.Plain, 2, 1, true, Ok, -1 },

            // Ultralight C: user pages 4-39, lock bytes, counter, AUTH0, AUTH1 and key are pages 40-47.
            new object[] { UltralightExpectation.C, 39, 1, false, Ok, -1 },
            new object[] { UltralightExpectation.C, 40, 1, false, Protected, 40 },
            new object[] { UltralightExpectation.C, 44, 4, false, Protected, 44 },
            new object[] { UltralightExpectation.C, 44, 4, true, Ok, -1 },
            new object[] { UltralightExpectation.C, 47, 2, true, Outside, 48 },

            // Ultralight EV1: MF0UL11 has the configuration on pages 16-19, MF0UL21 on 36-40 (the lock bytes first).
            new object[] { UltralightExpectation.Ev1Mf0ul11, 15, 1, false, Ok, -1 },
            new object[] { UltralightExpectation.Ev1Mf0ul11, 16, 1, false, Protected, 16 },
            new object[] { UltralightExpectation.Ev1Mf0ul11, 19, 1, true, Ok, -1 },
            new object[] { UltralightExpectation.Ev1Mf0ul11, 20, 1, true, Outside, 20 },
            new object[] { UltralightExpectation.Ev1Mf0ul21, 35, 1, false, Ok, -1 },
            new object[] { UltralightExpectation.Ev1Mf0ul21, 36, 1, false, Protected, 36 },
            new object[] { UltralightExpectation.Ev1Mf0ul21, 40, 1, true, Ok, -1 },
            new object[] { UltralightExpectation.Ev1Mf0ul21, 41, 1, true, Outside, 41 },

            // The other NTAGs: the password and PACK are the last two pages.
            new object[] { UltralightExpectation.Ntag213, 39, 1, false, Ok, -1 },
            new object[] { UltralightExpectation.Ntag213, 43, 2, false, Protected, 43 },
            new object[] { UltralightExpectation.Ntag213, 44, 1, true, Ok, -1 },
            new object[] { UltralightExpectation.Ntag213, 45, 1, true, Outside, 45 },
            new object[] { UltralightExpectation.Ntag216, 225, 1, false, Ok, -1 },
            new object[] { UltralightExpectation.Ntag216, 229, 2, false, Protected, 229 },
            new object[] { UltralightExpectation.Ntag216, 230, 1, true, Ok, -1 },
            new object[] { UltralightExpectation.Ntag216, 231, 1, true, Outside, 231 },

            // Without a named chip only what is true for every chip can be said before the card is asked.
            new object[] { UltralightExpectation.Auto, 4, 12, false, Ok, -1 },
            new object[] { UltralightExpectation.Auto, 100, 1, false, Ok, -1 },    // the layout of the chip found decides later
            new object[] { UltralightExpectation.Auto, 230, 1, false, Ok, -1 },
            new object[] { UltralightExpectation.Auto, 3, 1, false, Protected, 3 },
            new object[] { UltralightExpectation.Auto, 0, 4, false, Protected, 0 },
            new object[] { UltralightExpectation.Auto, 231, 1, false, Outside, 231 },
            new object[] { UltralightExpectation.Auto, 230, 2, false, Outside, 231 },
            new object[] { UltralightExpectation.Auto, 4, 1, true, NeedsChip, -1 },
            new object[] { UltralightExpectation.Auto, 130, 1, true, NeedsChip, -1 },
            new object[] { UltralightExpectation.Auto, 0, 0, false, Empty, -1 },
            new object[] { UltralightExpectation.Auto, 0, 0, true, Empty, -1 }
        };

        [Theory]
        [MemberData(nameof(Rows))]
        public void TheCheckBeforeTheCard_FollowsTheMatrix(UltralightExpectation expected, int startPage, int pageCount, bool allowProtected, UltralightWriteViolation violation, int page)
        {
            var decision = UltralightWritePolicy.Check(expected, startPage, pageCount, allowProtected);

            Assert.Equal(violation, decision.Violation);
            Assert.Equal(violation == Ok, decision.IsAllowed);
            Assert.Equal(page, decision.Page);
        }

        [Fact]
        public void TheCheckWithTheLayoutOfTheChip_GivesTheAnswerOfTheNamedChip_ForEveryRangeOfAGrid()
        {
            var starts = new[] { -1, 0, 3, 4, 15, 16, 19, 20, 35, 36, 39, 40, 44, 45, 47, 48, 129, 130, 134, 135, 225, 226, 230, 231, int.MaxValue };
            var counts = new[] { -1, 0, 1, 2, 5, 126, 231, int.MaxValue };

            foreach (var expectation in Enum.GetValues(typeof(UltralightExpectation)).Cast<UltralightExpectation>().Where(e => e != UltralightExpectation.Auto))
            {
                Assert.True(expectation.TryGetVariant(out var variant));
                var map = UltralightMemoryMap.For(variant);

                foreach (var start in starts)
                {
                    foreach (var count in counts)
                    {
                        foreach (var allow in new[] { false, true })
                        {
                            var before = UltralightWritePolicy.Check(expectation, start, count, allow);
                            var withMap = UltralightWritePolicy.Check(map, start, count, allow);

                            Assert.True(
                                before.Violation == withMap.Violation && before.Page == withMap.Page,
                                $"{expectation} start {start} count {count} allow {allow}: {before.Violation}/{before.Page} before the card, {withMap.Violation}/{withMap.Page} with the layout");
                        }
                    }
                }
            }
        }

        [Fact]
        public void AWriteThatIsAllowed_IsAllowedForEveryUserPageOfEveryChip_AndForNoOtherPageWithoutTheOptIn()
        {
            foreach (var variant in Enum.GetValues(typeof(UltralightVariant)).Cast<UltralightVariant>())
            {
                var map = UltralightMemoryMap.For(variant);

                for (var page = -2; page < map.PageCount + 3; page++)
                {
                    var allowed = UltralightWritePolicy.Check(map, page, 1, allowProtectedPages: false).IsAllowed;

                    Assert.Equal(map.IsUserPage(page), allowed);
                }
            }
        }

        [Fact]
        public void TheOptIn_NeverReachesAPageThatTheChipDoesNotHave()
        {
            foreach (var variant in Enum.GetValues(typeof(UltralightVariant)).Cast<UltralightVariant>())
            {
                var map = UltralightMemoryMap.For(variant);

                Assert.True(UltralightWritePolicy.Check(map, 0, map.PageCount, allowProtectedPages: true).IsAllowed);
                Assert.Equal(UltralightWriteViolation.OutsideTheChip, UltralightWritePolicy.Check(map, 0, map.PageCount + 1, allowProtectedPages: true).Violation);
                Assert.Equal(map.PageCount, UltralightWritePolicy.Check(map, map.PageCount, 1, allowProtectedPages: true).Page);
            }
        }

        [Fact]
        public void AnExpectationThatIsNoValueOfTheEnum_WritesNothing_ButAnInvalidRangeIsStillReportedFirst()
        {
            // A number from a project file that none of the values has: no layout can be named, so nothing is allowed on its account.
            var unknown = (UltralightExpectation)99;

            foreach (var allow in new[] { false, true })
            {
                var decision = UltralightWritePolicy.Check(unknown, 4, 1, allow);

                Assert.False(decision.IsAllowed);
                Assert.Equal(UltralightWriteViolation.UnknownExpectation, decision.Violation);
                Assert.Equal(-1, decision.Page);
                Assert.Equal(UltralightWriteViolation.EmptyOrInvalidRange, UltralightWritePolicy.Check(unknown, 4, 0, allow).Violation);
            }
        }

        [Fact]
        public void TwoDecisions_AreEqualWhenTheyHaveTheSameViolationAtTheSamePage()
        {
            var a = UltralightWritePolicy.Check(UltralightExpectation.Ntag215, 130, 1, false);
            var b = UltralightWritePolicy.Check(UltralightMemoryMap.For(UltralightVariant.Ntag215), 130, 1, false);
            var otherPage = UltralightWritePolicy.Check(UltralightExpectation.Ntag215, 131, 1, false);
            var allowed = UltralightWritePolicy.Check(UltralightExpectation.Ntag215, 4, 1, false);

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a != otherPage);
            Assert.False(a.Equals(allowed));
            Assert.False(a.Equals((object)"not a decision"));
        }

        [Fact]
        public void ACheckWithoutALayoutObject_IsAnArgumentError()
        {
            Assert.Throws<ArgumentNullException>(() => UltralightWritePolicy.Check((UltralightMemoryMap)null, 4, 1, false));
        }

        [Fact]
        public void TheCheckWithoutALayout_RefusesNothingMoreThanEveryChipRefuses()
        {
            // For Auto, a page that some chip has as user memory is not refused as protected: a chip decides that later.
            foreach (var page in new[] { 4, 15, 16, 39, 129, 225 })
            {
                Assert.True(UltralightWritePolicy.Check(UltralightExpectation.Auto, page, 1, false).IsAllowed, "page " + page);
            }

            // Pages 0-3 are protected in every chip.
            foreach (var variant in Enum.GetValues(typeof(UltralightVariant)).Cast<UltralightVariant>())
            {
                Assert.True(UltralightMemoryMap.For(variant).IsProtectedPage(3));
            }
        }
    }
}
