using System;
using System.Threading.Tasks;

using RFiDGear.HIL.Tests;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Ultralight;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The steps of the Ultralight / NTAG hardware cases (<c>RFiDGear.HIL.Tests/UltralightScenarios.cs</c>, compiled into this
    /// project too) run against fakes of the reader that hold a chip in memory: an Elatec reader and a PC/SC reader. A step that is
    /// wrong would otherwise be found only at a card, and a step that writes to a card has to be right. Every case has to pass for
    /// the chip and the reader it describes, a chip that a reader refuses has to be refused, a case for a card of another family
    /// has to skip, and a write that does not happen has to be found.
    /// </summary>
    public sealed class HilUltralightScenarioSelfCheckTests
    {
        // A case that skips ends the test as skipped, whether it is caught by Assert.Throws or not (xunit.v3), so the exception is
        // caught by hand: the skip is what these cases are shown to do.
        private static async Task SkipOf(Func<Task> scenario)
        {
            try
            {
                await scenario();
            }
            catch (SkipException)
            {
                return;
            }

            Assert.Fail("The case should have skipped, and it did not.");
        }

        // The card that is on the reader, as the reader says who it is: always the same one.
        private static Task<string?> Same() => Task.FromResult<string?>("04AABBCCDDEEFF");

        // The failure of a case that is shown to find a fault; a skip is not one, and a case that finds nothing is a failure here.
        private static async Task<Exception> FailureOf(Func<Task> scenario)
        {
            try
            {
                await scenario();
            }
            catch (Exception failure) when (failure is not SkipException)
            {
                return failure;
            }

            Assert.Fail("The case should have found a fault, and it did not.");

            return null!;
        }

        // ── What a reader gives of a chip ──

        [Theory]
        [InlineData(false, CARD_TYPE.MifareUltralight, "Full")]
        [InlineData(false, CARD_TYPE.MifareUltralightC, "Full")]
        [InlineData(false, CARD_TYPE.MifareUltralight_EV1_MF0UL11, "Full")]
        [InlineData(false, CARD_TYPE.MifareUltralight_EV1_MF0UL21, "Full")]
        [InlineData(false, CARD_TYPE.NTAG_213, "Full")]
        [InlineData(false, CARD_TYPE.NTAG_215, "Full")]
        [InlineData(false, CARD_TYPE.NTAG_216, "Full")]
        [InlineData(true, CARD_TYPE.MifareUltralight, "Full")]
        [InlineData(true, CARD_TYPE.MifareUltralightC, "Full")]
        [InlineData(true, CARD_TYPE.MifareUltralight_EV1_MF0UL11, "Refused")]
        [InlineData(true, CARD_TYPE.NTAG_213, "Refused")]
        [InlineData(true, CARD_TYPE.NTAG_215, "Refused")]
        [InlineData(true, CARD_TYPE.NTAG_216, "Refused")]
        [InlineData(false, CARD_TYPE.NTAG_210, "Refused")]
        [InlineData(false, CARD_TYPE.NTAG_212, "Refused")]
        [InlineData(true, CARD_TYPE.NTAG_212, "Refused")]
        [InlineData(false, CARD_TYPE.DESFireEV1, "NotOfTheFamily")]
        [InlineData(true, CARD_TYPE.Mifare1K, "NotOfTheFamily")]
        [InlineData(false, CARD_TYPE.NOTAG, "NotOfTheFamily")]
        [InlineData(true, CARD_TYPE.Unspecified, "Refused")]
        [InlineData(false, CARD_TYPE.Unspecified, "NotOfTheFamily")]
        [InlineData(false, CARD_TYPE.NTAG_424, "NotOfTheFamily")]
        public void WhatAReaderGives_IsWhatTheLayersDo(bool pcsc, CARD_TYPE type, string expected)
        {
            Assert.Equal(Enum.Parse<UltralightScenarios.Support>(expected), UltralightScenarios.SupportOf(pcsc, type));
        }

        // ── An Elatec reader with a chip ──

        [Theory]
        [InlineData(UltralightVariant.Plain, CARD_TYPE.MifareUltralight)]
        [InlineData(UltralightVariant.C, CARD_TYPE.MifareUltralightC)]
        [InlineData(UltralightVariant.Ev1Mf0ul11, CARD_TYPE.MifareUltralight_EV1_MF0UL11)]
        [InlineData(UltralightVariant.Ntag213, CARD_TYPE.NTAG_213)]
        [InlineData(UltralightVariant.Ntag215, CARD_TYPE.NTAG_215)]
        [InlineData(UltralightVariant.Ntag216, CARD_TYPE.NTAG_216)]
        public async Task EveryCase_PassesOnAnElatecReader_AndTheChipHoldsWhatItHeld(UltralightVariant variant, CARD_TYPE type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightTestReader(variant);

                using (ReaderDeviceScope.Use(reader))
                {
                    await UltralightScenarios.AReadOfTheUserPagesAsync(type);
                    await UltralightScenarios.AnExpectationThatIsNotTheChipIsRefusedAsync(type);
                    await UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(type, Same);
                    await UltralightScenarios.AWriteToAProtectedPageIsRefusedBeforeTheCardAsync(type);
                }

                // The only page that was written is page 10, and it holds what it held; nothing was written to page 3.
                Assert.Equal(UltralightTestReader.PageOf(HilUltralightTasks.UserPage), reader.PagesOf(HilUltralightTasks.UserPage, 1));
                Assert.Equal(UltralightTestReader.PageOf(HilUltralightTasks.ProtectedPage), reader.PagesOf(HilUltralightTasks.ProtectedPage, 1));
                Assert.Equal(2, reader.CountOf("write " + HilUltralightTasks.UserPage));
                Assert.Equal(0, reader.CountOf("write " + HilUltralightTasks.ProtectedPage));
                Assert.Equal(2, reader.CountStartingWith("write "));
            });
        }

        [Fact]
        public async Task TheWriteCase_FindsAChipThatDoesNotTakeTheWrite_AndLeavesThePageAsItWas()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightTestReader(UltralightVariant.Ntag215);
                reader.IgnoredWrites.Add(HilUltralightTasks.UserPage);

                using (ReaderDeviceScope.Use(reader))
                {
                    // Any failure but a skip: a case that skipped here would have found nothing.
                    await FailureOf(() => UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(CARD_TYPE.NTAG_215, Same));
                }

                Assert.Equal(UltralightTestReader.PageOf(HilUltralightTasks.UserPage), reader.PagesOf(HilUltralightTasks.UserPage, 1));
            });
        }

        [Fact]
        public async Task TheWriteCase_SaysSoWhenThePageCannotBePutBack()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightTestReader(UltralightVariant.Ntag215);

                // The chip takes the first write and refuses every later one: the page keeps the pattern.
                reader.RefusedWritesAfterTheFirst = true;
                var before = UltralightTestReader.PageOf(HilUltralightTasks.UserPage);

                using (ReaderDeviceScope.Use(reader))
                {
                    var failure = await FailureOf(() => UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(CARD_TYPE.NTAG_215, Same));

                    Assert.IsType<InvalidOperationException>(failure);
                    Assert.Contains("could NOT be put back", failure.Message, StringComparison.Ordinal);

                    // What the person needs to put the page back by hand: the bytes it held, and nothing to work out from the page.
                    Assert.Contains(Convert.ToHexString(before), failure.Message, StringComparison.Ordinal);
                    Assert.NotEqual(before, reader.PagesOf(HilUltralightTasks.UserPage, 1));
                }
            });
        }

        // ── A card that is replaced ──

        // A reader that says who is on it: the first answers are the card the case started with, the later ones another card.
        private static Func<Task<string?>> ReplacedAfter(int answers)
        {
            var asked = 0;

            return () => Task.FromResult<string?>(++asked <= answers ? "04AABBCCDDEEFF" : "04112233445566");
        }

        [Fact]
        public async Task TheWriteCase_WritesNothing_WhenTheCardIsReplacedBeforeTheWrite()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightTestReader(UltralightVariant.Ntag215);

                using (ReaderDeviceScope.Use(reader))
                {
                    // The first answer is the start, the second is the look before the write: another card.
                    var failure = await FailureOf(() => UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(CARD_TYPE.NTAG_215, ReplacedAfter(1)));

                    Assert.Contains("Nothing was written", failure.Message, StringComparison.Ordinal);
                }

                Assert.Equal(0, reader.CountStartingWith("write "));
                Assert.Equal(UltralightTestReader.PageOf(HilUltralightTasks.UserPage), reader.PagesOf(HilUltralightTasks.UserPage, 1));
            });
        }

        [Fact]
        public async Task TheWriteCase_DoesNotPutTheBytesOnAnotherCard_AndSaysWhatTheFirstCardHolds()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightTestReader(UltralightVariant.Ntag215);

                using (ReaderDeviceScope.Use(reader))
                {
                    // The card is the same at the start and before the write, and another one before the put-back.
                    var failure = await FailureOf(() => UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(CARD_TYPE.NTAG_215, ReplacedAfter(2)));

                    Assert.IsType<InvalidOperationException>(failure);
                    Assert.Contains("NOT put back", failure.Message, StringComparison.Ordinal);
                    Assert.Contains(Convert.ToHexString(UltralightTestReader.PageOf(HilUltralightTasks.UserPage)), failure.Message, StringComparison.Ordinal);
                    Assert.DoesNotContain("04AABBCCDDEEFF", failure.Message, StringComparison.Ordinal);
                    Assert.DoesNotContain("04112233445566", failure.Message, StringComparison.Ordinal);
                }

                // One write, the pattern: nothing was written to put the page back, on this card or on the one that replaced it.
                Assert.Equal(1, reader.CountStartingWith("write "));
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task TheWriteCase_WritesNothing_WhenTheReaderDoesNotSayWhoIsOnIt(string? identity)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightTestReader(UltralightVariant.Ntag215);

                using (ReaderDeviceScope.Use(reader))
                {
                    var failure = await FailureOf(() => UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(CARD_TYPE.NTAG_215, () => Task.FromResult(identity)));

                    Assert.Contains("no identity", failure.Message, StringComparison.Ordinal);
                }

                Assert.Empty(reader.Calls);
            });
        }

        // ── A PC/SC reader with a chip ──

        [Theory]
        [InlineData(UltralightVariant.Plain, CARD_TYPE.MifareUltralight)]
        [InlineData(UltralightVariant.C, CARD_TYPE.MifareUltralightC)]
        public async Task EveryCase_PassesOnAPcscReaderThatKnowsTheChip(UltralightVariant variant, CARD_TYPE type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightPcscTestReader(variant);
                var before = reader.PagesOf(HilUltralightTasks.UserPage, 1);

                using (ReaderDeviceScope.Use(reader))
                {
                    Assert.True(reader.IsPcscReader);

                    await UltralightScenarios.AReadOfTheUserPagesAsync(type);
                    await UltralightScenarios.AnExpectationThatIsNotTheChipIsRefusedAsync(type);
                    await UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(type, Same);
                    await UltralightScenarios.AWriteToAProtectedPageIsRefusedBeforeTheCardAsync(type);
                }

                // The page holds what it held, and the protected page was never written to.
                Assert.Equal(before, reader.PagesOf(HilUltralightTasks.UserPage, 1));
                Assert.DoesNotContain("write " + HilUltralightTasks.ProtectedPage, reader.Calls);
            });
        }

        [Theory]
        [InlineData(UltralightVariant.Ntag215, CARD_TYPE.NTAG_215)]
        [InlineData(UltralightVariant.Ev1Mf0ul11, CARD_TYPE.MifareUltralight_EV1_MF0UL11)]
        public async Task APcscReaderThatDoesNotKnowTheChip_IsAskedForNothing_AndEveryCaseSaysSo(UltralightVariant variant, CARD_TYPE type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightPcscTestReader(variant);

                using (ReaderDeviceScope.Use(reader))
                {
                    await UltralightScenarios.AReadOfTheUserPagesAsync(type);
                    await UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(type, Same);
                    await UltralightScenarios.AWriteToAProtectedPageIsRefusedBeforeTheCardAsync(type);

                    // There is no chip to name wrongly: the reader gives no pages of this one.
                    await SkipOf(() => UltralightScenarios.AnExpectationThatIsNotTheChipIsRefusedAsync(type));
                }

                Assert.DoesNotContain(reader.Calls, call => call.StartsWith("write", StringComparison.Ordinal));
                Assert.DoesNotContain(reader.Calls, call => call.StartsWith("read", StringComparison.Ordinal));
            });
        }

        // ── Another card ──

        [Theory]
        [InlineData(UltralightVariant.Ntag215)]
        [InlineData(UltralightVariant.Ev1Mf0ul21)]
        public async Task ACardThatAPcscReaderDoesNotKnow_IsRefusedByEveryCase_WithoutSayingWhichChipItIs(UltralightVariant variant)
        {
            // Public discovery on a PC/SC reader gives such a card the type "unspecified", and a case that skipped on it would never
            // check the refusal that the card on this reader is about.
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightPcscTestReader(variant);

                using (ReaderDeviceScope.Use(reader))
                {
                    await UltralightScenarios.AReadOfTheUserPagesAsync(CARD_TYPE.Unspecified);
                    Assert.Contains("select", reader.Calls);

                    await UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(CARD_TYPE.Unspecified, Same);
                    await UltralightScenarios.AWriteToAProtectedPageIsRefusedBeforeTheCardAsync(CARD_TYPE.Unspecified);

                    // There is no chip to name wrongly.
                    await SkipOf(() => UltralightScenarios.AnExpectationThatIsNotTheChipIsRefusedAsync(CARD_TYPE.Unspecified));
                }

                Assert.DoesNotContain(reader.Calls, call => call.StartsWith("write", StringComparison.Ordinal));
                Assert.DoesNotContain(reader.Calls, call => call.StartsWith("read", StringComparison.Ordinal));
            });
        }

        [Theory]
        [InlineData(CARD_TYPE.DESFireEV1)]
        [InlineData(CARD_TYPE.Mifare1K)]
        [InlineData(CARD_TYPE.NTAG_424)]
        [InlineData(CARD_TYPE.Unspecified)]
        public async Task ACardOfAnotherFamily_SkipsEveryCase_AndTheReaderIsNotAsked(CARD_TYPE type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightTestReader(UltralightVariant.Ntag215);

                using (ReaderDeviceScope.Use(reader))
                {
                    await SkipOf(() => UltralightScenarios.AReadOfTheUserPagesAsync(type));
                    await SkipOf(() => UltralightScenarios.AnExpectationThatIsNotTheChipIsRefusedAsync(type));
                    await SkipOf(() => UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(type, Same));
                    await SkipOf(() => UltralightScenarios.AWriteToAProtectedPageIsRefusedBeforeTheCardAsync(type));
                }

                Assert.Empty(reader.Calls);
            });
        }

        [Fact]
        public async Task AChipWithoutAKnownLayout_IsRefusedByEveryReader_AndTheCaseThatNeedsAChipToNameSkips()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // An NTAG210 has a type but no layout: the reader finds out what the chip is by itself (its answer to GET_VERSION: an
                // NTAG with 48 bytes of memory) and says no.
                var reader = new UltralightTestReader(UltralightVariant.Ntag213) { VersionAnswer = Convert.FromHexString("0004040201000B03") };

                using (ReaderDeviceScope.Use(reader))
                {
                    await UltralightScenarios.AReadOfTheUserPagesAsync(CARD_TYPE.NTAG_210);
                    await SkipOf(() => UltralightScenarios.AWriteToAProtectedPageIsRefusedBeforeTheCardAsync(CARD_TYPE.NTAG_210));
                }

                Assert.DoesNotContain("write " + HilUltralightTasks.UserPage, reader.Calls);
            });
        }
    }
}
