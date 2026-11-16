using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Tests.Classic;
using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The Classic read of the Elatec reader, against a chip in memory: it is the same layer as the PC/SC reader's, so what is pinned here is
    /// the glue (the model for the dialogs, the refusals that touch no chip) and the two things that are the TWN4's own: how it numbers a sector,
    /// and which faults of the library mean that the reader is gone.
    /// </summary>
    public class ElatecClassicTests
    {
        private const string Ff = "FFFFFFFFFFFF";

        private sealed class ElatecClassicTestReader : ElatecNetProvider
        {
            private readonly IClassicPort? port;

            public ElatecClassicTestReader(IClassicPort? port)
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
            var reader = new ElatecClassicTestReader(chip);

            chip.Poke(9, Enumerable.Repeat((byte)0x3C, 16).ToArray());

            var result = await reader.ReadMifareClassicSingleSector(2, Ff, Ff);

            Assert.Equal(ERROR.NoError, result);
            Assert.True(reader.Sector.IsAuthenticated);
            Assert.Equal(new[] { 8, 9, 10, 11 }, reader.Sector.DataBlock.Select(b => b.DataBlockNumberChipBased));
            Assert.Equal(Enumerable.Repeat((byte)0x3C, 16).ToArray(), reader.Sector.DataBlock[1].Data);
            Assert.All(reader.Sector.DataBlock, b => Assert.True(b.IsAuthenticated));
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ALarge4KSector_IsReadWithSixteenBlocks()
        {
            var chip = new SimulatedClassicChip();
            var result = await new ElatecClassicTestReader(chip).ReadMifareClassicSingleSector(35, Ff, Ff);

            Assert.Equal(ERROR.NoError, result);
            Assert.Equal("login 35 KeyA", chip.Log[0]);
            Assert.Equal(16, chip.Log.Count(entry => entry.StartsWith("read", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task WrongKeys_AreAnAuthFailure_AndTheSectorIsEmpty()
        {
            var reader = new ElatecClassicTestReader(new SimulatedClassicChip());

            var result = await reader.ReadMifareClassicSingleSector(2, "000000000000", "111111111111");

            Assert.Equal(ERROR.AuthFailure, result);
            Assert.False(reader.Sector.IsAuthenticated);
            Assert.Empty(reader.Sector.DataBlock);
        }

        [Theory]
        [InlineData("error 1", Ff)]
        [InlineData(Ff, "ZZZZZZZZZZZZ")]
        [InlineData("FFFFFFFFFFF", Ff)]
        public async Task AKeyThatIsNoKey_NeverReachesTheChip(string keyA, string keyB)
        {
            var reader = new ElatecClassicTestReader(new SimulatedClassicChip());

            Assert.Equal(ERROR.AuthFailure, await reader.ReadMifareClassicSingleSector(2, keyA, keyB));
            Assert.Equal(0, reader.Opened);
        }

        [Fact]
        public async Task WithoutAChip_TheReadIsATransportError()
        {
            var reader = new ElatecClassicTestReader(null);

            Assert.Equal(ERROR.TransportError, await reader.ReadMifareClassicSingleSector(2, Ff, Ff));
            Assert.False(reader.Sector.IsAuthenticated);
        }

        [Fact]
        public async Task ARead_ThatIsRefusedBeforeItStarts_WaitsForTheLockLikeAnyOther()
        {
            // A key that is no key never reaches the chip, but the sector that the call leaves is set under the lock too: the read before it
            // finishes first, and the refused one leaves its own (empty) sector after it, not before it.
            var chip = new SimulatedClassicChip { YieldsOnEveryCall = true };
            var reader = new ElatecClassicTestReader(chip);

            var results = await Task.WhenAll(
                reader.ReadMifareClassicSingleSector(1, Ff, Ff),
                reader.ReadMifareClassicSingleSector(2, "error 1", Ff));

            Assert.Equal(ERROR.NoError, results[0]);
            Assert.Equal(ERROR.AuthFailure, results[1]);
            Assert.False(reader.Sector.IsAuthenticated);
            Assert.Empty(reader.Sector.DataBlock);
        }

        [Fact]
        public async Task TwoReadsInARow_DoNotRunTogether()
        {
            // Every call of the chip gives up the thread, so the two reads would interleave their calls if nothing held the second back.
            var chip = new SimulatedClassicChip { YieldsOnEveryCall = true };
            var reader = new ElatecClassicTestReader(chip);

            var results = await Task.WhenAll(
                reader.ReadMifareClassicSingleSector(1, Ff, Ff),
                reader.ReadMifareClassicSingleSector(2, Ff, Ff));

            Assert.All(results, r => Assert.Equal(ERROR.NoError, r));

            // Each call logs in, reads the trailer and the three blocks; none was interleaved with the other.
            var logins = chip.Log.Select((entry, index) => (entry, index)).Where(x => x.entry.StartsWith("login", StringComparison.Ordinal)).Select(x => x.index).ToArray();

            Assert.Equal(2, logins.Length);
            Assert.Equal(5, logins[1] - logins[0]);
        }

        [Fact]
        public async Task WithoutAReaderAtAll_TheReadIsATransportError()
        {
            // The default of the provider finds no connected reader, so there is no chip to ask.
            var reader = new ElatecNetProvider();

            Assert.Equal(ERROR.TransportError, await reader.ReadMifareClassicSingleSector(2, Ff, Ff));
        }

        [Fact]
        public async Task TheSectorWrite_IsRefused_AndTouchesNoChip()
        {
            // The method that was here wrote the data to the blocks 0 to 3, whatever sector was meant.
            var chip = new SimulatedClassicChip();
            var reader = new ElatecClassicTestReader(chip);

            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicSingleSector(5, Ff, Ff, new byte[16]));
            Assert.Equal(0, reader.Opened);
            Assert.Empty(chip.Log);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(3, 0)]
        [InlineData(4, 1)]
        [InlineData(127, 31)]
        [InlineData(128, 32)]
        [InlineData(143, 32)]
        [InlineData(144, 36)]
        [InlineData(159, 36)]
        [InlineData(160, 40)]
        [InlineData(255, 60)]
        public void TheTwn4_NumbersASectorByItsFirstBlockDividedByFour(int block, int number)
        {
            Assert.Equal(number, ElatecClassicPort.SectorNumberOf(block));
        }

        [Fact]
        public void TheTwn4_TakesKeyAAsZeroAndKeyBAsOne()
        {
            Assert.Equal(0, ElatecClassicPort.KeyTypeOf(ClassicKeyType.KeyA));
            Assert.Equal(1, ElatecClassicPort.KeyTypeOf(ClassicKeyType.KeyB));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(17)]
        [InlineData(48)]
        public void AnAnswerThatIsNotSixteenBytes_IsNoBlock(int? length)
        {
            var result = ElatecClassicPort.ToBlockResult(length == null ? null : new byte[length.Value]);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Null(result.Data);
            Assert.False(result.Succeeded);
        }

        [Fact]
        public void SixteenBytes_AreABlock()
        {
            var bytes = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            var result = ElatecClassicPort.ToBlockResult(bytes);

            Assert.True(result.Succeeded);
            Assert.Equal(bytes, result.Data);
        }

        [Theory]
        [InlineData(ReaderError.NotOpen)]
        [InlineData(ReaderError.NotInit)]
        [InlineData(ReaderError.IsAlreadyOpen)]
        [InlineData(ReaderError.Timeout)]
        public void AReaderThatIsNotThere_IsATransportError(ReaderError error)
        {
            Assert.Equal(ERROR.TransportError, ElatecClassicPort.Map(new ReaderException("port", error)));
        }

        [Theory]
        [InlineData("Call was not successfull, error Timeout")]
        [InlineData("Call was not successfull, error NotOpen")]
        [InlineData("Call was not successfull, error NotInit")]
        [InlineData("Call was not successfull, error IsAlreadyOpen")]
        public void AReaderThatIsNotThere_IsATransportError_WhenOnlyTheMessageSaysSo(string message)
        {
            // Elatec.NET leaves ErrorNumber as None at most of its throw sites and puts the name of the error in the message.
            Assert.Equal(ERROR.TransportError, ElatecClassicPort.Map(new ReaderException(message)));
        }

        [Theory]
        [InlineData("Call was not successfull, error AccessDenied")]
        [InlineData("Call was not successfull, error Banana")]
        [InlineData("Call was not successfull, error 7")]
        [InlineData("Call was not successfull")]
        public void AnAnswerThatNamesNoReaderThatIsGone_IsARefusalOfTheChip(string message)
        {
            Assert.Equal(ERROR.AuthFailure, ElatecClassicPort.Map(new ReaderException(message)));
        }

        [Theory]
        [InlineData(MifareChipSubType.Mifare1K, ClassicCardSize.K1)]
        [InlineData(MifareChipSubType.Mifare2K, ClassicCardSize.K2)]
        [InlineData(MifareChipSubType.Mifare4K, ClassicCardSize.K4)]
        [InlineData(MifareChipSubType.MifarePlus_SL1_1K, ClassicCardSize.K1)]
        [InlineData(MifareChipSubType.MifarePlus_SL1_2K, ClassicCardSize.K2)]
        [InlineData(MifareChipSubType.MifarePlus_SL1_4K, ClassicCardSize.K4)]
        public void TheSizeOfTheChipThatWasSelected_IsTheOneItIs(MifareChipSubType subType, ClassicCardSize expected)
        {
            Assert.Equal(expected, ElatecClassicPort.SizeOf(subType));
        }

        [Theory]
        [InlineData(MifareChipSubType.NOTAG)]
        [InlineData(MifareChipSubType.Unspecified)]
        [InlineData(MifareChipSubType.MifareClassic)]
        [InlineData(MifareChipSubType.MifarePlus_SL3_4K)]
        [InlineData(MifareChipSubType.DESFireEV1_4K)]
        [InlineData(MifareChipSubType.MifareUltralight)]
        public void AChipThatIsNoClassicChipOfAKnownSize_HasNoSize_AndIsNotGuessed(MifareChipSubType subType)
        {
            Assert.Null(ElatecClassicPort.SizeOf(subType));
        }

        [Fact]
        public void AnAnswerOfTheReader_IsARefusalOfTheChip()
        {
            Assert.Equal(ERROR.AuthFailure, ElatecClassicPort.Map(new ReaderException("login", ReaderError.General)));
            Assert.Equal(ERROR.AuthFailure, ElatecClassicPort.Map(new ReaderException("login")));
            Assert.Equal(ERROR.AuthFailure, ElatecClassicPort.Map(new TwnException("login", ResponseError.InvalidFunction)));
            Assert.Equal(ERROR.AuthFailure, ElatecClassicPort.Map(new Exception("something else")));
        }

        [Fact]
        public void AFaultOfThePortOrTheSystem_IsATransportError()
        {
            Assert.Equal(ERROR.TransportError, ElatecClassicPort.Map(new IOException()));
            Assert.Equal(ERROR.TransportError, ElatecClassicPort.Map(new TimeoutException()));
            Assert.Equal(ERROR.TransportError, ElatecClassicPort.Map(new ObjectDisposedException("port")));
            Assert.Equal(ERROR.TransportError, ElatecClassicPort.Map(new InvalidOperationException()));
            Assert.Equal(ERROR.TransportError, ElatecClassicPort.Map(new UnauthorizedAccessException()));
            Assert.Throws<ArgumentNullException>(() => ElatecClassicPort.Map(null!));
        }
    }
}
