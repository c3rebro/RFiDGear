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
            private readonly IClassicPort? port;

            public ClassicPcscTestReader(IClassicPort? port)
            {
                this.port = port;
            }

            public int Opened { get; private set; }

            protected override Task<IClassicPort> OpenClassicPortAsync()
            {
                Opened++;

                return Task.FromResult(port!);
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
            var reader = new ClassicPcscTestReader(null);

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
            var chip = new SimulatedClassicChip();
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
