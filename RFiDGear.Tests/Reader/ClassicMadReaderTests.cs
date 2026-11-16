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
    /// The MAD calls of the readers (the legacy parameters of the dialogs and tasks), against a chip in memory: what they hand to the
    /// directory operations, and that a call that cannot be carried out never reaches the chip.
    /// </summary>
    public class ClassicMadReaderTests
    {
        private const string Ff = "FFFFFFFFFFFF";
        private const string KeyAText = "A0A1A2A3A4A5";
        private const string KeyBText = "B0B1B2B3B4B5";
        private const string AppKeyAText = "C0C1C2C3C4C5";
        private const string AppKeyBText = "D0D1D2D3D4D5";
        private const int Aid = 0x1234;
        private static readonly byte[] KeyA = Convert.FromHexString(KeyAText);
        private static readonly byte[] KeyB = Convert.FromHexString(KeyBText);
        private static readonly byte[] Ff6 = Enumerable.Repeat((byte)0xFF, 6).ToArray();

        private sealed class MadPcscReader : LibLogicalAccessProvider
        {
            private readonly IClassicPort? port;

            public MadPcscReader(IClassicPort? port)
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

        private static byte[] Pattern(byte start, int length) => Enumerable.Range(start, length).Select(i => (byte)i).ToArray();

        // A chip with a MAD1 that lists sectors 1 and 2 for the AID.
        private static SimulatedClassicChip ChipWithApplication()
        {
            var chip = new SimulatedClassicChip();
            var mad = ClassicMad.Create(ClassicMadVersion.Mad1);

            mad.SetAid(1, Aid);
            mad.SetAid(2, Aid);

            var bytes = mad.ToMad1Bytes();

            chip.Poke(1, bytes[..16]);
            chip.Poke(2, bytes[16..32]);
            chip.SetTrailer(0, KeyA, ClassicAccessBits.MadStandard, ClassicMadGpb.Create(ClassicMadVersion.Mad1, true), KeyB);

            return chip;
        }

        // ── write ──

        [Fact]
        public async Task AnApplication_IsWrittenOnAChipWithoutAMad_WithTheKeysOfTheDialogs()
        {
            var chip = new SimulatedClassicChip();
            var reader = new MadPcscReader(chip);
            var data = Pattern(0x10, 20);

            var result = await reader.WriteMifareClassicWithMAD(
                Aid, 1, Ff, Ff, Ff, Ff, Ff, Ff, KeyAText, KeyBText, data, 0xC1, default, true, true);

            Assert.Equal(ERROR.NoError, result);
            Assert.Equal(data[..16], chip.Peek(4));

            // The keys of the application are the same as the current ones: they are no new keys, and no trailer of the application is written.
            Assert.DoesNotContain(7, chip.Log.Where(e => e.StartsWith("write ", StringComparison.Ordinal)).Select(e => int.Parse(e.Split(' ')[1])));

            // The directory is there, with the new keys of the MAD.
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0xC2, KeyB), chip.Peek(3));

            Assert.Equal(ERROR.NoError, await reader.ReadMifareClassicWithMAD(Aid, Ff, Ff, KeyAText, KeyBText, 20, 0xC1, true, false));
            Assert.Equal(data, reader.MifareClassicData);
        }

        [Fact]
        public async Task NewKeysOfTheApplication_AreWritten_WhenTheyDifferFromTheCurrentOnes()
        {
            var chip = ChipWithApplication();
            var reader = new MadPcscReader(chip);

            var result = await reader.WriteMifareClassicWithMAD(
                Aid, 0, Ff, Ff, AppKeyAText, AppKeyBText, KeyAText, KeyBText, KeyAText, KeyBText, Pattern(0x10, 20), 0xC1, default, true, true);

            Assert.Equal(ERROR.NoError, result);
            Assert.Equal(
                ClassicAccessBits.Transport.BuildTrailer(Convert.FromHexString(AppKeyAText), 0x69, Convert.FromHexString(AppKeyBText)),
                chip.Peek(7));

            // The keys of the MAD did not change: its trailer was not written.
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(KeyA, ClassicMadGpb.Create(ClassicMadVersion.Mad1, true), KeyB), chip.Peek(3));
        }

        [Fact]
        public async Task TheAccessBitsOfTheDialog_AreTakenAsTheNewAccessBitsOfTheSectors()
        {
            var chip = ChipWithApplication();
            var reader = new MadPcscReader(chip);
            var sab = new SectorAccessBits
            {
                d_data_block0_access_bits = default,
                d_data_block1_access_bits = default,
                d_data_block2_access_bits = default,
                d_sector_trailer_access_bits = new AccessBits { c1 = 0, c2 = 1, c3 = 1 }
            };

            var result = await reader.WriteMifareClassicWithMAD(
                Aid, 0, Ff, Ff, Ff, Ff, KeyAText, KeyBText, KeyAText, KeyBText, Pattern(0x10, 20), 0xC1, sab, true, true);

            Assert.Equal(ERROR.NoError, result);
            Assert.Equal(new ClassicAccessBits(0, 0, 0, 6).BuildTrailer(Ff6, 0x69, Ff6), chip.Peek(7));
        }

        [Fact]
        public async Task TheGeneralPurposeByteOfTheCall_ReachesTheMadSectors()
        {
            // 81: a MAD1 for one application. A 1K gets a MAD1, and the byte of the call is the byte of its trailer.
            var chip = new SimulatedClassicChip(ClassicCardSize.K1);
            var reader = new MadPcscReader(chip);

            var result = await reader.WriteMifareClassicWithMAD(
                Aid, 1, Ff, Ff, Ff, Ff, Ff, Ff, KeyAText, KeyBText, Pattern(0x10, 20), 0x81, default, true, true);

            Assert.Equal(ERROR.NoError, result);
            Assert.Equal(0x81, chip.Peek(3)[9]);
        }

        [Fact]
        public async Task AnEmptyNewKey_IsNoNewKey_AndNoTrailerIsWritten()
        {
            // The boxes for the new keys are empty: the sectors keep their keys, and no trailer is written (the MAD of the chip says the same already).
            var chip = ChipWithApplication();
            var reader = new MadPcscReader(chip);

            var result = await reader.WriteMifareClassicWithMAD(
                Aid, 0, Ff, Ff, string.Empty, string.Empty, KeyAText, KeyBText, string.Empty, string.Empty, Pattern(0x10, 20), 0xC1, default, true, true);

            Assert.Equal(ERROR.NoError, result);
            Assert.DoesNotContain(chip.Log, entry => entry == "write 3 (16 bytes)" || entry == "write 7 (16 bytes)");
        }

        [Fact]
        public async Task AWriteWithoutTheMad_IsRefused_AndTheChipIsNotAsked()
        {
            var reader = new MadPcscReader(new SimulatedClassicChip());

            Assert.Equal(
                ERROR.ProtocolConstraint,
                await reader.WriteMifareClassicWithMAD(Aid, 1, Ff, Ff, Ff, Ff, Ff, Ff, Ff, Ff, Pattern(0x10, 16), 0xC1, default, true, false));
            Assert.Equal(0, reader.Opened);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(70000)]
        public async Task AnAidThatIsNoAid_IsRefused_AndTheChipIsNotAsked(int aid)
        {
            var reader = new MadPcscReader(new SimulatedClassicChip());

            Assert.Equal(
                ERROR.ProtocolConstraint,
                await reader.WriteMifareClassicWithMAD(aid, 1, Ff, Ff, Ff, Ff, Ff, Ff, Ff, Ff, Pattern(0x10, 16), 0xC1, default, true, true));
            Assert.Equal(
                ERROR.ProtocolConstraint,
                await reader.ReadMifareClassicWithMAD(aid, Ff, Ff, Ff, Ff, 16, 0xC1, true, false));
            Assert.Equal(0, reader.Opened);
        }

        [Fact]
        public async Task AccessBitsThatAreNoAccessBits_AreRefused_AndNeverWrittenAsSomethingElse()
        {
            var reader = new MadPcscReader(new SimulatedClassicChip());
            var sab = new SectorAccessBits { d_data_block0_access_bits = new AccessBits { c1 = 2, c2 = 0, c3 = 0 } };

            Assert.Equal(
                ERROR.ProtocolConstraint,
                await reader.WriteMifareClassicWithMAD(Aid, 1, Ff, Ff, Ff, Ff, Ff, Ff, Ff, Ff, Pattern(0x10, 16), 0xC1, sab, true, true));
            Assert.Equal(0, reader.Opened);
        }

        [Theory]
        [InlineData("error 1", Ff, Ff, Ff)]
        [InlineData(Ff, "GGGGGGGGGGGG", Ff, Ff)]
        [InlineData(Ff, Ff, "FFFFFFFFFFF", Ff)]
        public async Task AKeyThatIsNoKey_NeverReachesTheChip(string keyA, string keyB, string madKeyA, string madKeyB)
        {
            var reader = new MadPcscReader(new SimulatedClassicChip());

            Assert.Equal(
                ERROR.AuthFailure,
                await reader.WriteMifareClassicWithMAD(Aid, 1, keyA, keyB, keyA, keyB, madKeyA, madKeyB, madKeyA, madKeyB, Pattern(0x10, 16), 0xC1, default, true, true));
            Assert.Equal(
                ERROR.AuthFailure,
                await reader.ReadMifareClassicWithMAD(Aid, keyA, keyB, madKeyA, madKeyB, 16, 0xC1, true, false));
            Assert.Equal(0, reader.Opened);
        }

        [Fact]
        public async Task ANewKeyThatIsNoKey_NeverReachesTheChip()
        {
            var reader = new MadPcscReader(ChipWithApplication());

            Assert.Equal(
                ERROR.AuthFailure,
                await reader.WriteMifareClassicWithMAD(Aid, 0, Ff, Ff, "ZZ", Ff, KeyAText, KeyBText, KeyAText, KeyBText, Pattern(0x10, 16), 0xC1, default, true, true));
            Assert.Equal(0, reader.Opened);
        }

        // ── read ──

        [Fact]
        public async Task AnApplication_IsReadIntoTheDataOfTheReader()
        {
            var chip = ChipWithApplication();
            var reader = new MadPcscReader(chip);

            chip.Poke(4, Pattern(0x10, 16));
            chip.Poke(5, Pattern(0x20, 16));

            var result = await reader.ReadMifareClassicWithMAD(Aid, Ff, Ff, KeyAText, string.Empty, 20, 0xC1, true, false);

            Assert.Equal(ERROR.NoError, result);
            Assert.Equal(Pattern(0x10, 16).Concat(Pattern(0x20, 4)).ToArray(), reader.MifareClassicData);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AnApplicationThatIsNotThere_IsAProtocolConstraint_AndTheDataIsEmpty()
        {
            var reader = new MadPcscReader(ChipWithApplication());

            await reader.ReadMifareClassicWithMAD(Aid, Ff, Ff, KeyAText, string.Empty, 20, 0xC1, true, false);

            Assert.NotNull(reader.MifareClassicData);
            Assert.Equal(ERROR.ProtocolConstraint, await reader.ReadMifareClassicWithMAD(0x4321, Ff, Ff, KeyAText, string.Empty, 20, 0xC1, true, false));
            Assert.Null(reader.MifareClassicData);
        }

        [Fact]
        public async Task AnApplication_IsHandedBackWithTheAnswer_AndEveryCallGetsItsOwn()
        {
            // Two reads at the same time, each of an application of its own: the bytes come back with the answer of the call that read them.
            var chip = new SimulatedClassicChip { YieldsOnEveryCall = true };
            var mad = ClassicMad.Create(ClassicMadVersion.Mad1);

            mad.SetAid(1, Aid);
            mad.SetAid(2, 0x4321);

            var bytes = mad.ToMad1Bytes();

            chip.Poke(1, bytes[..16]);
            chip.Poke(2, bytes[16..32]);
            chip.SetTrailer(0, KeyA, ClassicAccessBits.MadStandard, ClassicMadGpb.Create(ClassicMadVersion.Mad1, true), KeyB);
            chip.Poke(4, Pattern(0x10, 16));
            chip.Poke(8, Pattern(0x80, 16));

            var reader = new MadPcscReader(chip);

            var results = await Task.WhenAll(
                reader.ReadMifareClassicApplicationAsync(Aid, Ff, Ff, KeyAText, string.Empty, 16),
                reader.ReadMifareClassicApplicationAsync(0x4321, Ff, Ff, KeyAText, string.Empty, 16));

            Assert.Equal(ERROR.NoError, results[0].Error);
            Assert.Equal(ERROR.NoError, results[1].Error);
            Assert.Equal(Pattern(0x10, 16), results[0].Data);
            Assert.Equal(Pattern(0x80, 16), results[1].Data);

            // The property of the reader is for the old call only.
            Assert.Null(reader.MifareClassicData);
        }

        [Fact]
        public async Task AnApplicationRead_ThatFails_HandsBackNoData()
        {
            var reader = new MadPcscReader(ChipWithApplication());

            var result = await reader.ReadMifareClassicApplicationAsync(0x4321, Ff, Ff, KeyAText, string.Empty, 16);

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Null(result.Data);
        }

        [Theory]
        [InlineData(0x0003, 16)]
        [InlineData(0x0005, 16)]
        [InlineData(Aid, 0)]
        [InlineData(Aid, -1)]
        public async Task ARead_ThatNeedsNoCardToBeRefused_IsRefused_AndTheChipIsNotSelected(int aid, int length)
        {
            var reader = new MadPcscReader(null);

            Assert.Equal(ERROR.ProtocolConstraint, await reader.ReadMifareClassicWithMAD(aid, Ff, Ff, KeyAText, KeyBText, length, 0xC1, true, false));
            Assert.Equal(ERROR.ProtocolConstraint, (await reader.ReadMifareClassicApplicationAsync(aid, Ff, Ff, KeyAText, KeyBText, length)).Error);
            Assert.Equal(0, reader.Opened);
        }

        [Fact]
        public async Task AWrite_ThatNeedsNoCardToBeRefused_IsRefused_AndTheChipIsNotSelected()
        {
            // No chip on the reader: what the request itself says is wrong is a protocol constraint and not a transport error.
            var reader = new MadPcscReader(null);
            var final = new SectorAccessBits { d_sector_trailer_access_bits = new AccessBits { c1 = 1, c2 = 1, c3 = 1 } };

            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicWithMAD(Aid, 1, Ff, Ff, Ff, Ff, KeyAText, KeyBText, KeyAText, KeyBText, new byte[0], 0xC1, default, true, true));
            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicWithMAD(Aid, 1, Ff, Ff, Ff, Ff, KeyAText, KeyBText, KeyAText, KeyBText, null!, 0xC1, default, true, true));
            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicWithMAD(3, 1, Ff, Ff, Ff, Ff, KeyAText, KeyBText, KeyAText, KeyBText, Pattern(0x10, 16), 0xC1, default, true, true));
            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicWithMAD(Aid, -1, Ff, Ff, Ff, Ff, KeyAText, KeyBText, KeyAText, KeyBText, Pattern(0x10, 16), 0xC1, default, true, true));
            Assert.Equal(ERROR.ProtocolConstraint, await reader.WriteMifareClassicWithMAD(Aid, 1, Ff, Ff, Ff, Ff, KeyAText, KeyBText, KeyAText, KeyBText, Pattern(0x10, 16), 0xC1, final, true, true));
            Assert.Equal(0, reader.Opened);
        }

        [Fact]
        public async Task AnApplicationWrite_IsWrittenAsItWasWhenTheCallStarted_EvenWhenItWaitsForTheGate()
        {
            // A read holds the gate; the write waits for it. The caller changes the array meanwhile: what was checked at the call is what is written.
            var chip = ChipWithApplication();

            chip.YieldsOnEveryCall = true;

            var reader = new MadPcscReader(chip);
            var data = Pattern(0x30, 20);
            var approved = (byte[])data.Clone();

            var read = reader.ReadMifareClassicSectorAsync(1, Ff, Ff);
            var write = reader.WriteMifareClassicWithMAD(Aid, 0, Ff, Ff, Ff, Ff, KeyAText, KeyBText, KeyAText, KeyBText, data, 0xC1, default, true, true);

            Array.Fill(data, (byte)0xEE);

            Assert.Equal(ERROR.NoError, (await read).Error);
            Assert.Equal(ERROR.NoError, await write);
            Assert.Equal(approved[..16], chip.Peek(4));
            Assert.Equal(approved[16..].Concat(new byte[12]).ToArray(), chip.Peek(5));
        }

        [Fact]
        public async Task WrongKeysOfTheMad_AreAnAuthFailure()
        {
            var reader = new MadPcscReader(ChipWithApplication());

            Assert.Equal(ERROR.AuthFailure, await reader.ReadMifareClassicWithMAD(Aid, Ff, Ff, "000000000000", "111111111111", 20, 0xC1, true, false));
        }

        [Fact]
        public async Task WithoutAChip_TheMadCallsAreATransportError()
        {
            var reader = new MadPcscReader(null);

            Assert.Equal(ERROR.TransportError, await reader.ReadMifareClassicWithMAD(Aid, Ff, Ff, KeyAText, KeyBText, 20, 0xC1, true, false));
            Assert.Equal(
                ERROR.TransportError,
                await reader.WriteMifareClassicWithMAD(Aid, 1, Ff, Ff, Ff, Ff, KeyAText, KeyBText, KeyAText, KeyBText, Pattern(0x10, 16), 0xC1, default, true, true));
        }
    }
}
