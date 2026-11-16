using System;
using System.Linq;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;
using Xunit;

namespace RFiDGear.Tests.Classic
{
    /// <summary>
    /// The read side of the Classic operations, against a simulated chip that behaves like the real 4K did: access bits enforced, a login that
    /// is only judged at the next access, a chip that is halted by a refusal.
    /// </summary>
    public class ClassicOperationsReadTests
    {
        private const string Ff = "FFFFFFFFFFFF";
        private static readonly byte[] KeyA = Convert.FromHexString("A0A1A2A3A4A5");
        private static readonly byte[] KeyB = Convert.FromHexString("B0B1B2B3B4B5");

        private static ClassicSectorKeys Keys(string? a, string? b)
        {
            Assert.True(ClassicSectorKeys.TryCreate(a, b, out var keys));

            return keys;
        }

        // A sector as a MAD sector is: key B hidden, data blocks 100 (key A reads, key B writes).
        private static void MakeMadSector(SimulatedClassicChip chip, int sector) =>
            chip.SetTrailer(sector, KeyA, ClassicAccessBits.MadStandard, 0xC1, KeyB);

        [Fact]
        public async Task AFactorySector_IsReadWithKeyA_InOneLogin()
        {
            var chip = new SimulatedClassicChip();

            chip.Poke(5, Enumerable.Repeat((byte)0x55, 16).ToArray());

            var read = await new ClassicOperations(chip).ReadSectorAsync(1, Keys(Ff, Ff));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.True(read.IsAuthenticated);
            Assert.Equal(ClassicKeyType.KeyA, read.LoginKey);
            Assert.Equal(ClassicAccessBits.Transport, read.AccessBits);
            Assert.Equal(new[] { 4, 5, 6, 7 }, read.Blocks.Select(b => b.Block));
            Assert.Equal(new[] { 0, 1, 2, 3 }, read.Blocks.Select(b => b.BlockInSector));
            Assert.All(read.Blocks, b => Assert.True(b.WasRead));
            Assert.Equal(Enumerable.Repeat((byte)0x55, 16).ToArray(), read.Blocks[1].Data);

            // The trailer shows key A as zeros, and key B because the factory bits let key A read it.
            Assert.Equal("000000000000FF078069FFFFFFFFFFFF", Convert.ToHexString(read.Blocks[3].Data!));

            Assert.Equal(1, chip.Logins);
            Assert.Equal(0, chip.Writes);
            Assert.Equal(0, chip.Reselects);
        }

        [Fact]
        public async Task ALargeSector_HasSixteenBlocks()
        {
            var chip = new SimulatedClassicChip();
            var read = await new ClassicOperations(chip).ReadSectorAsync(33, Keys(Ff, null));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.Equal(Enumerable.Range(144, 16), read.Blocks.Select(b => b.Block));
            Assert.All(read.Blocks, b => Assert.True(b.WasRead));
        }

        [Fact]
        public async Task TheTrailerCarriesTheGeneralPurposeByteOfTheChip()
        {
            var chip = new SimulatedClassicChip();
            var read = await new ClassicOperations(chip).ReadSectorAsync(39, Keys(Ff, null));

            Assert.Equal(0xBC, read.Blocks[^1].Data![9]);
            Assert.Equal(255, read.Blocks[^1].Block);
        }

        [Fact]
        public async Task AMadSector_IsReadWithKeyB_WhenThereIsNoKeyA()
        {
            var chip = new SimulatedClassicChip();

            MakeMadSector(chip, 3);

            var read = await new ClassicOperations(chip).ReadSectorAsync(3, Keys(null, "B0B1B2B3B4B5"));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.Equal(ClassicKeyType.KeyB, read.LoginKey);
            Assert.Equal(ClassicAccessBits.MadStandard, read.AccessBits);
            Assert.All(read.Blocks, b => Assert.True(b.WasRead));
        }

        [Fact]
        public async Task AWrongKeyA_IsFollowedByKeyB_AfterTheChipWasWoken()
        {
            var chip = new SimulatedClassicChip();

            MakeMadSector(chip, 3);

            var read = await new ClassicOperations(chip).ReadSectorAsync(3, Keys(Ff, "B0B1B2B3B4B5"));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.Equal(ClassicKeyType.KeyB, read.LoginKey);

            // login A, the trailer is refused, wake, login B, the trailer is read: nobody reached a login with a halted chip.
            Assert.Equal(
                new[] { "login 3 KeyA", "read 15", "reselect", "login 3 KeyB", "read 15" },
                chip.Log.Take(5));
        }

        [Fact]
        public async Task KeyBThatIsData_DoesNotLogIn_AndTheOperationSaysSo()
        {
            var chip = new SimulatedClassicChip();
            var read = await new ClassicOperations(chip).ReadSectorAsync(1, Keys(null, Ff));

            Assert.Equal(ERROR.AuthFailure, read.Error);
            Assert.False(read.IsAuthenticated);
            Assert.Empty(read.Blocks);
            Assert.Null(read.LoginKey);
            Assert.Equal(1, chip.Reselects);
        }

        [Fact]
        public async Task WrongKeys_AreAnAuthFailure_AndNothingIsWritten()
        {
            var chip = new SimulatedClassicChip();
            var read = await new ClassicOperations(chip).ReadSectorAsync(2, Keys("000000000000", "111111111111"));

            Assert.Equal(ERROR.AuthFailure, read.Error);
            Assert.Equal(0, chip.Writes);
            Assert.Equal(2, chip.Logins);
        }

        [Fact]
        public async Task WithoutKeys_NothingGoesToTheChip()
        {
            var chip = new SimulatedClassicChip();
            var read = await new ClassicOperations(chip).ReadSectorAsync(2, Keys(null, "  "));

            Assert.Equal(ERROR.AuthFailure, read.Error);
            Assert.Empty(chip.Log);
        }

        [Fact]
        public async Task AChipThatIsGone_IsATransportError_AtTheFirstCall()
        {
            var chip = new SimulatedClassicChip { CardPresent = false };
            var read = await new ClassicOperations(chip).ReadSectorAsync(2, Keys(Ff, Ff));

            Assert.Equal(ERROR.TransportError, read.Error);
            Assert.Single(chip.Log);
        }

        [Fact]
        public async Task AChipThatIsTakenAwayInTheMiddle_StopsTheRead()
        {
            var chip = new SimulatedClassicChip();
            var ops = new ClassicOperations(new RemoveAfterReads(chip, 2));
            var read = await ops.ReadSectorAsync(2, Keys(Ff, null));

            Assert.Equal(ERROR.TransportError, read.Error);
            Assert.Contains(read.Blocks, b => b.WasRead);
            Assert.Contains(read.Blocks, b => !b.WasRead);

            // The read that found the chip gone is the last call: nobody wakes or logs in again to find that out a second time.
            Assert.Equal("read 9", chip.Log[^1]);
        }

        [Fact]
        public async Task AChipThatDisappearsWhileItIsWoken_IsATransportError_NotAnAuthFailure()
        {
            // The key was refused, and the look at the chip that follows finds nothing: the chip is gone, which is not a wrong key.
            var chip = new SimulatedClassicChip();
            var read = await new ClassicOperations(new GoneWhenWoken(chip)).ReadSectorAsync(2, Keys("000000000000", null));

            Assert.Equal(ERROR.TransportError, read.Error);
        }

        [Fact]
        public async Task AChipThatJudgesTheLoginAtOnce_IsNotAskedForTheTrailerAfterARefusedLogin()
        {
            // The TWN4 says no to a wrong key at the login. There is nothing to prove then, and the trailer is not read with it.
            var chip = new SimulatedClassicChip { JudgesLoginAtOnce = true };

            MakeMadSector(chip, 3);

            var read = await new ClassicOperations(chip).ReadSectorAsync(3, Keys(Ff, "B0B1B2B3B4B5"));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.Equal(ClassicKeyType.KeyB, read.LoginKey);
            Assert.Equal(
                new[] { "login 3 KeyA", "reselect", "login 3 KeyB", "read 15", "read 12", "read 13", "read 14" },
                chip.Log);
        }

        [Fact]
        public async Task ARefusedReadWhoseNewLoginIsRefusedToo_EndsTheReadOfThatKey()
        {
            // Block 9 is refused, and the trailer that proves the new login is refused as well: the session is lost, so block 10 is not asked for.
            var chip = new SimulatedClassicChip();

            chip.FailReadOf(9, 1);
            chip.FailReadOf(11, 2);

            var read = await new ClassicOperations(chip).ReadSectorAsync(2, Keys(Ff, null));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.True(read.Blocks[0].WasRead);
            Assert.False(read.Blocks[1].WasRead);
            Assert.False(read.Blocks[2].WasRead);
            Assert.True(read.Blocks[3].WasRead);
            Assert.DoesNotContain("read 10", chip.Log);
        }

        [Fact]
        public async Task AKeyBThatIsData_IsNotTriedForABlockThatOnlyKeyBMayRead()
        {
            // The trailer 001 lets key A read key B, so key B cannot log in; the second group (101) is for key B only. Nothing can read it, and
            // nothing is tried: a login with key B would fail and halt the chip.
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(6, KeyA, new ClassicAccessBits(1, 5, 0, 4), 0x00, KeyB);

            var read = await new ClassicOperations(chip).ReadSectorAsync(6, Keys("A0A1A2A3A4A5", "B0B1B2B3B4B5"));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.False(read.Blocks[1].WasRead);
            Assert.DoesNotContain(chip.Log, entry => entry.Contains("KeyB", StringComparison.Ordinal));
            Assert.Equal(0, chip.Reselects);
        }

        [Fact]
        public async Task AKeyBThatThereIsNot_IsNotAskedFor_ForABlockThatOnlyKeyBMayRead()
        {
            // Key B is a key here (trailer 011) and reads the second group alone, but the operation has no key B: that block stays unread.
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(6, KeyA, new ClassicAccessBits(1, 6, 0, 6), 0x00, KeyB);

            var read = await new ClassicOperations(chip).ReadSectorAsync(6, Keys("A0A1A2A3A4A5", null));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.False(read.Blocks[1].WasRead);
            Assert.DoesNotContain(chip.Log, entry => entry.Contains("KeyB", StringComparison.Ordinal));
        }

        [Fact]
        public async Task WithoutAccessBits_ABlockThatTheLoginKeyCannotRead_IsNotLeftToTheOtherKey()
        {
            // Nothing says that the other key could read it, so it is not tried: a guess costs a refusal and a halted chip.
            var port = new ScriptedPort();

            port.Refused.Add(9);

            var read = await new ClassicOperations(port).ReadSectorAsync(2, Keys(Ff, "B0B1B2B3B4B5"));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.Null(read.AccessBits);
            Assert.False(read.Blocks[1].WasRead);
            Assert.DoesNotContain(port.Logins, login => login.Contains("KeyB", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ASectorThatTheChipDoesNotHave_IsRefusedWithoutACall()
        {
            var chip = new SimulatedClassicChip(ClassicCardSize.K1);
            var ops = new ClassicOperations(chip);

            Assert.Equal(ERROR.ProtocolConstraint, (await ops.ReadSectorAsync(16, Keys(Ff, null))).Error);
            Assert.Equal(ERROR.ProtocolConstraint, (await ops.ReadSectorAsync(-1, Keys(Ff, null))).Error);
            Assert.Empty(chip.Log);
        }

        [Fact]
        public async Task ABlockThatOnlyKeyBMayRead_IsReadWithKeyB_WhenThereIsOne()
        {
            var chip = new SimulatedClassicChip();

            // Group 0 100 (A or B reads), group 1 011 (only key B reads), group 2 000, trailer 011 so that key B is a key.
            chip.SetTrailer(6, KeyA, new ClassicAccessBits(1, 6, 0, 6), 0x00, KeyB);
            chip.Poke(25, Enumerable.Repeat((byte)0x77, 16).ToArray());

            var read = await new ClassicOperations(chip).ReadSectorAsync(6, Keys("A0A1A2A3A4A5", "B0B1B2B3B4B5"));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.All(read.Blocks, b => Assert.True(b.WasRead));
            Assert.Equal(Enumerable.Repeat((byte)0x77, 16).ToArray(), read.Blocks[1].Data);
            Assert.Equal(ClassicKeyType.KeyA, read.LoginKey);

            // Key A reads the blocks 24 and 26 first, then key B logs in and reads block 25. Nothing was refused, so the chip was never woken:
            // a wake is a login that fails, and on a chip that is awake it would halt it and make the login of key B fail.
            Assert.Equal(
                new[] { "login 6 KeyA", "read 27", "read 24", "read 26", "login 6 KeyB", "read 27", "read 25" },
                chip.Log);
            Assert.Equal(0, chip.Reselects);
        }

        [Fact]
        public async Task ABlockThatOnlyKeyBMayRead_IsLeftUnreadWithoutAnAttempt_WhenThereIsOnlyKeyA()
        {
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(6, KeyA, new ClassicAccessBits(1, 6, 0, 4), 0x00, KeyB);

            var read = await new ClassicOperations(chip).ReadSectorAsync(6, Keys("A0A1A2A3A4A5", null));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.True(read.Blocks[0].WasRead);
            Assert.False(read.Blocks[1].WasRead);
            Assert.True(read.Blocks[2].WasRead);
            Assert.DoesNotContain("read 25", chip.Log);
            Assert.Equal(0, chip.Reselects);
        }

        [Fact]
        public async Task AReadThatIsRefusedInTheMiddle_IsFollowedByAFreshLogin_AndTheOtherBlocksAreStillRead()
        {
            var chip = new SimulatedClassicChip();

            chip.FailNextReadOf(9);

            var read = await new ClassicOperations(chip).ReadSectorAsync(2, Keys(Ff, null));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.True(read.Blocks[0].WasRead);
            Assert.False(read.Blocks[1].WasRead);
            Assert.True(read.Blocks[2].WasRead);
            Assert.True(read.Blocks[3].WasRead);
            Assert.Equal(2, chip.Logins);
            Assert.Equal(1, chip.Reselects);
        }

        [Fact]
        public async Task ASectorWhoseAccessBitsAreBroken_CannotBeLoggedInTo()
        {
            var chip = new SimulatedClassicChip();
            var trailer = chip.Peek(11);

            trailer[6] ^= 0xFF;
            chip.Poke(11, trailer);

            var read = await new ClassicOperations(chip).ReadSectorAsync(2, Keys(Ff, null));

            // The simulated chip refuses everything in a sector whose bits are broken, as a real one does: nothing can log in.
            Assert.Equal(ERROR.AuthFailure, read.Error);
        }

        [Fact]
        public async Task WhenTheReaderGivesNoUsableAccessBits_EveryBlockIsTriedWithTheKeyThatLoggedIn()
        {
            // A reader that hands back a trailer without access bits (zeros) leaves the operation without knowledge: it must not guess
            // which block a key may read, it tries them all with the key that logged in.
            var port = new ScriptedPort();
            var read = await new ClassicOperations(port).ReadSectorAsync(2, Keys(Ff, null));

            Assert.Equal(ERROR.NoError, read.Error);
            Assert.Null(read.AccessBits);
            Assert.All(read.Blocks, b => Assert.True(b.WasRead));
            Assert.Equal(new[] { 11, 8, 9, 10 }, port.ReadOrder);

            // One login, to the first block of the sector, with key A (6 bytes); nothing was written.
            Assert.Equal(new[] { "8 KeyA 6" }, port.Logins);
        }

        [Theory]
        [InlineData("ff ff ff ff ff ff", true)]
        [InlineData("FFFFFFFFFFFF", true)]
        [InlineData("a0a1a2a3a4a5", true)]
        [InlineData("", true)]
        [InlineData(null, true)]
        [InlineData("   ", true)]
        [InlineData("FFFFFFFFFFF", false)]
        [InlineData("FFFFFFFFFFFFF", false)]
        [InlineData("GGGGGGGGGGGG", false)]
        [InlineData("ff-ff-ff-ff-ff-ff", false)]
        [InlineData("error 1", false)]
        public void TheTextOfAKey_IsAKeyOrNothingAtAll(string? text, bool accepted)
        {
            Assert.Equal(accepted, ClassicSectorKeys.TryCreate(text, null, out var keys));
            Assert.Equal(accepted, ClassicSectorKeys.TryCreate(null, text, out _));
            Assert.Equal(accepted, keys != null);
        }

        [Fact]
        public void TheKeysAreClearedWhenTheOperationEnds_AndTheTextNeverShowsThem()
        {
            Assert.True(ClassicSectorKeys.TryCreate("A0A1A2A3A4A5", null, out var keys));

            var bytes = keys.Get(ClassicKeyType.KeyA);

            Assert.Equal("ClassicSectorKeys(A, -)", keys.ToString());
            Assert.DoesNotContain("A0A1", keys.ToString(), StringComparison.OrdinalIgnoreCase);

            keys.Dispose();

            Assert.All(bytes, b => Assert.Equal(0, b));
            Assert.Throws<InvalidOperationException>(() => keys.Get(ClassicKeyType.KeyB));
        }

        [Fact]
        public void BothKeys_AreCleared_WhenTheOperationEnds()
        {
            Assert.True(ClassicSectorKeys.TryCreate("A0A1A2A3A4A5", "B0B1B2B3B4B5", out var keys));

            var a = keys.Get(ClassicKeyType.KeyA);
            var b = keys.Get(ClassicKeyType.KeyB);

            Assert.Equal("ClassicSectorKeys(A, B)", keys.ToString());

            keys.Dispose();

            Assert.All(a, value => Assert.Equal(0, value));
            Assert.All(b, value => Assert.Equal(0, value));
        }

        [Fact]
        public async Task ANullPortOrNullKeys_AreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new ClassicOperations(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() => new ClassicOperations(new SimulatedClassicChip()).ReadSectorAsync(0, null!));
        }

        // A port that logs in to anything and reads zeros everywhere, so the access bits of a trailer are not there.
        private sealed class ScriptedPort : IClassicPort
        {
            public System.Collections.Generic.List<int> ReadOrder { get; } = new System.Collections.Generic.List<int>();

            public System.Collections.Generic.List<string> Logins { get; } = new System.Collections.Generic.List<string>();

            // Blocks that the chip refuses to read, whatever happens.
            public System.Collections.Generic.HashSet<int> Refused { get; } = new System.Collections.Generic.HashSet<int>();

            public ClassicCardSize? Size => ClassicCardSize.K1;

            public Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key)
            {
                Logins.Add(block + " " + keyType + " " + key.Length);

                return Task.FromResult(ClassicPortResult.Ok);
            }

            public Task<ClassicBlockResult> ReadBlockAsync(int block)
            {
                ReadOrder.Add(block);

                return Task.FromResult(Refused.Contains(block)
                    ? new ClassicBlockResult(ERROR.AuthFailure, null)
                    : new ClassicBlockResult(ERROR.NoError, new byte[16]));
            }

            public Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data)
            {
                Logins.Add("write " + block + " " + data.Length);

                return Task.FromResult(new ClassicPortResult(ERROR.ProtocolConstraint));
            }

            public Task<ClassicPortResult> ReselectAsync() => Task.FromResult(ClassicPortResult.Ok);
        }

        // The chip is gone as soon as it is woken.
        private sealed class GoneWhenWoken : IClassicPort
        {
            private readonly SimulatedClassicChip inner;

            public GoneWhenWoken(SimulatedClassicChip inner)
            {
                this.inner = inner;
            }

            public ClassicCardSize? Size => inner.Size;

            public Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key) => inner.LoginAsync(block, keyType, key);

            public Task<ClassicBlockResult> ReadBlockAsync(int block) => inner.ReadBlockAsync(block);

            public Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data) => inner.WriteBlockAsync(block, data);

            public Task<ClassicPortResult> ReselectAsync()
            {
                inner.CardPresent = false;

                return inner.ReselectAsync();
            }
        }

        // Takes the chip away after a number of reads.
        private sealed class RemoveAfterReads : IClassicPort
        {
            private readonly SimulatedClassicChip inner;
            private readonly int allowed;
            private int reads;

            public RemoveAfterReads(SimulatedClassicChip inner, int allowed)
            {
                this.inner = inner;
                this.allowed = allowed;
            }

            public ClassicCardSize? Size => inner.Size;

            public Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key) => inner.LoginAsync(block, keyType, key);

            public Task<ClassicBlockResult> ReadBlockAsync(int block)
            {
                if (++reads > allowed)
                {
                    inner.CardPresent = false;
                }

                return inner.ReadBlockAsync(block);
            }

            public Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data) => inner.WriteBlockAsync(block, data);

            public Task<ClassicPortResult> ReselectAsync() => inner.ReselectAsync();
        }
    }
}
