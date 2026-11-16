using System;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;
using Xunit;

namespace RFiDGear.Tests.Classic
{
    /// <summary>
    /// The write side where the answer of the chip is scripted: the look after a refused write, a read back that differs in one byte, and the
    /// rights of a trailer whose key may not be written by anybody.
    /// </summary>
    public class ClassicOperationsWriteScriptedTests
    {
        private const string Ff = "FFFFFFFFFFFF";
        private const string KeyAText = "A0A1A2A3A4A5";
        private const string KeyBText = "B0B1B2B3B4B5";
        private static readonly byte[] KeyA = Convert.FromHexString(KeyAText);
        private static readonly byte[] KeyB = Convert.FromHexString(KeyBText);

        private static ClassicSectorKeys Keys(string? a, string? b)
        {
            Assert.True(ClassicSectorKeys.TryCreate(a, b, out var keys));

            return keys;
        }

        // The write is answered as scripted; or it is done, and the chip is taken away when the chip refused it.
        private sealed class ScriptedPort : IClassicPort
        {
            private readonly SimulatedClassicChip inner;

            public ScriptedPort(SimulatedClassicChip inner)
            {
                this.inner = inner;
            }

            public ClassicPortResult? WriteAnswer { get; set; }

            public bool TakesTheChipAwayWhenAWriteIsRefused { get; set; }

            public ClassicCardSize? Size => inner.Size;

            public Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key) => inner.LoginAsync(block, keyType, key);

            public Task<ClassicBlockResult> ReadBlockAsync(int block) => inner.ReadBlockAsync(block);

            public async Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data)
            {
                if (WriteAnswer != null)
                {
                    return WriteAnswer.Value;
                }

                var result = await inner.WriteBlockAsync(block, data);

                if (!result.Succeeded && TakesTheChipAwayWhenAWriteIsRefused)
                {
                    inner.CardPresent = false;
                }

                return result;
            }

            public Task<ClassicPortResult> ReselectAsync() => inner.ReselectAsync();
        }

        [Fact]
        public async Task AWriteThatSaysTheChipIsGone_IsATransportError_AndNoLookIsMade()
        {
            // The write says that the chip is gone, but the antenna would still find one: the answer of the write is the answer, and the look is not made.
            var chip = new SimulatedClassicChip();
            var port = new ScriptedPort(chip) { WriteAnswer = new ClassicPortResult(ERROR.TransportError) };

            var result = await new ClassicOperations(port).WriteBlockAsync(5, new byte[16], Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(5, result.FailedBlock);
            Assert.Equal(0, chip.Reselects);
        }

        [Fact]
        public async Task ARefusedWrite_AfterWhichTheChipIsGone_IsATransportError_AndNotARefusal()
        {
            var chip = new SimulatedClassicChip();
            var port = new ScriptedPort(chip) { TakesTheChipAwayWhenAWriteIsRefused = true };

            chip.FailNextWriteOf(5);

            var result = await new ClassicOperations(port).WriteBlockAsync(5, new byte[16], Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.Equal(ERROR.TransportError, result.Error);
            Assert.Equal(1, chip.Writes);
        }

        [Fact]
        public async Task ATrailerWhoseGeneralPurposeByteDoesNotReadBackAsWritten_IsReported()
        {
            // Key A and the access bits read back as written, the byte behind them does not.
            var chip = new SimulatedClassicChip { CorruptsWrites = true, CorruptedByte = 9 };
            var trailer = ClassicAccessBits.Transport.BuildTrailer(KeyA, 0x00, KeyB);

            var result = await new ClassicOperations(chip).WriteBlockAsync(7, trailer, Keys(Ff, Ff), new ClassicWriteOptions { AllowTrailer = true });

            Assert.Equal(ERROR.Unknown, result.Error);
            Assert.Equal(7, result.FailedBlock);
        }

        [Fact]
        public async Task AKeyThatNoKeyMayWrite_IsNotAttempted_EvenWhenNothingElseChanges()
        {
            // Condition 010: nobody writes key A, key B or the access bits (the bits are final, so the request says that they may be written).
            var chip = new SimulatedClassicChip();
            var bits = new ClassicAccessBits(0, 0, 0, 2);

            chip.SetTrailer(1, KeyA, bits, 0x00, KeyB);

            var next = bits.BuildTrailer(new byte[] { 1, 2, 3, 4, 5, 6 }, 0x00, KeyB);
            var result = await new ClassicOperations(chip).WriteBlockAsync(
                7, next, Keys(KeyAText, null), new ClassicWriteOptions { AllowTrailer = true, AllowFinalAccessBits = true });

            Assert.Equal(ERROR.PermissionDenied, result.Error);
            Assert.Equal(0, chip.Writes);
        }
    }
}
