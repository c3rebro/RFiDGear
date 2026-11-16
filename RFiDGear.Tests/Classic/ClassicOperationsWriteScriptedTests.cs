using System;
using System.Linq;
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

            /// <summary>Gets or sets what happens on the first login (the caller's array that changes while the call waits for the reader).</summary>
            public Action? OnFirstLogin { get; set; }

            public Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key)
            {
                var hook = OnFirstLogin;

                OnFirstLogin = null;
                hook?.Invoke();

                return inner.LoginAsync(block, keyType, key);
            }

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

        // ── the bytes that were approved are the bytes that are written ──

        [Fact]
        public async Task ABlock_IsWrittenAsItWasWhenTheCallStarted_WhateverTheCallerDoesToTheArrayMeanwhile()
        {
            var chip = new SimulatedClassicChip();
            var data = Enumerable.Range(0x10, 16).Select(i => (byte)i).ToArray();
            var approved = (byte[])data.Clone();
            var port = new ScriptedPort(chip) { OnFirstLogin = () => Array.Fill(data, (byte)0xEE) };

            var result = await new ClassicOperations(port).WriteBlockAsync(5, data, Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(approved, chip.Peek(5));
        }

        [Fact]
        public async Task ATrailer_IsWrittenAsItWasApproved_AndNotAsTheEditorLeftIt()
        {
            // The access bits that were approved are consistent and can be changed again; the array is made final while the call waits.
            var chip = new SimulatedClassicChip();
            var trailer = ClassicAccessBits.Transport.BuildTrailer(KeyA, 0x00, KeyB);
            var approved = (byte[])trailer.Clone();
            var port = new ScriptedPort(chip) { OnFirstLogin = () => new ClassicAccessBits(7, 7, 7, 7).BuildTrailer(KeyA, 0x00, KeyB).CopyTo(trailer, 0) };

            var result = await new ClassicOperations(port).WriteBlockAsync(7, trailer, Keys(Ff, null), new ClassicWriteOptions { AllowTrailer = true });

            Assert.True(result.Succeeded);
            Assert.Equal(approved, chip.Peek(7));
        }

        [Fact]
        public async Task ASector_IsWrittenAsItWasWhenTheCallStarted_WhateverTheCallerDoesToTheArrayMeanwhile()
        {
            var chip = new SimulatedClassicChip();
            var data = Enumerable.Range(1, 48).Select(i => (byte)i).ToArray();
            var approved = (byte[])data.Clone();
            var port = new ScriptedPort(chip) { OnFirstLogin = () => Array.Fill(data, (byte)0xEE) };

            var result = await new ClassicOperations(port).WriteSectorAsync(2, data, Keys(Ff, null), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(approved, chip.Peek(8).Concat(chip.Peek(9)).Concat(chip.Peek(10)).ToArray());
        }

        // ── what a trailer reads back as ──

        [Fact]
        public async Task ATrailerWhoseKeyBDoesNotReadBackAsWritten_IsReported_WhenKeyACanReadIt()
        {
            // The transport access bits let key A read key B: the new key B is part of what must read back.
            var chip = new SimulatedClassicChip { CorruptsWrites = true, CorruptedByte = 12 };
            var trailer = ClassicAccessBits.Transport.BuildTrailer(KeyA, 0x00, KeyB);

            var result = await new ClassicOperations(chip).WriteBlockAsync(7, trailer, Keys(Ff, Ff), new ClassicWriteOptions { AllowTrailer = true });

            Assert.Equal(ERROR.Unknown, result.Error);
            Assert.Equal(7, result.FailedBlock);
        }

        [Fact]
        public async Task ATrailerWhoseKeyBIsHiddenByTheNewAccessBits_IsNotComparedForKeyB()
        {
            // With the access bits of the MAD key B is never shown (it reads as zeros): there is nothing to compare, and the write is not reported as wrong.
            var chip = new SimulatedClassicChip { CorruptsWrites = true, CorruptedByte = 12 };
            var trailer = ClassicAccessBits.MadStandard.BuildTrailer(KeyA, 0xC1, KeyB);

            var result = await new ClassicOperations(chip).WriteBlockAsync(7, trailer, Keys(Ff, Ff), new ClassicWriteOptions { AllowTrailer = true });

            Assert.True(result.Succeeded);
        }

        // ── a key that the chip refused is not a key to write with ──

        [Fact]
        public async Task AKeyThatTheChipRefused_IsNotUsedToWrite_WhenKeyBMayWriteToo()
        {
            // The data blocks are 000 (both keys may write) and the trailer 011 (key B logs in): key A is wrong, key B opens the sector.
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(1, KeyA, new ClassicAccessBits(0, 0, 0, 6), 0x69, KeyB);

            var data = Enumerable.Range(0x20, 16).Select(i => (byte)i).ToArray();
            var result = await new ClassicOperations(chip).WriteBlockAsync(5, data, Keys("111111111111", KeyBText), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(data, chip.Peek(5));

            // The wrong key A was tried once, at the start, and never again.
            Assert.Single(chip.Log, entry => entry == "login 1 KeyA");
        }

        [Fact]
        public async Task AKeyThatTheChipRefused_IsNotUsedToWriteASector_WhenKeyBMayWriteToo()
        {
            var chip = new SimulatedClassicChip();

            chip.SetTrailer(1, KeyA, new ClassicAccessBits(0, 0, 0, 6), 0x69, KeyB);

            var data = Enumerable.Range(1, 48).Select(i => (byte)i).ToArray();
            var result = await new ClassicOperations(chip).WriteSectorAsync(1, data, Keys("111111111111", KeyBText), ClassicWriteOptions.DataOnly);

            Assert.True(result.Succeeded);
            Assert.Equal(3, result.BlocksWritten);
            Assert.Equal(data, chip.Peek(4).Concat(chip.Peek(5)).Concat(chip.Peek(6)).ToArray());
            Assert.Single(chip.Log, entry => entry == "login 1 KeyA");
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
