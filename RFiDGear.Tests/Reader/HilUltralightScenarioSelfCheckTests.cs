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

        // The failure of a case that is shown to find a fault; a skip is not one, and a case that finds nothing is a failure here.
        private static async Task<Exception> FailureOf(Func<Task> scenario)
        {
            try
            {
                await scenario();
            }
            catch (SkipException)
            {
                throw;
            }
            catch (Exception failure)
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
                    await UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(type);
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
                    await FailureOf(() => UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(CARD_TYPE.NTAG_215));
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
                    var failure = await FailureOf(() => UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(CARD_TYPE.NTAG_215));

                    Assert.IsType<InvalidOperationException>(failure);
                    Assert.Contains("could NOT be put back", failure.Message, StringComparison.Ordinal);
                    Assert.NotEqual(before, reader.PagesOf(HilUltralightTasks.UserPage, 1));
                }
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
                    await UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(type);
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
                    await UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(type);
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
        [InlineData(CARD_TYPE.DESFireEV1)]
        [InlineData(CARD_TYPE.Mifare1K)]
        [InlineData(CARD_TYPE.NTAG_424)]
        public async Task ACardOfAnotherFamily_SkipsEveryCase_AndTheReaderIsNotAsked(CARD_TYPE type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new UltralightTestReader(UltralightVariant.Ntag215);

                using (ReaderDeviceScope.Use(reader))
                {
                    await SkipOf(() => UltralightScenarios.AReadOfTheUserPagesAsync(type));
                    await SkipOf(() => UltralightScenarios.AnExpectationThatIsNotTheChipIsRefusedAsync(type));
                    await SkipOf(() => UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync(type));
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
