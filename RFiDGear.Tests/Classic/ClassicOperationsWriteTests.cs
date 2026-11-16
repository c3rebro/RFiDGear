using System;
using System.Linq;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;
using Xunit;

namespace RFiDGear.Tests.Classic
{
    /// <summary>
    /// The write side of the Classic operations, against a simulated chip that enforces the access bits as the data sheet says: which key
    /// writes, one attempt and no repeat, a read back of every block, and nothing at all for what the policy refuses.
    /// </summary>
    public class ClassicOperationsWriteTests
    {
        private const string Ff = "FFFFFFFFFFFF";
        private const string KeyAText = "A0A1A2A3A4A5";
        private const string KeyBText = "B0B1B2B3B4B5";
        private static readonly byte[] Ff6 = Enumerable.Repeat((byte)0xFF, 6).ToArray();
        private static readonly byte[] KeyA = Convert.FromHexString(KeyAText);
        private static readonly byte[] KeyB = Convert.FromHexString(KeyBText);

        private static ClassicSectorKeys Keys(string? a, string? b)
        {
            Assert.True(ClassicSectorKeys.TryCreate(a, b, out var keys));

            return keys;
        }

        private static byte[] Pattern(byte start, int length = 16) => Enumerable.Range(start, length).Select(i => (byte)i).ToArray();

        // ── a data block ──

        [Fact]
        public async Task ADataBlock_IsWrittenWithKeyA_AndReadBack()
        {
            var chip = new SimulatedClassicChip();
            var data = Pattern(0x10);

            var result = await new ClassicOperations(chip).WriteBlockAsync(5, data, Keys(Ff, Ff), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(1, result.BlocksWritten);
            Assert.Null(result.FailedBlock);
            Assert.Equal(data, chip.Peek(5));
            Assert.Equal(
                new[] { "login 1 KeyA", "read 7", "write 5 (16 bytes)", "read 5" },
                chip.Log);
        }

        [Fact]
        public async Task ABlockThatOnlyKeyBMayWrite_IsWrittenWithKeyB()
        {
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(3, KeyA, ClassicAccessBits.MadStandard, 0xC1, KeyB);

            var data = Pattern(0x20);
            var result = await new ClassicOperations(chip).WriteBlockAsync(12, data, Keys(KeyAText, KeyBText), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(data, chip.Peek(12));
            Assert.Equal(
                new[] { "login 3 KeyA", "read 15", "login 3 KeyB", "read 15", "write 12 (16 bytes)", "read 12" },
                chip.Log);
        }

        [Fact]
        public async Task ABlockThatOnlyKeyBMayWrite_IsRefusedWithoutKeyB_AndNothingIsWritten()
        {
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(3, KeyA, ClassicAccessBits.MadStandard, 0xC1, KeyB);

            var result = await new ClassicOperations(chip).WriteBlockAsync(12, Pattern(0x20), Keys(KeyAText, null), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(12, result.FailedBlock);
            Assert.Equal(0, chip.Writes);
            Assert.Equal(new byte[16], chip.Peek(12));
        }

        [Fact]
        public async Task ABlockThatKeyBMayWriteButCannotLogInWith_IsNotWrittenWithKeyB()
        {
            // Factory sector: both keys may write, but key B is data there. Only key B was given: it cannot log in, and nothing is written.
            var chip = new SimulatedClassicChip();
            var result = await new ClassicOperations(chip).WriteBlockAsync(5, Pattern(0x10), Keys(null, Ff), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ABlockThatNobodyMayWrite_IsPermissionDenied_AndNothingIsWritten()
        {
            var chip = new SimulatedClassicChip();

            // 010: both keys read, nobody writes.
            chip.SetTrailer(1, KeyA, new ClassicAccessBits(2, 2, 2, 6), 0x00, KeyB);

            var result = await new ClassicOperations(chip).WriteBlockAsync(5, Pattern(0x10), Keys(KeyAText, KeyBText), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(0, chip.Writes);
            Assert.DoesNotContain(chip.Log, entry => entry.StartsWith("write", StringComparison.Ordinal));
        }

        [Fact]
        public async Task WrongKeys_AreAnAuthFailure_AndNothingIsWritten()
        {
            var chip = new SimulatedClassicChip();
            var result = await new ClassicOperations(chip).WriteBlockAsync(5, Pattern(0x10), Keys("000000000000", "111111111111"), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ASectorWhoseAccessBitsAreBroken_IsNotWritten()
        {
            // The chip does not let any key in when the bits are broken, so the sector is not even entered.
            var chip = new SimulatedClassicChip();
            var trailer = chip.Peek(7);

            trailer[7] ^= 0xFF;
            chip.Poke(7, trailer);

            var result = await new ClassicOperations(chip).WriteBlockAsync(5, Pattern(0x10), Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.False(result.Succeeded);
            Assert.Equal(0, chip.Writes);
        }

        // ── what the policy refuses touches no chip ──

        [Fact]
        public async Task WhatThePolicyRefuses_NeverReachesTheChip()
        {
            var chip = new SimulatedClassicChip();
            var ops = new ClassicOperations(chip);
            var keys = Keys(Ff, Ff);

            var noData = await ops.WriteBlockAsync(5, null!, keys, ClassicWriteOptions.DataOnly);
            var shortData = await ops.WriteBlockAsync(5, new byte[15], keys, ClassicWriteOptions.DataOnly);
            var manufacturer = await ops.WriteBlockAsync(0, Pattern(0), keys, ClassicWriteOptions.DataOnly);
            var trailer = await ops.WriteBlockAsync(7, ClassicAccessBits.Transport.BuildTrailer(Ff6, 0x69, Ff6), keys, ClassicWriteOptions.DataOnly);
            var outside = await ops.WriteBlockAsync(256, Pattern(0), keys, ClassicWriteOptions.DataOnly);

            Assert.All(new[] { noData, shortData, manufacturer, trailer, outside }, r => Assert.Equal(ERROR.ProtocolConstraint, r.Error));
            Assert.Equal(
                new[]
                {
                    ClassicWriteViolation.WrongLength, ClassicWriteViolation.WrongLength, ClassicWriteViolation.ManufacturerBlock,
                    ClassicWriteViolation.TrailerNotAllowed, ClassicWriteViolation.OutsideTheChip
                },
                new[] { noData, shortData, manufacturer, trailer, outside }.Select(r => r.Violation));
            Assert.Empty(chip.Log);
        }

        [Fact]
        public async Task TheManufacturerBlock_IsWrittenOnAChipThatAllowsIt_WhenTheOptionSaysSo()
        {
            var chip = new SimulatedClassicChip { Block0IsWritable = true };
            var data = Pattern(0x80);

            var result = await new ClassicOperations(chip).WriteBlockAsync(0, data, Keys(Ff, null), new ClassicWriteOptions { AllowManufacturerBlock = true });

            Assert.True(result.Succeeded);
            Assert.Equal(data, chip.Peek(0));
        }

        [Fact]
        public async Task TheManufacturerBlock_OfAGenuineChip_IsRefusedByTheChip_AndTheCallEndsThere()
        {
            var chip = new SimulatedClassicChip();
            var before = chip.Peek(0);

            var result = await new ClassicOperations(chip).WriteBlockAsync(0, Pattern(0x80), Keys(Ff, null), new ClassicWriteOptions { AllowManufacturerBlock = true });

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Equal(before, chip.Peek(0));
            Assert.Equal(1, chip.Writes);
        }

        // ── one attempt ──

        [Fact]
        public async Task ARefusedWrite_IsNeverRepeated()
        {
            var chip = new SimulatedClassicChip();

            chip.FailNextWriteOf(5);

            var result = await new ClassicOperations(chip).WriteBlockAsync(5, Pattern(0x10), Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Equal(5, result.FailedBlock);
            Assert.Equal(1, chip.Writes);
            Assert.Equal(new byte[16], chip.Peek(5));

            // The one thing that is done about it is a look at the chip.
            Assert.Equal("reselect", chip.Log[^1]);
        }

        [Fact]
        public async Task AChipThatIsGoneBeforeTheWrite_IsATransportError_AndIsNotLookedForAgain()
        {
            var chip = new SimulatedClassicChip();
            var ops = new ClassicOperations(new GoneBeforeTheWrite(chip));

            var result = await ops.WriteBlockAsync(5, Pattern(0x10), Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(0, chip.Writes);
            Assert.Equal(0, chip.Reselects);
        }

        [Fact]
        public async Task AChipThatIsGoneAfterTheWrite_IsATransportError_AndTheWriteIsNotRepeated()
        {
            var chip = new SimulatedClassicChip { GoesAwayAfterAWrite = true };
            var result = await new ClassicOperations(chip).WriteBlockAsync(5, Pattern(0x10), Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(1, chip.Writes);
        }

        [Fact]
        public async Task ABlockThatDoesNotReadBackAsWritten_IsReported()
        {
            var chip = new SimulatedClassicChip { CorruptsWrites = true };
            var result = await new ClassicOperations(chip).WriteBlockAsync(5, Pattern(0x10), Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.Unknown, result.Error);
            Assert.Equal(5, result.FailedBlock);
            Assert.Equal(0, result.BlocksWritten);
            Assert.Equal(1, chip.Writes);
        }

        // ── a trailer ──

        [Fact]
        public async Task ATrailer_IsWrittenWithKeyA_AndReadBackWithTheNewKey()
        {
            var chip = new SimulatedClassicChip();
            var trailer = ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0xC1, KeyB);

            var result = await new ClassicOperations(chip).WriteBlockAsync(19, trailer, Keys(Ff, null), new ClassicWriteOptions { AllowTrailer = true });

            Assert.True(result.Succeeded);
            Assert.Equal(trailer, chip.Peek(19));

            // The old key A wrote it; the new key A logs in to read it back.
            Assert.Equal(
                new[] { "login 4 KeyA", "read 19", "write 19 (16 bytes)", "login 4 KeyA", "read 19" },
                chip.Log);

            // The sector is read with the new keys now, and with the old ones no longer.
            Assert.True((await new ClassicOperations(chip).ReadSectorAsync(4, Keys(KeyAText, KeyBText))).IsAuthenticated);
            Assert.False((await new ClassicOperations(chip).ReadSectorAsync(4, Keys(Ff, null))).IsAuthenticated);
        }

        [Fact]
        public async Task ATrailerWithBitsThatAreNotConsistent_IsNeverWritten()
        {
            var chip = new SimulatedClassicChip();
            var trailer = ClassicAccessBits.Transport.BuildTrailer(Ff6, 0x69, Ff6);

            trailer[6] ^= 0x10;

            var everything = new ClassicWriteOptions { AllowTrailer = true, AllowFinalAccessBits = true, AllowManufacturerBlock = true };
            var result = await new ClassicOperations(chip).WriteBlockAsync(7, trailer, Keys(Ff, null), everything);

            Assert.Equal(ClassicWriteViolation.AccessBitsNotConsistent, result.Violation);
            Assert.Empty(chip.Log);
        }

        [Fact]
        public async Task AccessBitsThatCanNeverBeChangedAgain_AreOnlyWrittenWithTheSecondOption()
        {
            var chip = new SimulatedClassicChip();
            var final = new ClassicAccessBits(0, 0, 0, 1).BuildTrailer(Ff6, 0x69, Ff6);

            var refused = await new ClassicOperations(chip).WriteBlockAsync(7, final, Keys(Ff, null), new ClassicWriteOptions { AllowTrailer = true });

            Assert.Equal(ClassicWriteViolation.AccessBitsAreFinal, refused.Violation);
            Assert.Empty(chip.Log);

            var written = await new ClassicOperations(chip).WriteBlockAsync(7, final, Keys(Ff, null), new ClassicWriteOptions { AllowTrailer = true, AllowFinalAccessBits = true });

            Assert.True(written.Succeeded);
            Assert.Equal(final, chip.Peek(7));
        }

        [Fact]
        public async Task TheKeysOfASectorWhoseAccessBitsAreFinal_AreChangedWithKeyB_WhenTheBitsStayTheSame()
        {
            // 100: key A is written by key B, the access bits by nobody, key B by key B. Changing the keys and leaving the bits as they are needs key B.
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(2, KeyA, new ClassicAccessBits(1, 1, 1, 1), 0x00, KeyB);

            var current = chip.Peek(11);
            var next = new byte[16];

            Array.Copy(Pattern(0x30, 6), next, 6);
            Array.Copy(current, 6, next, 6, 4);
            Array.Copy(Pattern(0x40, 6), 0, next, 10, 6);

            var withKeyA = await new ClassicOperations(chip).WriteBlockAsync(11, next, Keys(KeyAText, null), new ClassicWriteOptions { AllowTrailer = true, AllowFinalAccessBits = true });

            Assert.Equal(ERROR.PermissionDenied, withKeyA.Error);
            Assert.Equal(0, chip.Writes);

            var withKeyB = await new ClassicOperations(chip).WriteBlockAsync(11, next, Keys(KeyAText, KeyBText), new ClassicWriteOptions { AllowTrailer = true, AllowFinalAccessBits = true });

            Assert.True(withKeyB.Succeeded);
            Assert.Equal(next, chip.Peek(11));
        }

        [Fact]
        public async Task ChangedAccessBits_NeedTheRightToWriteThem_AndNothingIsWrittenWithoutIt()
        {
            // 100: nobody may write the access bits. A trailer whose bits differ from the ones on the chip is not written by any key.
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(2, KeyA, new ClassicAccessBits(1, 1, 1, 1), 0x00, KeyB);

            var trailer = ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0xC1, KeyB);
            var result = await new ClassicOperations(chip).WriteBlockAsync(11, trailer, Keys(KeyAText, KeyBText), new ClassicWriteOptions { AllowTrailer = true });

            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(0, chip.Writes);
        }

        // ── a sector ──

        [Fact]
        public async Task ASector_IsWrittenBlockByBlock_InOrder_AndEveryBlockIsReadBack()
        {
            var chip = new SimulatedClassicChip();
            var data = Pattern(0x01, 48);

            var result = await new ClassicOperations(chip).WriteSectorAsync(2, data, Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(3, result.BlocksWritten);
            Assert.Equal(data.Take(16), chip.Peek(8));
            Assert.Equal(data.Skip(16).Take(16), chip.Peek(9));
            Assert.Equal(data.Skip(32), chip.Peek(10));
            Assert.Equal(
                new[]
                {
                    "login 2 KeyA", "read 11", "write 8 (16 bytes)", "read 8", "write 9 (16 bytes)", "read 9", "write 10 (16 bytes)", "read 10"
                },
                chip.Log);
        }

        [Fact]
        public async Task SectorZero_LeavesTheManufacturerBlockAlone()
        {
            var chip = new SimulatedClassicChip();
            var before = chip.Peek(0);
            var data = Pattern(0x01, 32);

            var result = await new ClassicOperations(chip).WriteSectorAsync(0, data, Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.BlocksWritten);
            Assert.Equal(before, chip.Peek(0));
            Assert.Equal(data.Take(16), chip.Peek(1));
            Assert.Equal(data.Skip(16), chip.Peek(2));
        }

        [Fact]
        public async Task SectorZero_WritesTheManufacturerBlockToo_WhenItMayBe()
        {
            var chip = new SimulatedClassicChip { Block0IsWritable = true };
            var data = Pattern(0x01, 48);

            var result = await new ClassicOperations(chip).WriteSectorAsync(0, data, Keys(Ff, null), new ClassicWriteOptions { AllowManufacturerBlock = true });

            Assert.True(result.Succeeded);
            Assert.Equal(3, result.BlocksWritten);
            Assert.Equal(data.Take(16), chip.Peek(0));
        }

        [Fact]
        public async Task ALargeSector_IsWrittenWithFifteenBlocks_AndNeverItsTrailer()
        {
            var chip = new SimulatedClassicChip();
            var trailerBefore = chip.Peek(159);
            var data = Pattern(0x01, 240);

            var result = await new ClassicOperations(chip).WriteSectorAsync(33, data, Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(15, result.BlocksWritten);
            Assert.Equal(15, chip.Writes);
            Assert.Equal(trailerBefore, chip.Peek(159));
            Assert.Equal(data.Skip(224), chip.Peek(158));
        }

        [Fact]
        public async Task ASectorWriteThatTakesTheTrailerAsData_IsRefusedAsTheWrongLength_AndTouchesNoChip()
        {
            var chip = new SimulatedClassicChip();
            var result = await new ClassicOperations(chip).WriteSectorAsync(2, new byte[64], Keys(Ff, null), new ClassicWriteOptions { AllowTrailer = true });

            Assert.Equal(ClassicWriteViolation.WrongLength, result.Violation);
            Assert.Empty(chip.Log);
        }

        [Fact]
        public async Task ASectorWithBlocksOfDifferentKeys_SwitchesKeysBetweenTheBlocks()
        {
            // Groups 0 and 2: 100 (only key B writes). Group 1: 000 (both keys write, key A is used). Key B can log in (trailer 011).
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(6, KeyA, new ClassicAccessBits(1, 0, 1, 6), 0x00, KeyB);

            var data = Pattern(0x01, 48);
            var result = await new ClassicOperations(chip).WriteSectorAsync(6, data, Keys(KeyAText, KeyBText), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(3, result.BlocksWritten);
            Assert.Equal(
                new[]
                {
                    "login 6 KeyA", "read 27",
                    "login 6 KeyB", "read 27", "write 24 (16 bytes)", "read 24",
                    "login 6 KeyA", "read 27", "write 25 (16 bytes)", "read 25",
                    "login 6 KeyB", "read 27", "write 26 (16 bytes)", "read 26"
                },
                chip.Log);
            Assert.Equal(data.Skip(32), chip.Peek(26));
        }

        [Fact]
        public async Task ASectorThatOneBlockOfCannotBeWritten_IsNotWrittenAtAll()
        {
            // The second block is 010: nobody writes it. The other two could be written, and are not: all or nothing is checked first.
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(6, KeyA, new ClassicAccessBits(0, 2, 0, 4), 0x00, KeyB);

            var result = await new ClassicOperations(chip).WriteSectorAsync(6, Pattern(0x01, 48), Keys(KeyAText, null), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(0, result.BlocksWritten);
            Assert.Equal(0, chip.Writes);
        }

        [Fact]
        public async Task ASectorWrite_StopsAtTheFirstBlockThatFails_AndTheBlocksAfterItAreNotTouched()
        {
            var chip = new SimulatedClassicChip();

            chip.FailNextWriteOf(9);

            var data = Pattern(0x01, 48);
            var result = await new ClassicOperations(chip).WriteSectorAsync(2, data, Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.AuthFailure, result.Error);
            Assert.Equal(1, result.BlocksWritten);
            Assert.Equal(9, result.FailedBlock);
            Assert.Equal(2, chip.Writes);
            Assert.Equal(data.Take(16), chip.Peek(8));
            Assert.Equal(new byte[16], chip.Peek(9));
            Assert.Equal(new byte[16], chip.Peek(10));
        }

        [Fact]
        public async Task ASectorIsWrittenOnAChipThatJudgesTheLoginAtOnce_ToTheo()
        {
            var chip = new SimulatedClassicChip { JudgesLoginAtOnce = true };
            var result = await new ClassicOperations(chip).WriteSectorAsync(2, Pattern(0x01, 48), Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(3, result.BlocksWritten);
        }

        [Fact]
        public async Task NoKeysOrNoOptions_AreRefused()
        {
            var ops = new ClassicOperations(new SimulatedClassicChip());

            await Assert.ThrowsAsync<ArgumentNullException>(() => ops.WriteBlockAsync(5, new byte[16], null!, ClassicWriteOptions.DataOnly));
            await Assert.ThrowsAsync<ArgumentNullException>(() => ops.WriteBlockAsync(5, new byte[16], Keys(Ff, null), null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() => ops.WriteSectorAsync(2, new byte[48], null!, ClassicWriteOptions.DataOnly));
            await Assert.ThrowsAsync<ArgumentNullException>(() => ops.WriteSectorAsync(2, new byte[48], Keys(Ff, null), null!));
        }

        // The chip is gone as soon as it is asked to write.
        private sealed class GoneBeforeTheWrite : IClassicPort
        {
            private readonly SimulatedClassicChip inner;

            public GoneBeforeTheWrite(SimulatedClassicChip inner)
            {
                this.inner = inner;
            }

            public ClassicCardSize? Size => inner.Size;

            public Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key) => inner.LoginAsync(block, keyType, key);

            public Task<ClassicBlockResult> ReadBlockAsync(int block) => inner.ReadBlockAsync(block);

            public Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data)
            {
                inner.CardPresent = false;

                return inner.WriteBlockAsync(block, data);
            }

            public Task<ClassicPortResult> ReselectAsync() => inner.ReselectAsync();
        }
    }
}
