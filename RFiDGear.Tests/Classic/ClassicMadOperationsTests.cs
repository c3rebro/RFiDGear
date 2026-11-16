using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;
using Xunit;

namespace RFiDGear.Tests.Classic
{
    /// <summary>
    /// The MIFARE Application Directory on top of the Classic operations, against a simulated chip that enforces the access bits: the directory
    /// is read as MAD1 or MAD2, an application is read through it, and one is written in the order data, directory, trailers, with every refusal
    /// made before the first block is written.
    /// </summary>
    public class ClassicMadOperationsTests
    {
        private const string Ff = "FFFFFFFFFFFF";
        private const string KeyAText = "A0A1A2A3A4A5";
        private const string KeyBText = "B0B1B2B3B4B5";
        private const string AppKeyAText = "C0C1C2C3C4C5";
        private const string AppKeyBText = "D0D1D2D3D4D5";
        private const ushort Aid = 0x1234;
        private static readonly byte[] KeyA = Convert.FromHexString(KeyAText);
        private static readonly byte[] KeyB = Convert.FromHexString(KeyBText);
        private static readonly byte[] Ff6 = Enumerable.Repeat((byte)0xFF, 6).ToArray();

        private static ClassicSectorKeys Keys(string? a, string? b)
        {
            Assert.True(ClassicSectorKeys.TryCreate(a, b, out var keys));

            return keys;
        }

        private static ClassicMadOperations Ops(SimulatedClassicChip chip) => new ClassicMadOperations(new ClassicOperations(chip));

        private static byte[] Pattern(byte start, int length) => Enumerable.Range(start, length).Select(i => (byte)i).ToArray();

        // A chip with a directory (MAD1 or MAD2) in its MAD sectors: key A of the MAD is the one of the specification, key B is a known one.
        private static SimulatedClassicChip ChipWithMad(ClassicMadVersion version, ClassicCardSize size, Action<ClassicMad>? setup = null)
        {
            var chip = new SimulatedClassicChip(size);
            var mad = ClassicMad.Create(version);

            setup?.Invoke(mad);
            PokeMad(chip, mad);

            return chip;
        }

        private static void PokeMad(SimulatedClassicChip chip, ClassicMad mad)
        {
            var gpb = ClassicMadGpb.Create(mad.Version, true);
            var one = mad.ToMad1Bytes();

            chip.Poke(1, one[..16]);
            chip.Poke(2, one[16..32]);
            chip.SetTrailer(0, KeyA, ClassicAccessBits.MadStandard, gpb, KeyB);

            if (mad.Version == ClassicMadVersion.Mad2)
            {
                var two = mad.ToMad2Bytes();

                chip.Poke(64, two[..16]);
                chip.Poke(65, two[16..32]);
                chip.Poke(66, two[32..48]);
                chip.SetTrailer(16, KeyA, ClassicAccessBits.MadStandard, gpb, KeyB);
            }
        }

        // The numbers of the blocks that were written, in the order of the writes.
        private static List<int> WrittenBlocks(SimulatedClassicChip chip) =>
            chip.Log.Where(e => e.StartsWith("write ", StringComparison.Ordinal)).Select(e => int.Parse(e.Split(' ')[1])).ToList();

        private static ClassicApplicationWrite Request(
            ushort aid = Aid, int start = 1, byte[]? data = null, bool madKeys = true, ClassicSectorKeys? applicationKeys = null,
            ClassicSectorKeys? newApplicationKeys = null, ClassicAccessBits? bits = null, bool createMad = false, ClassicSectorKeys? newMadKeys = null,
            byte madGpb = 0xC1) =>
            new ClassicApplicationWrite
            {
                Aid = aid,
                StartSector = start,
                Data = data ?? Pattern(0x10, 20),
                MadKeys = madKeys ? Keys(KeyAText, KeyBText) : null,
                ApplicationKeys = applicationKeys ?? Keys(Ff, Ff),
                NewApplicationKeys = newApplicationKeys,
                NewAccessBits = bits,
                CreateMad = createMad,
                NewMadKeys = newMadKeys,
                MadGeneralPurposeByte = madGpb
            };

        // ── the directory ──

        [Fact]
        public async Task AChipWithoutAMad_IsNoMad_AndNothingIsWritten()
        {
            var chip = new SimulatedClassicChip();
            var result = await Ops(chip).ReadMadAsync(Keys(Ff, Ff));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.NoMad, result.Status);
            Assert.Equal(0x69, result.GeneralPurposeByte);
            Assert.Null(result.Mad);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AMad1_IsRead_WithKeyAOnly_AndSector16IsNotAsked()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad =>
            {
                mad.SetAid(1, Aid);
                mad.SetAid(2, Aid);
                mad.SetAid(5, 0x4321);
            });

            var result = await Ops(chip).ReadMadAsync(Keys(KeyAText, null));

            Assert.True(result.Succeeded);
            Assert.Equal(ClassicMadVersion.Mad1, result.Mad!.Version);
            Assert.Equal(new[] { 1, 2 }, result.Mad.FindSectors(Aid));
            Assert.Equal(new[] { 5 }, result.Mad.FindSectors(0x4321));
            Assert.DoesNotContain("login 16 KeyA", chip.Log);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AMad2_IsRead_FromBothSectors()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad2, ClassicCardSize.K4, mad => mad.SetAid(20, Aid));

            var result = await Ops(chip).ReadMadAsync(Keys(KeyAText, null));

            Assert.True(result.Succeeded);
            Assert.Equal(ClassicMadVersion.Mad2, result.Mad!.Version);
            Assert.Equal(new[] { 20 }, result.Mad.FindSectors(Aid));
            Assert.Contains("login 16 KeyA", chip.Log);
        }

        [Fact]
        public async Task ADamagedMad_IsReported_AndNotRepaired()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));
            var block = chip.Peek(1);

            block[3] ^= 0x10;
            chip.Poke(1, block);

            var result = await Ops(chip).ReadMadAsync(Keys(KeyAText, null));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.Damaged, result.Status);
            Assert.NotEqual(ClassicMadProblems.None, result.Mad!.Problems);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AMad2OnAChipWithoutSector16_IsDamaged()
        {
            var chip = new SimulatedClassicChip(ClassicCardSize.K1);
            var mad = ClassicMad.Create(ClassicMadVersion.Mad2);
            var one = mad.ToMad1Bytes();

            chip.Poke(1, one[..16]);
            chip.Poke(2, one[16..32]);
            chip.SetTrailer(0, KeyA, ClassicAccessBits.MadStandard, ClassicMadGpb.Create(ClassicMadVersion.Mad2, true), KeyB);

            var result = await Ops(chip).ReadMadAsync(Keys(KeyAText, null));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.Damaged, result.Status);
        }

        [Fact]
        public async Task WrongKeysOfTheMad_AreAnAuthFailure()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4);
            var result = await Ops(chip).ReadMadAsync(Keys("000000000000", "111111111111"));

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Null(result.Mad);
        }

        // ── an application is read ──

        [Fact]
        public async Task AnApplication_IsRead_FromTheSectorsOfTheDirectory_InOrder()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad =>
            {
                mad.SetAid(1, Aid);
                mad.SetAid(3, Aid);
            });

            for (var i = 0; i < 3; i++)
            {
                chip.Poke(4 + i, Pattern(0x10, 16));
                chip.Poke(12 + i, Pattern(0x80, 16));
            }

            var result = await Ops(chip).ReadApplicationAsync(Aid, 60, Keys(KeyAText, null), Keys(Ff, Ff));

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { 1, 3 }, result.Sectors);
            Assert.Equal(Enumerable.Repeat(Pattern(0x10, 16), 3).SelectMany(b => b).Concat(Pattern(0x80, 12)).ToArray(), result.Data);
        }

        [Fact]
        public async Task AnApplicationRead_StopsWhenItHasEnough()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad =>
            {
                mad.SetAid(1, Aid);
                mad.SetAid(3, Aid);
            });

            var result = await Ops(chip).ReadApplicationAsync(Aid, 20, Keys(KeyAText, null), Keys(Ff, Ff));

            Assert.True(result.Succeeded);
            Assert.Equal(20, result.Data!.Length);
            Assert.Contains("login 1 KeyA", chip.Log);
            Assert.DoesNotContain("login 3 KeyA", chip.Log);
        }

        [Fact]
        public async Task AnApplicationThatIsNotListed_IsNotFound()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, 0x4321));
            var result = await Ops(chip).ReadApplicationAsync(Aid, 16, Keys(KeyAText, null), Keys(Ff, Ff));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.NotFound, result.Status);
            Assert.Null(result.Data);
        }

        [Fact]
        public async Task AnApplicationThatHoldsLessThanAskedFor_IsTooShort_AndGivesNoData()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad =>
            {
                mad.SetAid(1, Aid);
                mad.SetAid(2, Aid);
            });

            var result = await Ops(chip).ReadApplicationAsync(Aid, 97, Keys(KeyAText, null), Keys(Ff, Ff));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.TooShort, result.Status);
            Assert.Null(result.Data);
        }

        [Theory]
        [InlineData(0x0003, 16)]
        [InlineData(0x0005, 16)]
        [InlineData(0x1234, 0)]
        public async Task ARequestThatIsNoRequest_IsRefused_AndTheChipIsNotAsked(ushort aid, int length)
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4);
            var result = await Ops(chip).ReadApplicationAsync(aid, length, Keys(KeyAText, null), Keys(Ff, Ff));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.WrongRequest, result.Status);
            Assert.Empty(chip.Log);
        }

        [Fact]
        public async Task ARead_WithKeysThatHoldNoKey_IsRefused_AndTheChipIsNotAsked()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));

            var withoutMadKeys = await Ops(chip).ReadApplicationAsync(Aid, 16, Keys(null, null), Keys(Ff, Ff));
            var withoutApplicationKeys = await Ops(chip).ReadApplicationAsync(Aid, 16, Keys(KeyAText, null), Keys(null, null));

            Assert.Equal(ClassicMadStatus.WrongRequest, withoutMadKeys.Status);
            Assert.Equal(ClassicMadStatus.WrongRequest, withoutApplicationKeys.Status);
            Assert.Empty(chip.Log);
        }

        [Fact]
        public async Task AnApplicationSectorThatTheKeysDoNotOpen_IsAnAuthFailure()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));

            chip.SetTrailer(1, KeyA, ClassicAccessBits.Transport, 0x00, KeyB);

            var result = await Ops(chip).ReadApplicationAsync(Aid, 16, Keys(KeyAText, null), Keys(Ff, Ff));

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Null(result.Data);
        }

        // ── an application is written ──

        [Fact]
        public async Task AnApplication_IsWrittenOnAChipWithoutAMad_AndTheMadIsMade()
        {
            var chip = new SimulatedClassicChip();
            var data = Pattern(0x10, 20);

            var result = await Ops(chip).WriteApplicationAsync(Request(data: data, createMad: true, newMadKeys: Keys(KeyAText, KeyBText)).WithMadKeys(Keys(Ff, Ff)));

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { 1 }, result.Sectors);

            // The data is there exactly as given; the rest of the sector stays as it was.
            Assert.Equal(data[..16], chip.Peek(4));
            Assert.Equal(data[16..].Concat(new byte[12]).ToArray(), chip.Peek(5));
            Assert.Equal(new byte[16], chip.Peek(6));

            // The directory is there, as MAD2 on a chip that has the sector for it, and the trailers are the standard ones.
            var read = await Ops(chip).ReadMadAsync(Keys(KeyAText, null));

            Assert.True(read.Succeeded);
            Assert.Equal(ClassicMadVersion.Mad2, read.Mad!.Version);
            Assert.Equal(new[] { 1 }, read.Mad.FindSectors(Aid));
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0xC2, KeyB), chip.Peek(3));
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0xC2, KeyB), chip.Peek(67));

            // Data first, then the directory, then the trailers.
            var writes = WrittenBlocks(chip);

            Assert.True(writes.IndexOf(4) < writes.IndexOf(1));
            Assert.True(writes.IndexOf(66) < writes.IndexOf(3));
            Assert.Equal(new[] { 3, 67 }, writes.TakeLast(2));
        }

        [Fact]
        public async Task AMadThatIsMadeOnA1K_IsMad1_AndSector16IsNotTouched()
        {
            var chip = new SimulatedClassicChip(ClassicCardSize.K1);
            var request = Request(createMad: true, newMadKeys: Keys(KeyAText, KeyBText)).WithMadKeys(Keys(Ff, Ff));

            var result = await Ops(chip).WriteApplicationAsync(request);

            Assert.True(result.Succeeded);
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0xC1, KeyB), chip.Peek(3));
            Assert.DoesNotContain(chip.Log, e => e.Contains("16", StringComparison.Ordinal) && e.StartsWith("login", StringComparison.Ordinal));
            Assert.Equal(new[] { 1 }, (await Ops(chip).ReadMadAsync(Keys(KeyAText, null))).Mad!.FindSectors(Aid));
        }

        [Fact]
        public async Task AGeneralPurposeByteThatFitsTheVersionOfTheMad_IsKept()
        {
            // 81: a MAD1 for one application (the multi-application bit is clear): the byte of the request, not the default C1.
            var chip = new SimulatedClassicChip(ClassicCardSize.K1);
            var request = Request(createMad: true, newMadKeys: Keys(KeyAText, KeyBText), madGpb: 0x81).WithMadKeys(Keys(Ff, Ff));

            Assert.True((await Ops(chip).WriteApplicationAsync(request)).Succeeded);
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0x81, KeyB), chip.Peek(3));
        }

        [Theory]
        [InlineData(0xC2)]
        [InlineData(0x00)]
        [InlineData(0x69)]
        [InlineData(0x41)]
        public async Task AGeneralPurposeByteThatIsNotOfTheVersionOfTheMad_IsReplaced(byte madGpb)
        {
            // MAD2 and no MAD byte at all on a chip that gets a MAD1.
            var chip = new SimulatedClassicChip(ClassicCardSize.K1);
            var request = Request(createMad: true, newMadKeys: Keys(KeyAText, KeyBText), madGpb: madGpb).WithMadKeys(Keys(Ff, Ff));

            Assert.True((await Ops(chip).WriteApplicationAsync(request)).Succeeded);
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(KeyA, ClassicMadGpb.Create(ClassicMadVersion.Mad1, true), KeyB), chip.Peek(3));
        }

        [Fact]
        public async Task AChipWithoutAMad_IsLeftAlone_WhenTheRequestDoesNotAllowToMakeOne()
        {
            var chip = new SimulatedClassicChip();
            var result = await Ops(chip).WriteApplicationAsync(Request().WithMadKeys(Keys(Ff, Ff)));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.NoMad, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AnApplication_IsWrittenInTheSectorsTheMadListsForIt_AndWhatLiesBehindTheDataStays()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad =>
            {
                mad.SetAid(1, Aid);
                mad.SetAid(2, Aid);
            });

            var old = Pattern(0xA0, 16);
            var data = Pattern(0x10, 20);

            chip.Poke(5, old);
            chip.Poke(6, old);

            var result = await Ops(chip).WriteApplicationAsync(Request(start: 0, data: data));

            // The data reaches sector 1 only: sector 2 of the application is left alone.
            Assert.True(result.Succeeded);
            Assert.Equal(new[] { 1 }, result.Sectors);
            Assert.Equal(data[..16], chip.Peek(4));
            Assert.Equal(data[16..].Concat(old[4..]).ToArray(), chip.Peek(5));
            Assert.Equal(old, chip.Peek(6));

            // The directory is there already and says the same: neither it nor a trailer is written.
            Assert.All(WrittenBlocks(chip), block => Assert.InRange(block, 4, 6));
        }

        [Fact]
        public async Task AnApplication_IsWrittenAsItWasWhenTheCallStarted_WhateverTheCallerDoesToTheArrayMeanwhile()
        {
            // The chip gives up the thread on every call, so the caller gets to change its array while the call waits for the chip.
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));

            chip.YieldsOnEveryCall = true;

            var data = Pattern(0x10, 20);
            var approved = (byte[])data.Clone();
            var write = Ops(chip).WriteApplicationAsync(Request(start: 0, data: data));

            Array.Fill(data, (byte)0xEE);

            var result = await write;

            Assert.True(result.Succeeded);
            Assert.Equal(approved[..16], chip.Peek(4));
            Assert.Equal(approved[16..].Concat(new byte[12]).ToArray(), chip.Peek(5));
        }

        [Fact]
        public async Task ADamagedMad_IsNotReplaced_EvenWhenTheRequestAllowsToMakeOne()
        {
            // A MAD that has a wrong CRC is damaged, not missing: nothing is written over it.
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, 0x4321));
            var block = chip.Peek(1);

            block[3] ^= 0x10;
            chip.Poke(1, block);

            var result = await Ops(chip).WriteApplicationAsync(Request(start: 5, createMad: true, newMadKeys: Keys(KeyAText, KeyBText)));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.Damaged, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AListedApplicationThatHoldsLessThanTheData_IsRefused_AndNothingIsWritten()
        {
            // Sector 1 is all that the directory lists for the AID: 48 bytes. 60 bytes do not fit, and the first 48 are not written either.
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));
            var result = await Ops(chip).WriteApplicationAsync(Request(start: 0, data: Pattern(0x10, 60)));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.NoRoom, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ASectorThatTheDirectoryCoversButTheChipDoesNotHave_IsNoRoom()
        {
            // A Mini has the sectors 0 to 4; a MAD1 covers the sectors 1 to 15. From sector 4, 100 bytes need sectors 5 and 6 as well.
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.Mini);
            var result = await Ops(chip).WriteApplicationAsync(Request(start: 4, data: Pattern(0x10, 100)));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.NoRoom, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AnApplicationThatIsNotListed_IsNotWrittenWhenNoStartSectorIsGiven()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4);
            var result = await Ops(chip).WriteApplicationAsync(Request(start: 0));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.NotFound, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AnApplication_IsAddedToAnExistingDirectory_WithoutTouchingTheOthers()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(5, 0x4321));
            var result = await Ops(chip).WriteApplicationAsync(Request(data: Pattern(0x10, 60)));

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { 1, 2 }, result.Sectors);

            var read = await Ops(chip).ReadMadAsync(Keys(KeyAText, null));

            Assert.Equal(new[] { 1, 2 }, read.Mad!.FindSectors(Aid));
            Assert.Equal(new[] { 5 }, read.Mad.FindSectors(0x4321));

            // Only a block of the directory and the data were written, no trailer.
            Assert.DoesNotContain(3, WrittenBlocks(chip));
        }

        [Fact]
        public async Task ASectorOfAnotherApplication_IsNotTaken_AndNothingIsWritten()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(2, 0x4321));
            var result = await Ops(chip).WriteApplicationAsync(Request(data: Pattern(0x10, 60)));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.SectorTaken, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Theory]
        [InlineData(ClassicCardSize.K1, ClassicMadVersion.Mad1)]
        [InlineData(ClassicCardSize.K4, ClassicMadVersion.Mad1)]
        public async Task AWriteThatDoesNotFit_IsRefusedBeforeTheFirstBlock(ClassicCardSize size, ClassicMadVersion version)
        {
            // From sector 15 on, 100 bytes need three sectors: a 1K and a MAD1 stop after sector 15.
            var chip = ChipWithMad(version, size);
            var result = await Ops(chip).WriteApplicationAsync(Request(start: 15, data: Pattern(0x10, 100)));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.NoRoom, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task TheSectorOfMad2_IsSkipped_AndTheDirectoryStaysValid()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad2, ClassicCardSize.K4);
            var result = await Ops(chip).WriteApplicationAsync(Request(start: 15, data: Pattern(0x10, 120)));

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { 15, 17, 18 }, result.Sectors);

            var read = await Ops(chip).ReadMadAsync(Keys(KeyAText, null));

            Assert.True(read.Succeeded);
            Assert.Equal(new[] { 15, 17, 18 }, read.Mad!.FindSectors(Aid));
            Assert.Equal(Pattern(0x10, 16), chip.Peek(60));

            // 120 bytes: 48 in sector 15, 48 in sector 17 (blocks 68 to 70), and 24 in sector 18 (72 and the start of 73).
            Assert.Equal(Pattern(0x40, 16), chip.Peek(68));
            Assert.Equal(Pattern(0x70, 16), chip.Peek(72));
            Assert.Equal(Pattern(0x80, 8).Concat(new byte[8]).ToArray(), chip.Peek(73));
        }

        [Fact]
        public async Task ABlockThatTheChipRefuses_EndsTheCall_AndNoDirectoryEntryIsWrittenBeforeTheData()
        {
            var chip = new SimulatedClassicChip();

            chip.FailNextWriteOf(5);

            var result = await Ops(chip).WriteApplicationAsync(Request(createMad: true, newMadKeys: Keys(KeyAText, KeyBText)).WithMadKeys(Keys(Ff, Ff)));

            Assert.False(result.Succeeded);
            Assert.Equal(5, result.FailedBlock);
            Assert.DoesNotContain(WrittenBlocks(chip), block => block is 1 or 2 or 3 or 64 or 65 or 66 or 67);
            Assert.Equal(chip.Peek(3), ClassicAccessBits.Transport.BuildTrailer(Ff6, 0x69, Ff6));
        }

        // ── what is refused before anything is written ──

        [Fact]
        public async Task ARequestWithoutData_OrWithAnAdministrationCode_OrWithoutKeys_IsRefused_AndTheChipIsNotAsked()
        {
            var requests = new[]
            {
                Request(data: Array.Empty<byte>()),
                Request(aid: 0x0005),
                Request(start: -1),
                Request(madKeys: false),

                // No keys for the sectors of the application.
                new ClassicApplicationWrite { Aid = Aid, StartSector = 1, Data = Pattern(0x10, 20), MadKeys = Keys(KeyAText, KeyBText) },

                // Keys that hold no key at all (for the sectors of the application, and for the MAD sectors).
                Request(applicationKeys: Keys(null, null)),
                new ClassicApplicationWrite { Aid = Aid, StartSector = 1, Data = Pattern(0x10, 20), MadKeys = Keys(null, null), ApplicationKeys = Keys(Ff, Ff) },
                Request(bits: new ClassicAccessBits(0, 0, 0, 7))
            };

            foreach (var request in requests)
            {
                var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4);
                var result = await Ops(chip).WriteApplicationAsync(request);

                Assert.Equal(ERROR.ProtocolConstraint, result.Error);
                Assert.Equal(ClassicMadStatus.WrongRequest, result.Status);
                Assert.Empty(chip.Log);
            }
        }

        [Fact]
        public async Task NewKeysThatLackAKey_AreFoundBeforeTheFirstBlock()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));

            // Key B is neither new nor given as the current one: the trailer cannot be built, and the data must not be written either.
            var result = await Ops(chip).WriteApplicationAsync(Request(applicationKeys: Keys(Ff, null), newApplicationKeys: Keys(AppKeyAText, null)));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.KeysMissing, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ADirectoryThatIsMadeWithoutAKeyThatNothingGives_IsFoundBeforeTheFirstBlock()
        {
            // Key B is neither new nor one of the current keys: the trailer of the MAD sectors cannot be built.
            var chip = new SimulatedClassicChip();

            var result = await Ops(chip).WriteApplicationAsync(Request(createMad: true, newMadKeys: Keys(KeyAText, null)).WithMadKeys(Keys(Ff, null)));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.KeysMissing, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ADirectoryThatIsMadeWithOneNewKey_KeepsTheCurrentKeyOfTheOtherOne()
        {
            var chip = new SimulatedClassicChip();

            var result = await Ops(chip).WriteApplicationAsync(Request(createMad: true, newMadKeys: Keys(KeyAText, null)).WithMadKeys(Keys(Ff, Ff)));

            Assert.True(result.Succeeded);
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0xC2, Ff6), chip.Peek(3));
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0xC2, Ff6), chip.Peek(67));
        }

        [Fact]
        public async Task OneKeyOfAnExistingMad_CanChangeWhileTheOtherStays()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));
            var newKey = Convert.FromHexString("010203040506");

            var result = await Ops(chip).WriteApplicationAsync(Request(newMadKeys: Keys("010203040506", null)));

            Assert.True(result.Succeeded);
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(newKey, ClassicMadGpb.Create(ClassicMadVersion.Mad1, true), KeyB), chip.Peek(3));
        }

        [Theory]
        [InlineData(0xC3)]
        [InlineData(0xC0)]
        [InlineData(0x83)]
        public async Task ADirectoryThatTheChipAnnouncesAndThatIsNoDirectoryOfAKnownVersion_IsDamaged_AndNotReplaced(byte gpb)
        {
            // The DA bit says that there is a MAD. A version that is none (or a bit that is reserved) is a damaged one, which nothing replaces.
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(0, KeyA, ClassicAccessBits.MadStandard, gpb, KeyB);

            var read = await Ops(chip).ReadMadAsync(Keys(KeyAText, null));
            var write = await Ops(chip).WriteApplicationAsync(Request(createMad: true, newMadKeys: Keys(KeyAText, KeyBText)));

            Assert.Equal(ClassicMadStatus.Damaged, read.Status);
            Assert.Equal(ClassicMadStatus.Damaged, write.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AGeneralPurposeByteWithoutTheDirectoryBit_IsNoMad_WhateverElseItSays()
        {
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(0, KeyA, ClassicAccessBits.MadStandard, 0x41, KeyB);

            var read = await Ops(chip).ReadMadAsync(Keys(KeyAText, null));

            Assert.Equal(ClassicMadStatus.NoMad, read.Status);
        }

        [Fact]
        public async Task ADirectoryThatListsASectorTheChipDoesNotHave_IsNoRoom_AndNothingIsWritten()
        {
            // A Mini has the sectors 0 to 4. The directory lists the sectors 4 and 5 for the AID: 60 bytes reach sector 5, and not a block is written before that is found out.
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.Mini, mad =>
            {
                mad.SetAid(4, Aid);
                mad.SetAid(5, Aid);
            });

            var result = await Ops(chip).WriteApplicationAsync(Request(start: 0, data: Pattern(0x10, 60)));

            Assert.Equal(ERROR.ProtocolConstraint, result.Error);
            Assert.Equal(ClassicMadStatus.NoRoom, result.Status);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AnApplicationRead_NeedsOnlyTheBlocksThatTheBytesReach()
        {
            // Block 6 (the third of sector 1) is unreadable for every key. 32 bytes are the blocks 4 and 5; 33 reach block 6.
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));

            chip.SetTrailer(1, Ff6, new ClassicAccessBits(0, 0, 7, 4), 0x69, Ff6);

            var enough = await Ops(chip).ReadApplicationAsync(Aid, 32, Keys(KeyAText, null), Keys(Ff, null));
            var tooMuch = await Ops(chip).ReadApplicationAsync(Aid, 33, Keys(KeyAText, null), Keys(Ff, null));

            Assert.True(enough.Succeeded);
            Assert.Equal(32, enough.Data!.Length);
            Assert.Equal(ERROR.PermissionDenied, tooMuch.Error);
            Assert.Null(tooMuch.Data);
        }

        [Fact]
        public async Task AShortWrite_WritesOnlyTheBlocksThatTheDataReaches()
        {
            // 16 bytes reach block 4. Block 6 cannot be written by any key, and it is none of this write's business.
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));

            chip.SetTrailer(1, Ff6, new ClassicAccessBits(0, 0, 7, 4), 0x69, Ff6);

            var data = Pattern(0x40, 16);
            var result = await Ops(chip).WriteApplicationAsync(Request(start: 0, data: data));

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { 4 }, WrittenBlocks(chip));
            Assert.Equal(data, chip.Peek(4));
        }

        [Fact]
        public async Task AShortWrite_WhoseLastBlockCannotBeReadWithTheKeys_IsAnAuthFailure_WhenTheKeyIsWrong()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));

            // 20 bytes: the second block is only partly covered and has to be read first, with a key that the sector does not take.
            var result = await Ops(chip).WriteApplicationAsync(Request(start: 0, data: Pattern(0x10, 20), applicationKeys: Keys("111111111111", null)));

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task AShortWrite_WhoseLastBlockCannotBeReadByAnyKeyThatLoggedIn_IsPermissionDenied()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));

            // Block 5 is unreadable for every key: the key logs in, and the block that has to be merged cannot be shown.
            chip.SetTrailer(1, Ff6, new ClassicAccessBits(0, 7, 0, 4), 0x69, Ff6);

            var result = await Ops(chip).WriteApplicationAsync(Request(start: 0, data: Pattern(0x10, 20)));

            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(0, chip.Writes);
        }

        // ── the keys and the access bits of the application ──

        [Fact]
        public async Task NewKeys_ReplaceTheKeysOfEverySector_AndTheAccessBitsAndTheByteStay()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad =>
            {
                mad.SetAid(1, Aid);
                mad.SetAid(2, Aid);
            });

            // 60 bytes reach both sectors, and the keys go into the trailers of the sectors that the data reaches.
            var result = await Ops(chip).WriteApplicationAsync(Request(start: 0, data: Pattern(0x10, 60), newApplicationKeys: Keys(AppKeyAText, AppKeyBText)));

            Assert.True(result.Succeeded);

            var expected = ClassicAccessBits.Transport.BuildTrailer(Convert.FromHexString(AppKeyAText), 0x69, Convert.FromHexString(AppKeyBText));

            Assert.Equal(expected, chip.Peek(7));
            Assert.Equal(expected, chip.Peek(11));

            // The trailers are the last that is written.
            Assert.Equal(new[] { 7, 11 }, WrittenBlocks(chip).TakeLast(2));
            Assert.Equal(Pattern(0x10, 16), (await Ops(chip).ReadApplicationAsync(Aid, 16, Keys(KeyAText, null), Keys(AppKeyAText, null))).Data);
        }

        [Fact]
        public async Task NewAccessBits_AreWrittenToEverySector_WithTheKeysThatItHas()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad1, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));
            var bits = new ClassicAccessBits(0, 0, 0, 6);

            var result = await Ops(chip).WriteApplicationAsync(Request(bits: bits));

            Assert.True(result.Succeeded);
            Assert.Equal(bits.BuildTrailer(Ff6, 0x69, Ff6), chip.Peek(7));
        }

        [Fact]
        public async Task AMadThatIsRekeyed_GetsTheNewKeysInItsTrailers_AndKeepsItsByte()
        {
            var chip = ChipWithMad(ClassicMadVersion.Mad2, ClassicCardSize.K4, mad => mad.SetAid(1, Aid));
            var newKey = Convert.FromHexString("010203040506");

            var result = await Ops(chip).WriteApplicationAsync(Request(newMadKeys: Keys("010203040506", "0A0B0C0D0E0F")));

            Assert.True(result.Succeeded);
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(newKey, 0xC2, Convert.FromHexString("0A0B0C0D0E0F")), chip.Peek(3));
            Assert.Equal(ClassicAccessBits.MadStandard.BuildTrailer(newKey, 0xC2, Convert.FromHexString("0A0B0C0D0E0F")), chip.Peek(67));
        }
    }

    internal static class ClassicApplicationWriteExtensions
    {
        // A copy of the request with other keys for the MAD sectors (the request builder of the tests holds the ones of a chip with a directory).
        public static ClassicApplicationWrite WithMadKeys(this ClassicApplicationWrite request, ClassicSectorKeys madKeys) =>
            new ClassicApplicationWrite
            {
                Aid = request.Aid,
                StartSector = request.StartSector,
                Data = request.Data,
                MadKeys = madKeys,
                ApplicationKeys = request.ApplicationKeys,
                NewApplicationKeys = request.NewApplicationKeys,
                NewAccessBits = request.NewAccessBits,
                CreateMad = request.CreateMad,
                NewMadKeys = request.NewMadKeys,
                MadGeneralPurposeByte = request.MadGeneralPurposeByte
            };
    }
}
