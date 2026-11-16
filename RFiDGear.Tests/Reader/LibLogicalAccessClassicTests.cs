using System;
using System.Linq;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Classic;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Tests.Classic;
using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The Classic read of the PC/SC provider, against a chip in memory: what the dialogs get for a sector (the model with one entry per block),
    /// and that a call that did nothing is no success.
    /// </summary>
    public class LibLogicalAccessClassicTests
    {
        private const string Ff = "FFFFFFFFFFFF";

        // A provider whose chip is the simulated one (or none).
        private sealed class ClassicPcscTestReader : LibLogicalAccessProvider
        {
            private readonly Func<int, IClassicPort?> portOf;

            public ClassicPcscTestReader(IClassicPort? port)
                : this(_ => port)
            {
            }

            // The chip that the n-th operation finds (1 is the first), or none.
            public ClassicPcscTestReader(Func<int, IClassicPort?> portOf)
            {
                this.portOf = portOf;
            }

            public int Opened { get; private set; }

            protected override Task<IClassicPort> OpenClassicPortAsync()
            {
                Opened++;

                return Task.FromResult(portOf(Opened)!);
            }
        }

        [Fact]
        public async Task ASector_IsReadIntoTheModelOfTheDialogs_OneEntryPerBlock()
        {
            var chip = new SimulatedClassicChip();
            var reader = new ClassicPcscTestReader(chip);

            chip.Poke(5, Enumerable.Repeat((byte)0x5A, 16).ToArray());

            var result = await reader.ReadMifareClassicSingleSector(1, Ff, Ff);

            Assert.Equal(ERROR.NoError, result);
            Assert.True(reader.Sector.IsAuthenticated);
            Assert.Equal(new[] { 4, 5, 6, 7 }, reader.Sector.DataBlock.Select(b => b.DataBlockNumberChipBased));
            Assert.Equal(new[] { 0, 1, 2, 3 }, reader.Sector.DataBlock.Select(b => b.DataBlockNumberSectorBased));
            Assert.All(reader.Sector.DataBlock, b => Assert.True(b.IsAuthenticated));
            Assert.Equal(Enumerable.Repeat((byte)0x5A, 16).ToArray(), reader.Sector.DataBlock[1].Data);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ASectorOfALarge4KSector_HasSixteenBlocks()
        {
            var reader = new ClassicPcscTestReader(new SimulatedClassicChip());

            var result = await reader.ReadMifareClassicSingleSector(33, Ff, Ff);

            Assert.Equal(ERROR.NoError, result);
            Assert.Equal(Enumerable.Range(144, 16), reader.Sector.DataBlock.Select(b => b.DataBlockNumberChipBased));
            Assert.Equal(Enumerable.Range(0, 16), reader.Sector.DataBlock.Select(b => b.DataBlockNumberSectorBased));
        }

        [Fact]
        public async Task ABlockThatNoKeyCouldRead_HasNoData_AndTheSectorIsStillRead()
        {
            var chip = new SimulatedClassicChip();

            // The second group of blocks is for key B only, and there is no key B.
            chip.SetTrailer(6, Convert.FromHexString("A0A1A2A3A4A5"), new ClassicAccessBits(1, 6, 0, 4), 0x00, Convert.FromHexString(Ff));

            var reader = new ClassicPcscTestReader(chip);
            var result = await reader.ReadMifareClassicSingleSector(6, "A0A1A2A3A4A5", string.Empty);

            Assert.Equal(ERROR.NoError, result);
            Assert.True(reader.Sector.IsAuthenticated);
            Assert.True(reader.Sector.DataBlock[0].IsAuthenticated);
            Assert.False(reader.Sector.DataBlock[1].IsAuthenticated);
            Assert.Null(reader.Sector.DataBlock[1].Data);
            Assert.True(reader.Sector.DataBlock[2].IsAuthenticated);
        }

        [Fact]
        public async Task WrongKeys_AreAnAuthFailure_AndTheSectorIsEmpty()
        {
            var reader = new ClassicPcscTestReader(new SimulatedClassicChip());

            var result = await reader.ReadMifareClassicSingleSector(2, "000000000000", "111111111111");

            Assert.Equal(ERROR.AuthFailure, result);
            Assert.False(reader.Sector.IsAuthenticated);
            Assert.Empty(reader.Sector.DataBlock);
        }

        [Theory]
        [InlineData("error 1", Ff)]
        [InlineData(Ff, "GGGGGGGGGGGG")]
        [InlineData("FFFFFFFFFFF", Ff)]
        public async Task AKeyThatIsNoKey_NeverReachesTheChip(string keyA, string keyB)
        {
            var reader = new ClassicPcscTestReader(new SimulatedClassicChip());

            var result = await reader.ReadMifareClassicSingleSector(2, keyA, keyB);

            Assert.Equal(ERROR.AuthFailure, result);
            Assert.Equal(0, reader.Opened);
            Assert.NotNull(reader.Sector);
        }

        [Fact]
        public async Task WithoutAChip_TheReadIsATransportError()
        {
            var reader = new ClassicPcscTestReader((IClassicPort?)null);

            var result = await reader.ReadMifareClassicSingleSector(2, Ff, Ff);

            Assert.Equal(ERROR.TransportError, result);
            Assert.Equal(1, reader.Opened);
            Assert.False(reader.Sector.IsAuthenticated);
        }

        [Fact]
        public async Task AChipThatIsTakenAway_IsATransportError()
        {
            var chip = new SimulatedClassicChip { CardPresent = false };
            var result = await new ClassicPcscTestReader(chip).ReadMifareClassicSingleSector(2, Ff, Ff);

            Assert.Equal(ERROR.TransportError, result);
        }

        [Fact]
        public async Task ASectorThatTheChipDoesNotHave_IsAProtocolConstraint_AndNothingIsAskedOfTheChip()
        {
            var chip = new SimulatedClassicChip(ClassicCardSize.K1);
            var result = await new ClassicPcscTestReader(chip).ReadMifareClassicSingleSector(20, Ff, Ff);

            Assert.Equal(ERROR.ProtocolConstraint, result);
            Assert.Empty(chip.Log);
        }

        [Fact]
        public async Task TwoReadsInARow_DoNotRunTogether()
        {
            // Every call of the chip gives up the thread, so the two reads would interleave their calls if nothing held the second back.
            var chip = new SimulatedClassicChip { YieldsOnEveryCall = true };
            var reader = new ClassicPcscTestReader(chip);

            var results = await Task.WhenAll(
                reader.ReadMifareClassicSingleSector(1, Ff, Ff),
                reader.ReadMifareClassicSingleSector(2, Ff, Ff));

            Assert.All(results, r => Assert.Equal(ERROR.NoError, r));

            // Each call logs in, reads the trailer and the three blocks; none was interleaved with the other (the gate holds the second back).
            var logins = chip.Log.Select((entry, index) => (entry, index)).Where(x => x.entry.StartsWith("login", StringComparison.Ordinal)).Select(x => x.index).ToArray();

            Assert.Equal(2, logins.Length);
            Assert.Equal(5, logins[1] - logins[0]);
        }

        [Fact]
        public async Task ARead_ThatFailsAfterAnotherRead_DoesNotShowTheSectorOfTheOther()
        {
            // The second read waits for the gate while the first one runs, and then finds no chip. The sector that it leaves must be its own
            // (nothing), not the authenticated sector that the first one finished with.
            var chip = new SimulatedClassicChip { YieldsOnEveryCall = true };
            var reader = new ClassicPcscTestReader(open => open == 1 ? chip : null);

            var results = await Task.WhenAll(
                reader.ReadMifareClassicSingleSector(1, Ff, Ff),
                reader.ReadMifareClassicSingleSector(2, Ff, Ff));

            Assert.Equal(ERROR.NoError, results[0]);
            Assert.Equal(ERROR.TransportError, results[1]);
            Assert.False(reader.Sector.IsAuthenticated);
            Assert.Empty(reader.Sector.DataBlock);
        }

        [Fact]
        public async Task ARead_ThatIsRefusedBeforeItStarts_WaitsForTheGateLikeAnyOther()
        {
            // A key that is no key never reaches the chip, but the sector that the call leaves is set under the gate too: the read before it
            // finishes first, and the refused one leaves its own (empty) sector after it, not before it.
            var chip = new SimulatedClassicChip { YieldsOnEveryCall = true };
            var reader = new ClassicPcscTestReader(chip);

            var results = await Task.WhenAll(
                reader.ReadMifareClassicSingleSector(1, Ff, Ff),
                reader.ReadMifareClassicSingleSector(2, "error 1", Ff));

            Assert.Equal(ERROR.NoError, results[0]);
            Assert.Equal(ERROR.AuthFailure, results[1]);
            Assert.False(reader.Sector.IsAuthenticated);
            Assert.Empty(reader.Sector.DataBlock);
        }

        [Fact]
        public async Task TheSectorOfASectorRead_IsHandedBackWithTheAnswer_AndIsTheOneOfThatCall()
        {
            // Two reads at the same time: each gets its own sector, whatever the reader shows in its shared property afterwards.
            var chip = new SimulatedClassicChip { YieldsOnEveryCall = true };
            var reader = new ClassicPcscTestReader(chip);

            var results = await Task.WhenAll(
                reader.ReadMifareClassicSectorAsync(1, Ff, Ff),
                reader.ReadMifareClassicSectorAsync(2, Ff, Ff));

            Assert.Equal(ERROR.NoError, results[0].Error);
            Assert.Equal(ERROR.NoError, results[1].Error);
            Assert.Equal(new[] { 4, 5, 6, 7 }, results[0].Sector.DataBlock.Select(b => b.DataBlockNumberChipBased));
            Assert.Equal(new[] { 8, 9, 10, 11 }, results[1].Sector.DataBlock.Select(b => b.DataBlockNumberChipBased));
        }

        [Fact]
        public async Task ASectorRead_ThatFails_HandsBackAnEmptySectorOfItsOwn_AndTheOtherKeepsItsSector()
        {
            var chip = new SimulatedClassicChip { YieldsOnEveryCall = true };
            var reader = new ClassicPcscTestReader(open => open == 1 ? chip : null);

            var results = await Task.WhenAll(
                reader.ReadMifareClassicSectorAsync(1, Ff, Ff),
                reader.ReadMifareClassicSectorAsync(2, Ff, Ff));

            Assert.Equal(ERROR.NoError, results[0].Error);
            Assert.True(results[0].Sector.IsAuthenticated);
            Assert.Equal(4, results[0].Sector.DataBlock.Count);
            Assert.Equal(ERROR.TransportError, results[1].Error);
            Assert.NotNull(results[1].Sector);
            Assert.False(results[1].Sector.IsAuthenticated);
            Assert.Empty(results[1].Sector.DataBlock);
        }

        [Fact]
        public async Task ASectorRead_WithAKeyThatIsNoKey_HandsBackAnEmptySector_AndNeverReachesTheChip()
        {
            var reader = new ClassicPcscTestReader(new SimulatedClassicChip());

            var result = await reader.ReadMifareClassicSectorAsync(2, "error 1", Ff);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.NotNull(result.Sector);
            Assert.Empty(result.Sector.DataBlock);
            Assert.Equal(0, reader.Opened);
        }

        [Fact]
        public async Task TheLegacyRead_StillLeavesItsSectorInTheProperty_AndTheNewOneDoesNot()
        {
            var reader = new ClassicPcscTestReader(new SimulatedClassicChip());

            await reader.ReadMifareClassicSectorAsync(1, Ff, Ff);

            Assert.Null(reader.Sector);

            await reader.ReadMifareClassicSingleSector(1, Ff, Ff);

            Assert.True(reader.Sector.IsAuthenticated);
        }

        [Fact]
        public async Task ABlock_IsWrittenThroughTheProvider_AndReadBack()
        {
            var chip = new SimulatedClassicChip();
            var reader = new ClassicPcscTestReader(chip);
            var data = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();

            Assert.Equal(ERROR.NoError, await reader.WriteMifareClassicSingleBlock(5, Ff, Ff, data));
            Assert.Equal(data, chip.Peek(5));
            Assert.Equal("read 5", chip.Log[^1]);
        }

        [Fact]
        public async Task ASector_IsWrittenThroughTheProvider_AndNotTheTrailer()
        {
            var chip = new SimulatedClassicChip();
            var reader = new ClassicPcscTestReader(chip);
            var trailer = chip.Peek(11);
            var data = Enumerable.Range(1, 48).Select(i => (byte)i).ToArray();

            Assert.Equal(ERROR.NoError, await reader.WriteMifareClassicSingleSector(2, Ff, Ff, data));
            Assert.Equal(data.Skip(32), chip.Peek(10));
            Assert.Equal(trailer, chip.Peek(11));
        }

        [Fact]
        public async Task SectorZero_TakesThirtyTwoBytes_AndNeverTouchesTheManufacturerBlock()
        {
            var chip = new SimulatedClassicChip();
            var reader = new ClassicPcscTestReader(chip);
            var block0 = chip.Peek(0);
            var data = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

            Assert.Equal(ERROR.NoError, await reader.WriteMifareClassicSingleSector(0, Ff, Ff, data));
            Assert.Equal(block0, chip.Peek(0));
            Assert.Equal(data.Take(16), chip.Peek(1));
            Assert.Equal(data.Skip(16), chip.Peek(2));

            // 48 bytes would be a write of block 0: it is refused before any block is touched.
            var writes = chip.Writes;

            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicSingleSector(0, Ff, Ff, new byte[48]));
            Assert.Equal(writes, chip.Writes);
        }

        [Fact]
        public async Task ALargeSector_TakesTwoHundredAndFortyBytes_AndNeverTheTrailer()
        {
            var chip = new SimulatedClassicChip();
            var reader = new ClassicPcscTestReader(chip);
            var trailer = chip.Peek(143);
            var data = Enumerable.Range(1, 240).Select(i => (byte)i).ToArray();

            Assert.Equal(ERROR.NoError, await reader.WriteMifareClassicSingleSector(32, Ff, Ff, data));
            Assert.Equal(data.Skip(224), chip.Peek(142));
            Assert.Equal(trailer, chip.Peek(143));

            // 256 bytes would be a write of the trailer: it is refused before any block is touched.
            var writes = chip.Writes;

            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicSingleSector(32, Ff, Ff, new byte[256]));
            Assert.Equal(writes, chip.Writes);
        }

        [Fact]
        public async Task ATrailer_MayBeWrittenThroughTheBlockWrite_WhenItsAccessBitsAreConsistent()
        {
            // The dialog writes the keys and the access bits of a sector through this call: that stays, with the checks.
            var chip = new SimulatedClassicChip();
            var reader = new ClassicPcscTestReader(chip);
            var trailer = ClassicAccessBits.MadStandard.BuildTrailer(new byte[] { 1, 2, 3, 4, 5, 6 }, 0xC1, new byte[] { 7, 8, 9, 10, 11, 12 });

            Assert.Equal(ERROR.NoError, await reader.WriteMifareClassicSingleBlock(7, Ff, Ff, trailer));
            Assert.Equal(trailer, chip.Peek(7));
        }

        [Fact]
        public async Task WhatThePolicyRefuses_ThroughTheBlockWrite_TouchesNoChip()
        {
            var chip = new SimulatedClassicChip();
            var reader = new ClassicPcscTestReader(chip);
            var broken = ClassicAccessBits.Transport.BuildTrailer(new byte[6], 0, new byte[6]);
            var final = new ClassicAccessBits(0, 0, 0, 1).BuildTrailer(new byte[6], 0, new byte[6]);

            broken[7] ^= 0x01;

            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicSingleBlock(0, Ff, Ff, new byte[16]));
            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicSingleBlock(7, Ff, Ff, broken));
            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicSingleBlock(7, Ff, Ff, final));
            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicSingleBlock(5, Ff, Ff, null));
            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicSingleBlock(5, Ff, Ff, new byte[15]));
            Assert.Empty(chip.Log);
        }

        [Fact]
        public async Task AWriteWithAKeyThatIsNoKey_NeverReachesTheChip()
        {
            var reader = new ClassicPcscTestReader(new SimulatedClassicChip());

            Assert.Equal(ERROR.AuthFailure, await reader.WriteMifareClassicSingleBlock(5, "error 1", Ff, new byte[16]));
            Assert.Equal(ERROR.AuthFailure, await reader.WriteMifareClassicSingleSector(2, Ff, "ZZ", new byte[48]));
            Assert.Equal(0, reader.Opened);
        }

        [Fact]
        public async Task AWriteWithoutAChip_IsATransportError()
        {
            var reader = new ClassicPcscTestReader((IClassicPort?)null);

            Assert.Equal(ERROR.TransportError, await reader.WriteMifareClassicSingleBlock(5, Ff, Ff, new byte[16]));
            Assert.Equal(ERROR.TransportError, await reader.WriteMifareClassicSingleSector(2, Ff, Ff, new byte[48]));
        }

        [Fact]
        public async Task AFaultOfThePort_IsATransportError_AndTheGateIsFreeAgain()
        {
            var reader = new ClassicPcscTestReader(new ThrowingPort());

            Assert.Equal(ERROR.TransportError, await reader.WriteMifareClassicSingleBlock(5, Ff, Ff, new byte[16]));

            // The gate was released: a second call is not held up for ever.
            Assert.Equal(ERROR.TransportError, await reader.ReadMifareClassicSingleSector(1, Ff, Ff));
        }

        private sealed class ThrowingPort : IClassicPort
        {
            public ClassicCardSize? Size => ClassicCardSize.K4;

            public Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key) =>
                throw new InvalidOperationException("The reader failed at block " + block + " with " + keyType + " and " + key.Length + " bytes.");

            public Task<ClassicBlockResult> ReadBlockAsync(int block) =>
                throw new InvalidOperationException("The reader failed at block " + block + ".");

            public Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data) =>
                throw new InvalidOperationException("The reader failed at block " + block + " with " + data.Length + " bytes.");

            public Task<ClassicPortResult> ReselectAsync() => throw new InvalidOperationException("The reader failed.");
        }

        [Theory]
        [InlineData("Mifare1K", ClassicCardSize.K1)]
        [InlineData("Mifare2K", ClassicCardSize.K2)]
        [InlineData("Mifare4K", ClassicCardSize.K4)]
        [InlineData("MifareMini", ClassicCardSize.Mini)]
        public void TheCardTypeOfTheLibrary_IsMappedToASize(string cardType, ClassicCardSize expected)
        {
            Assert.Equal(expected, LlaClassicPort.SizeOf(cardType));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("MifareUltralight")]
        [InlineData("DESFireEV1")]
        public void ACardTypeThatIsNoClassicSize_IsNotGuessed(string? cardType)
        {
            Assert.Null(LlaClassicPort.SizeOf(cardType));
        }

        [Fact]
        public async Task TheMadCalls_AreATransportError_WhenThereIsNoReaderOrNoChip_AndNotASuccess()
        {
            // A provider without a reader (nothing is opened): both calls did nothing, which is no success.
            var reader = new LibLogicalAccessProvider();

            Assert.Equal(
                ERROR.TransportError,
                await reader.ReadMifareClassicWithMAD(0x1234, Ff, Ff, Ff, Ff, 16, 0xC1, true, true));

            Assert.Equal(
                ERROR.TransportError,
                await reader.WriteMifareClassicWithMAD(0x1234, 1, Ff, Ff, Ff, Ff, Ff, Ff, Ff, Ff, new byte[16], 0xC1, default(SectorAccessBits), true, true));
        }
    }
}
