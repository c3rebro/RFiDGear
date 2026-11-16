using System;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.HIL.Tests;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Tests.Classic;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The steps of the MIFARE Classic hardware cases (<c>RFiDGear.HIL.Tests/ClassicScenarios.cs</c>, compiled into this project too) run
    /// against a chip in memory behind the real reader code: every case has to pass for a chip as it leaves the factory, a card that is not one
    /// has to skip, and a case that writes has to put the card back, also after a step that failed, and never write to a card that was replaced.
    /// </summary>
    public sealed class HilClassicScenarioSelfCheckTests
    {
        private sealed class ScenarioReader : LibLogicalAccessProvider
        {
            private readonly IClassicPort port;

            public ScenarioReader(IClassicPort port)
            {
                this.port = port;
            }

            protected override Task<IClassicPort> OpenClassicPortAsync() => Task.FromResult(port);
        }

        private static Task<string?> Same() => Task.FromResult<string?>("04AABBCC");

        // A case that skips ends the test as skipped, whether it is caught by Assert.Throws or not (xunit.v3), so the exception is caught by hand.
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

        private static byte[][] Everything(SimulatedClassicChip chip, ClassicCardSize size) =>
            Enumerable.Range(0, ClassicLayout.BlockCount(size)).Select(chip.Peek).ToArray();

        [Theory]
        [InlineData(ClassicCardSize.K4)]
        [InlineData(ClassicCardSize.K2)]
        [InlineData(ClassicCardSize.K1)]
        [InlineData(ClassicCardSize.Mini)]
        public async Task EveryCase_PassesOnAChipAsItLeavesTheFactory_AndLeavesItAsItWas(ClassicCardSize size)
        {
            var chip = new SimulatedClassicChip(size);
            var reader = new ScenarioReader(chip);
            var before = Everything(chip, size);

            await ClassicScenarios.AFactorySectorIsReadAsync(reader, size);
            await ClassicScenarios.AWrongKeyIsRefused_AndTheNextReadWorksAsync(reader);
            await ClassicScenarios.ABlockIsWrittenReadBackAndPutBackAsync(reader, Same);
            await ClassicScenarios.ASectorIsWrittenReadBackAndPutBackAsync(reader, size, Same);
            await ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(reader, size, Same);

            Assert.Equal(before, Everything(chip, size));
            Assert.True(chip.Writes > 0);
        }

        [Fact]
        public async Task TheReadCases_NeverWriteToTheCard()
        {
            var chip = new SimulatedClassicChip();
            var reader = new ScenarioReader(chip);

            await ClassicScenarios.AFactorySectorIsReadAsync(reader, ClassicCardSize.K4);
            await ClassicScenarios.AWrongKeyIsRefused_AndTheNextReadWorksAsync(reader);

            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ACardThatTakesNotTheFactoryKey_MakesEveryCaseSkip_AndWritesNothing()
        {
            var chip = new SimulatedClassicChip();

            foreach (var sector in new[] { 0, 1, 2, 33 })
            {
                chip.SetTrailer(sector, Convert.FromHexString("A0A1A2A3A4A5"), ClassicAccessBits.Transport, 0x69, Convert.FromHexString("B0B1B2B3B4B5"));
            }

            var reader = new ScenarioReader(chip);

            await SkipOf(() => ClassicScenarios.AFactorySectorIsReadAsync(reader, ClassicCardSize.K4));
            await SkipOf(() => ClassicScenarios.AWrongKeyIsRefused_AndTheNextReadWorksAsync(reader));
            await SkipOf(() => ClassicScenarios.ABlockIsWrittenReadBackAndPutBackAsync(reader, Same));
            await SkipOf(() => ClassicScenarios.ASectorIsWrittenReadBackAndPutBackAsync(reader, ClassicCardSize.K4, Same));
            await SkipOf(() => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(reader, ClassicCardSize.K4, Same));

            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ACardThatHasAMad_IsNotGivenAnother()
        {
            var chip = new SimulatedClassicChip();
            var mad = ClassicMad.Create(ClassicMadVersion.Mad1);
            var bytes = mad.ToMad1Bytes();

            chip.Poke(1, bytes[..16]);
            chip.Poke(2, bytes[16..32]);
            chip.SetTrailer(0, Convert.FromHexString("FFFFFFFFFFFF"), ClassicAccessBits.Transport, ClassicMadGpb.Create(ClassicMadVersion.Mad1, true), Convert.FromHexString("FFFFFFFFFFFF"));

            await SkipOf(() => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(new ScenarioReader(chip), ClassicCardSize.K4, Same));

            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ABlockWrite_ThatTheCardRefuses_IsFound_AndTheBlockIsUnchanged()
        {
            var chip = new SimulatedClassicChip();
            var before = chip.Peek(5);

            chip.FailNextWriteOf(5);

            var failure = await FailureOf(() => ClassicScenarios.ABlockIsWrittenReadBackAndPutBackAsync(new ScenarioReader(chip), Same));

            Assert.NotNull(failure);
            Assert.Equal(before, chip.Peek(5));
        }

        [Fact]
        public async Task ABlockThatDoesNotReadBackAsWritten_IsFound_AndIsPutBack()
        {
            // The chip corrupts what it stores: the write is reported, and the put-back is attempted with the same chip (and fails to be exact).
            var chip = new SimulatedClassicChip { CorruptsWrites = true };

            var failure = await FailureOf(() => ClassicScenarios.ABlockIsWrittenReadBackAndPutBackAsync(new ScenarioReader(chip), Same));

            Assert.NotNull(failure);
        }

        [Fact]
        public async Task ACardThatWasReplaced_GetsNothingPutBack()
        {
            var chip = new SimulatedClassicChip();
            var asked = 0;

            // The first two answers (before and after the card is read) are the card that the case starts with; every later one is another card.
            Task<string?> Replaced() => Task.FromResult<string?>(asked++ < 2 ? "04AABBCC" : "04112233");

            var failure = await FailureOf(() => ClassicScenarios.ABlockIsWrittenReadBackAndPutBackAsync(new ScenarioReader(chip), Replaced));

            Assert.NotNull(failure);
            Assert.Equal(0, chip.Writes);
        }

        public static TheoryData<string> WritingCases => new TheoryData<string> { "block", "sector", "mad" };

        private static Task RunWritingCase(string name, ScenarioReader reader, Func<Task<string?>> who) => name switch
        {
            "block" => ClassicScenarios.ABlockIsWrittenReadBackAndPutBackAsync(reader, who),
            "sector" => ClassicScenarios.ASectorIsWrittenReadBackAndPutBackAsync(reader, ClassicCardSize.K4, who),
            _ => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(reader, ClassicCardSize.K4, who)
        };

        [Theory]
        [MemberData(nameof(WritingCases))]
        public async Task ACardThatIsReplacedWhileItIsRead_MakesTheWritingCaseSkip_AndWritesNothing(string name)
        {
            // The card that is read is not the card that was there when the case started: whatever it held is no snapshot to put back.
            var chip = new SimulatedClassicChip();
            var asked = 0;

            Task<string?> Replaced() => Task.FromResult<string?>(asked++ == 0 ? "04AABBCC" : "04112233");

            await SkipOf(() => RunWritingCase(name, new ScenarioReader(chip), Replaced));

            Assert.Equal(0, chip.Writes);
        }

        [Theory]
        [MemberData(nameof(WritingCases))]
        public async Task AReaderThatDoesNotSayWhoIsOnIt_MakesTheWritingCaseSkip_AndWritesNothing(string name)
        {
            var chip = new SimulatedClassicChip();

            await SkipOf(() => RunWritingCase(name, new ScenarioReader(chip), () => Task.FromResult<string?>(null)));

            Assert.Equal(0, chip.Writes);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(64)]
        public async Task ACardThatHoldsDataWhereTheMadCaseWritesZeros_MakesTheCaseSkip_AndWritesNothing(int block)
        {
            // The put-back writes zeros there: a card with something in these blocks would lose it. The case leaves such a card alone.
            var chip = new SimulatedClassicChip();

            chip.Poke(block, Enumerable.Repeat((byte)0x5A, 16).ToArray());

            await SkipOf(() => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(new ScenarioReader(chip), ClassicCardSize.K4, Same));

            Assert.Equal(0, chip.Writes);
        }

        [Theory]
        [InlineData(0x00)]
        [InlineData(0x40)]
        [InlineData(0xBC)]
        public async Task ACardWhoseTrailerHasAnotherGeneralPurposeByte_MakesTheMadCaseSkip_AndWritesNothing(byte gpb)
        {
            // The put-back writes 69 into the trailer: a card with another byte would lose it.
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(0, Convert.FromHexString("FFFFFFFFFFFF"), ClassicAccessBits.Transport, gpb, Convert.FromHexString("FFFFFFFFFFFF"));

            await SkipOf(() => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(new ScenarioReader(chip), ClassicCardSize.K4, Same));

            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ASectorWithFactoryKeysAndOtherAccessBits_IsNoFactorySector_AndMakesTheCasesThatUseItSkip()
        {
            // The keys are the factory keys, the access bits are those of the MAD: key B is no data there, and a write needs key B.
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(1, Convert.FromHexString("FFFFFFFFFFFF"), ClassicAccessBits.MadStandard, 0x69, Convert.FromHexString("FFFFFFFFFFFF"));

            var reader = new ScenarioReader(chip);

            await SkipOf(() => ClassicScenarios.AFactorySectorIsReadAsync(reader, ClassicCardSize.K4));
            await SkipOf(() => ClassicScenarios.AWrongKeyIsRefused_AndTheNextReadWorksAsync(reader));
            await SkipOf(() => ClassicScenarios.ABlockIsWrittenReadBackAndPutBackAsync(reader, Same));
            await SkipOf(() => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(reader, ClassicCardSize.K4, Same));

            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AKeyBThatIsNotTheFactoryKey_IsNoFactorySector_AndMakesTheCasesThatUseItSkip()
        {
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(1, Convert.FromHexString("FFFFFFFFFFFF"), ClassicAccessBits.Transport, 0x69, Convert.FromHexString("B0B1B2B3B4B5"));

            var reader = new ScenarioReader(chip);

            await SkipOf(() => ClassicScenarios.AWrongKeyIsRefused_AndTheNextReadWorksAsync(reader));
            await SkipOf(() => ClassicScenarios.ABlockIsWrittenReadBackAndPutBackAsync(reader, Same));

            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AMadCase_WhoseWriteStopsBeforeTheTrailers_FailsWithItsOwnError_AndNotWithAnIncompleteCleanup()
        {
            // The first trailer is not written: it is the factory trailer still, and the put-back does not try to write it with the keys of the case.
            var chip = new SimulatedClassicChip();
            var before = Everything(chip, ClassicCardSize.K4);

            chip.FailWriteOf(3, 1);

            var failure = await FailureOf(() => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(new ScenarioReader(chip), ClassicCardSize.K4, Same));

            Assert.DoesNotContain("could not be put back", failure.Message, StringComparison.Ordinal);
            Assert.Equal(before, Everything(chip, ClassicCardSize.K4));
        }

        [Fact]
        public async Task AMadCase_WhoseCardIsReplacedDuringThePutBack_WritesNothingMoreToTheOtherCard()
        {
            var chip = new SimulatedClassicChip();
            var asked = 0;

            // Before the read, after it, in the steps, before the put-back: the card of the case. Then every answer is another card.
            Task<string?> Replaced() => Task.FromResult<string?>(asked++ < 4 ? "04AABBCC" : "04112233");

            var failure = await FailureOf(() => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(new ScenarioReader(chip), ClassicCardSize.K4, Replaced));

            Assert.Contains("replaced during the put-back", failure.Message, StringComparison.Ordinal);

            // Nothing was put back: the data of the case is still in block 4.
            Assert.Contains(chip.Peek(4), b => b != 0);
        }

        [Fact]
        public async Task AMadCase_WhoseCleanupFails_FailsAndSaysWhatIsLeft_EvenWhenTheMadOfTheFirstSectorIsGone()
        {
            // The trailer of sector 16 is written twice: by the writer of the MAD (that works) and by the put-back (the chip refuses it). Sector 0 is
            // put back, so the card has no MAD1 any more; what is left is the changed sector 16 with the temporary keys, and the case has to say so.
            var chip = new SimulatedClassicChip();

            chip.FailWriteOf(67, 2);

            var failure = await FailureOf(() => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(new ScenarioReader(chip), ClassicCardSize.K4, Same));

            Assert.Contains("could not be put back", failure.Message, StringComparison.Ordinal);
            Assert.Contains("block 67", failure.Message, StringComparison.Ordinal);
            Assert.NotEqual(Convert.FromHexString("FFFFFFFFFFFF"), chip.Peek(67).Take(6).ToArray());
        }

        [Fact]
        public async Task AMadCase_ThatStopsHalfWay_PutsTheMadSectorsBack()
        {
            var chip = new SimulatedClassicChip();
            var size = ClassicCardSize.K4;
            var before = Everything(chip, size);

            // The write of the first data block works, the directory is made, and then the chip refuses one of the trailers of the sectors.
            chip.FailNextWriteOf(67);

            var failure = await FailureOf(() => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(new ScenarioReader(chip), size, Same));

            Assert.NotNull(failure);
            Assert.Equal(before, Everything(chip, size));
        }
    }
}
