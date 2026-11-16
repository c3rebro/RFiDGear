#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Classic;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The steps of the MIFARE Classic hardware cases (see <c>CLASSIC.md</c>). They call the reader the way the dialogs and the tasks do, so
    /// the unit tests run the same steps against a chip in memory (<c>HilClassicScenarioSelfCheckTests</c>) and a step that is wrong is found
    /// before it reaches a card. They are written for a card as it leaves the factory (every key <c>FFFFFFFFFFFF</c>, the access bits of the
    /// transport configuration): a card that is not takes the case to a skip with the reason, never to a failure. The cases that write put back
    /// what they changed, also after a step that failed.
    /// </summary>
    internal static class ClassicScenarios
    {
        /// <summary>The key of a chip as it leaves the factory.</summary>
        internal const string Factory = "FFFFFFFFFFFF";

        /// <summary>A key that no card has.</summary>
        internal const string WrongKey = "A5A5A5A5A5A5";

        /// <summary>The sector that the cases of the block and the sector write use; its data blocks are blocks 4 to 6.</summary>
        internal const int WriteSector = 1;

        /// <summary>The sector of a 4K that has 16 blocks.</summary>
        internal const int LargeSector = 33;

        /// <summary>The application identifier of the MAD case.</summary>
        internal const int Aid = 0x1234;

        /// <summary>Tells whether a card of this type is a MIFARE Classic and how large.</summary>
        /// <param name="cardType">The card type the reader found.</param>
        /// <param name="size">The size.</param>
        /// <returns><c>true</c> for a Classic chip.</returns>
        internal static bool IsClassic(CARD_TYPE cardType, out ClassicCardSize size) => ClassicLayout.TryGetSize(cardType, out size);

        /// <summary>Reads sector 1, and the first sector with 16 blocks of a 4K, with the factory key.</summary>
        /// <param name="device">The reader.</param>
        /// <param name="size">The size of the chip.</param>
        /// <returns>The task of the case.</returns>
        internal static async Task AFactorySectorIsReadAsync(ReaderDevice device, ClassicCardSize size)
        {
            var small = await ReadFactorySectorOrSkipAsync(device, WriteSector);

            Assert.Equal(4, small.DataBlock.Count);
            Assert.True(small.IsAuthenticated);
            Assert.Equal(new[] { 4, 5, 6, 7 }, small.DataBlock.Select(b => b.DataBlockNumberChipBased));
            Assert.All(small.DataBlock.Take(3), block => Assert.NotNull(block.Data));

            if (size == ClassicCardSize.K4)
            {
                var large = await ReadFactorySectorOrSkipAsync(device, LargeSector);

                Assert.Equal(16, large.DataBlock.Count);
                Assert.Equal(Enumerable.Range(144, 16), large.DataBlock.Select(b => b.DataBlockNumberChipBased));
            }
            else
            {
                // A sector that the chip does not have is refused, and the chip is still there afterwards.
                var beyond = ClassicLayout.SectorCount(size);
                var refused = await device.ReadMifareClassicSectorAsync(beyond, Factory, Factory);

                Assert.Equal(ERROR.ProtocolConstraint, refused.Error);
                Assert.Equal(ERROR.NoError, (await device.ReadMifareClassicSectorAsync(WriteSector, Factory, Factory)).Error);
            }
        }

        /// <summary>A wrong key is refused, and the card answers the next read as if nothing had happened.</summary>
        /// <param name="device">The reader.</param>
        /// <returns>The task of the case.</returns>
        internal static async Task AWrongKeyIsRefused_AndTheNextReadWorksAsync(ReaderDevice device)
        {
            await ReadFactorySectorOrSkipAsync(device, WriteSector);

            // Both keys wrong: refused. Key B only: it is plain data in the access bits of a factory chip and cannot log in.
            Assert.Equal(ERROR.AuthFailure, (await device.ReadMifareClassicSectorAsync(WriteSector, WrongKey, WrongKey)).Error);
            Assert.Equal(ERROR.NoError, (await device.ReadMifareClassicSectorAsync(WriteSector, Factory, Factory)).Error);
            Assert.Equal(ERROR.AuthFailure, (await device.ReadMifareClassicSectorAsync(WriteSector, string.Empty, Factory)).Error);
            Assert.Equal(ERROR.NoError, (await device.ReadMifareClassicSectorAsync(WriteSector, Factory, string.Empty)).Error);

            // A key that is no key never reaches the card.
            Assert.Equal(ERROR.AuthFailure, (await device.ReadMifareClassicSectorAsync(WriteSector, "not a key", Factory)).Error);
            Assert.Equal(ERROR.NoError, (await device.ReadMifareClassicSectorAsync(WriteSector, Factory, Factory)).Error);
        }

        /// <summary>Writes block 5 with other bytes, reads it back and puts the old bytes back.</summary>
        /// <param name="device">The reader.</param>
        /// <param name="whoIsOnTheReader">Asks who is on the reader now (the UID), so that a card that was replaced meanwhile is not written to.</param>
        /// <returns>The task of the case.</returns>
        internal static async Task ABlockIsWrittenReadBackAndPutBackAsync(ReaderDevice device, Func<Task<string?>> whoIsOnTheReader)
        {
            var before = await ReadFactorySectorOrSkipAsync(device, WriteSector);
            var uid = await whoIsOnTheReader();
            var old = before.DataBlock[1].Data ?? throw SkipException.ForSkip("Block 5 cannot be read with the factory key: the card is not as it left the factory.");
            var changed = old.Select(b => (byte)(b ^ 0xFF)).ToArray();

            await PutBackAfterAsync(
                () => device.WriteMifareClassicSingleBlock(5, Factory, Factory, old),
                whoIsOnTheReader,
                uid,
                async () =>
                {
                    Assert.Equal(uid, await whoIsOnTheReader());
                    Assert.Equal(ERROR.NoError, await device.WriteMifareClassicSingleBlock(5, Factory, Factory, changed));
                    Assert.Equal(changed, (await device.ReadMifareClassicSectorAsync(WriteSector, Factory, Factory)).Sector.DataBlock[1].Data);

                    // The blocks beside it are as they were.
                    var after = (await device.ReadMifareClassicSectorAsync(WriteSector, Factory, Factory)).Sector;

                    Assert.Equal(before.DataBlock[0].Data, after.DataBlock[0].Data);
                    Assert.Equal(before.DataBlock[2].Data, after.DataBlock[2].Data);
                });

            Assert.Equal(old, (await device.ReadMifareClassicSectorAsync(WriteSector, Factory, Factory)).Sector.DataBlock[1].Data);
        }

        /// <summary>Writes all data blocks of sector 2 (and the 15 of a large sector of a 4K) with other bytes, reads them back and puts the old bytes back.</summary>
        /// <param name="device">The reader.</param>
        /// <param name="size">The size of the chip.</param>
        /// <param name="whoIsOnTheReader">Asks who is on the reader now.</param>
        /// <returns>The task of the case.</returns>
        internal static async Task ASectorIsWrittenReadBackAndPutBackAsync(ReaderDevice device, ClassicCardSize size, Func<Task<string?>> whoIsOnTheReader)
        {
            await SectorRoundTripAsync(device, 2, whoIsOnTheReader);

            if (size == ClassicCardSize.K4)
            {
                await SectorRoundTripAsync(device, LargeSector, whoIsOnTheReader);
            }
        }

        private static async Task SectorRoundTripAsync(ReaderDevice device, int sector, Func<Task<string?>> whoIsOnTheReader)
        {
            var before = await ReadFactorySectorOrSkipAsync(device, sector);
            var uid = await whoIsOnTheReader();
            var dataBlocks = before.DataBlock.Count - 1;

            if (before.DataBlock.Take(dataBlocks).Any(block => block.Data == null))
            {
                throw SkipException.ForSkip($"A block of sector {sector} cannot be read with the factory key: the card is not as it left the factory.");
            }

            var old = before.DataBlock.Take(dataBlocks).SelectMany(block => block.Data!).ToArray();
            var changed = old.Select((b, i) => (byte)(b ^ (0x10 + i % 0xE0))).ToArray();

            await PutBackAfterAsync(
                () => device.WriteMifareClassicSingleSector(sector, Factory, Factory, old),
                whoIsOnTheReader,
                uid,
                async () =>
                {
                    Assert.Equal(uid, await whoIsOnTheReader());
                    Assert.Equal(ERROR.NoError, await device.WriteMifareClassicSingleSector(sector, Factory, Factory, changed));

                    var after = (await device.ReadMifareClassicSectorAsync(sector, Factory, Factory)).Sector;

                    Assert.Equal(changed, after.DataBlock.Take(dataBlocks).SelectMany(block => block.Data!).ToArray());

                    // The sector trailer was not touched: the factory key still logs in, and the keys and the access bits are as they were.
                    Assert.Equal(before.DataBlock[^1].Data!.Skip(6), after.DataBlock[^1].Data!.Skip(6));
                });

            Assert.Equal(old, (await device.ReadMifareClassicSectorAsync(sector, Factory, Factory)).Sector.DataBlock.Take(dataBlocks).SelectMany(block => block.Data!).ToArray());
        }

        /// <summary>
        /// Writes an application through the MAD of a chip that has none (which makes the MAD with the keys A0A1A2A3A4A5 and B0B1B2B3B4B5), reads it back
        /// through the MAD, and puts the chip back as it was: the trailers of the MAD sectors, the blocks of the directory and the data. It runs only on a
        /// chip that holds exactly what the put-back writes (every block it can change is zeros, every trailer is the factory one), and it fails, and says
        /// what is left, when the chip is not like that again when it is done.
        /// </summary>
        /// <param name="device">The reader.</param>
        /// <param name="size">The size of the chip.</param>
        /// <param name="whoIsOnTheReader">Asks who is on the reader now.</param>
        /// <returns>The task of the case.</returns>
        internal static async Task AnApplicationIsWrittenThroughTheMadAndPutBackAsync(ReaderDevice device, ClassicCardSize size, Func<Task<string?>> whoIsOnTheReader)
        {
            const string madKeyA = "A0A1A2A3A4A5";
            const string madKeyB = "B0B1B2B3B4B5";

            await ReadFactorySectorOrSkipAsync(device, 0);

            // The chip gets a MAD2 where it has the sector for it: the same test as the writer.
            var madSectors = MadSectorsOf(size);
            var before = await FactoryProblemsAsync(device, madSectors);

            // Whatever is not as the put-back writes it (a MAD, data, another general purpose byte) would be overwritten: the card is left alone.
            if (before.Count > 0)
            {
                throw SkipException.ForSkip("The card is not as it left the factory, and the case would overwrite it: " + before[0]);
            }

            var uid = await whoIsOnTheReader();
            var data = Enumerable.Range(1, 20).Select(i => (byte)i).ToArray();

            try
            {
                Assert.Equal(uid, await whoIsOnTheReader());
                Assert.Equal(
                    ERROR.NoError,
                    await device.WriteMifareClassicWithMAD(Aid, WriteSector, Factory, Factory, Factory, Factory, Factory, Factory, madKeyA, madKeyB, data, 0xC1, default, true, true));

                Assert.Equal(ERROR.NoError, await device.ReadMifareClassicWithMAD(Aid, Factory, Factory, madKeyA, string.Empty, 20, 0xC1, true, false));
                Assert.Equal(data, device.MifareClassicData);
            }
            finally
            {
                Assert.True(await whoIsOnTheReader() == uid, "The card on the reader is not the one the case started with: nothing was put back, and the case did not finish.");

                // Every restoring write is tried, and every answer is looked at; then the card is read again, and it must be as it was.
                var left = await PutTheMadBackAsync(device, madSectors, madKeyA, madKeyB);

                left.AddRange(await FactoryProblemsAsync(device, madSectors));

                Assert.True(left.Count == 0, "The card could not be put back completely. What is left: " + string.Join("; ", left) + ". See CLASSIC.md for how to put the card back.");
            }
        }

        // The sectors that the MAD case changes the trailers of: sector 0, and sector 16 on a chip that has the sector after it (as the writer decides).
        private static int[] MadSectorsOf(ClassicCardSize size) =>
            ClassicLayout.HasSector(size, ClassicMad.Mad2Sector + 1) ? new[] { 0, ClassicMad.Mad2Sector } : new[] { 0 };

        // The blocks that the MAD case changes besides the trailers: the blocks of the directory in the MAD sectors, and the two blocks of the data.
        private static IEnumerable<int> BlocksOfTheMadCase(int[] madSectors) =>
            new[] { 1, 2, 64, 65, 66 }.Where(block => madSectors.Contains(ClassicLayout.SectorOf(block))).Concat(new[] { 4, 5 });

        // What is not as the put-back writes it: a trailer that is not the factory trailer (the access bits, the general purpose byte, key B, which
        // the factory access bits let key A read) and a block of the case that holds anything but zeros. Each is a sentence that says which and why.
        private static async Task<List<string>> FactoryProblemsAsync(ReaderDevice device, int[] madSectors)
        {
            var problems = new List<string>();
            var factoryTail = ClassicAccessBits.Transport.BuildTrailer(Convert.FromHexString(Factory), 0x69, Convert.FromHexString(Factory)).AsSpan(6).ToArray();

            foreach (var sector in madSectors.Concat(new[] { WriteSector }).Distinct())
            {
                var read = await device.ReadMifareClassicSectorAsync(sector, Factory, Factory);

                if (read.Error != ERROR.NoError)
                {
                    problems.Add($"sector {sector} cannot be read with the factory key (answer {read.Error})");

                    continue;
                }

                var trailer = read.Sector.DataBlock[^1].Data;

                if (trailer == null || !trailer.AsSpan(6).SequenceEqual(factoryTail))
                {
                    problems.Add($"the trailer of sector {sector} is not the factory trailer (its access bits, general purpose byte or key B differ)");
                }

                foreach (var block in BlocksOfTheMadCase(madSectors).Where(block => ClassicLayout.SectorOf(block) == sector))
                {
                    var data = read.Sector.DataBlock.FirstOrDefault(b => b.DataBlockNumberChipBased == block)?.Data;

                    if (data == null || data.Any(b => b != 0))
                    {
                        problems.Add($"block {block} does not hold zeros");
                    }
                }
            }

            return problems;
        }

        // The MAD sectors get the transport access bits and the factory keys again (their data blocks are then writable with the factory key), the
        // blocks of the directory and the data are zeros again. Every write is tried whatever the ones before it said, and what did not work is
        // returned, one sentence each.
        private static async Task<List<string>> PutTheMadBackAsync(ReaderDevice device, int[] madSectors, string madKeyA, string madKeyB)
        {
            var failed = new List<string>();
            var factoryTrailer = ClassicAccessBits.Transport.BuildTrailer(Convert.FromHexString(Factory), 0x69, Convert.FromHexString(Factory));
            var zeros = new byte[ClassicLayout.BytesPerBlock];

            foreach (var sector in madSectors)
            {
                // The MAD trailer lets key B write: the call takes it.
                Note(failed, ClassicLayout.TrailerBlock(sector), await device.WriteMifareClassicSingleBlock(ClassicLayout.TrailerBlock(sector), madKeyA, madKeyB, factoryTrailer));
            }

            foreach (var block in BlocksOfTheMadCase(madSectors))
            {
                Note(failed, block, await device.WriteMifareClassicSingleBlock(block, Factory, Factory, zeros));
            }

            return failed;
        }

        private static void Note(List<string> failed, int block, ERROR answer)
        {
            if (answer != ERROR.NoError)
            {
                failed.Add($"block {block} could not be put back (answer {answer})");
            }
        }


        private static async Task<MifareClassicSectorModel> ReadFactorySectorOrSkipAsync(ReaderDevice device, int sector)
        {
            var read = await device.ReadMifareClassicSectorAsync(sector, Factory, Factory);

            if (read.Error != ERROR.NoError)
            {
                throw SkipException.ForSkip($"Sector {sector} cannot be read with the factory key (answer {read.Error}): the card is not as it left the factory, or the reader does not see it.");
            }

            return read.Sector;
        }

        // Runs the steps, and puts back what they changed whatever came out of them, but only on the card that they started with: a card that was
        // replaced meanwhile gets nothing (the bytes that are put back are those of the other card).
        private static async Task PutBackAfterAsync(Func<Task<ERROR>> putBack, Func<Task<string?>> whoIsOnTheReader, string? uid, Func<Task> steps)
        {
            try
            {
                await steps();
            }
            finally
            {
                Assert.True(await whoIsOnTheReader() == uid, "The card on the reader is not the one the case started with: nothing was put back, and the case did not finish.");

                var restored = await putBack();

                Assert.True(restored == ERROR.NoError, $"The card could not be put back: the answer was {restored}. Check what is on it before you use it.");
            }
        }
    }
}
