#nullable enable
using System;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>The sizes of MIFARE Classic that this application knows.</summary>
    public enum ClassicCardSize
    {
        /// <summary>MIFARE Classic Mini: 5 sectors of 4 blocks (320 bytes).</summary>
        Mini = 1,

        /// <summary>MIFARE Classic 1K: 16 sectors of 4 blocks.</summary>
        K1 = 2,

        /// <summary>MIFARE Classic 2K (and Plus SL1 2K): 32 sectors of 4 blocks.</summary>
        K2 = 3,

        /// <summary>MIFARE Classic 4K (and Plus SL1 4K): 32 sectors of 4 blocks, then 8 sectors of 16 blocks.</summary>
        K4 = 4
    }

    /// <summary>
    /// Where the blocks and sectors of a MIFARE Classic chip are: the one place of the arithmetic. Sectors 0 to 31 have 4 blocks (3 data
    /// blocks and the sector trailer), the sectors 32 to 39 of a 4K have 16 blocks (15 data blocks and the trailer). The numbers here are
    /// the numbers of the chip: block 0 is the manufacturer block, and a block number is the one that goes to the card, whatever a reader
    /// makes of it (the TWN4 counts the sectors from 32 on in steps of 4, which is the business of its provider, not of this class).
    /// </summary>
    public static class ClassicLayout
    {
        /// <summary>The size of a block in bytes.</summary>
        internal const int BytesPerBlock = 16;

        /// <summary>The number of blocks of a sector below sector 32.</summary>
        internal const int SmallSectorBlocks = 4;

        /// <summary>The number of blocks of a sector from sector 32 on.</summary>
        internal const int LargeSectorBlocks = 16;

        /// <summary>The first sector with 16 blocks.</summary>
        internal const int FirstLargeSector = 32;

        /// <summary>The first block of the first sector with 16 blocks.</summary>
        internal const int FirstLargeBlock = 128;

        /// <summary>The highest sector number of any chip (a 4K has the sectors 0 to 39).</summary>
        internal const int MaxSector = 39;

        /// <summary>The highest block number of any chip.</summary>
        internal const int MaxBlock = 255;

        /// <summary>Tells how many sectors a chip of the size has.</summary>
        /// <param name="size">The size of the chip.</param>
        /// <returns>5, 16, 32 or 40.</returns>
        public static int SectorCount(ClassicCardSize size) => size switch
        {
            ClassicCardSize.Mini => 5,
            ClassicCardSize.K1 => 16,
            ClassicCardSize.K2 => 32,
            ClassicCardSize.K4 => 40,
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Not a known size of a MIFARE Classic chip.")
        };

        /// <summary>Tells how many blocks a chip of the size has.</summary>
        /// <param name="size">The size of the chip.</param>
        /// <returns>20, 64, 128 or 256.</returns>
        public static int BlockCount(ClassicCardSize size) => size switch
        {
            ClassicCardSize.Mini => 20,
            ClassicCardSize.K1 => 64,
            ClassicCardSize.K2 => 128,
            ClassicCardSize.K4 => 256,
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Not a known size of a MIFARE Classic chip.")
        };

        /// <summary>Maps the chip type that a reader reports to the size of a Classic chip.</summary>
        /// <param name="type">The type of the chip.</param>
        /// <param name="size">The size, when the type is a MIFARE Classic chip that this class knows.</param>
        /// <returns><c>true</c> for a Classic chip (or a Plus in security level 1, which answers like one), <c>false</c> for everything else.</returns>
        public static bool TryGetSize(CARD_TYPE type, out ClassicCardSize size)
        {
            switch (type)
            {
                case CARD_TYPE.MifareMini:
                    size = ClassicCardSize.Mini;
                    return true;
                case CARD_TYPE.Mifare1K:
                case CARD_TYPE.MifarePlus_SL1_1K:
                    size = ClassicCardSize.K1;
                    return true;
                case CARD_TYPE.Mifare2K:
                case CARD_TYPE.MifarePlus_SL1_2K:
                    size = ClassicCardSize.K2;
                    return true;
                case CARD_TYPE.Mifare4K:
                case CARD_TYPE.MifarePlus_SL1_4K:
                    size = ClassicCardSize.K4;
                    return true;
                default:
                    size = default;
                    return false;
            }
        }

        /// <summary>Tells whether the chip of the size has the sector.</summary>
        /// <param name="size">The size of the chip.</param>
        /// <param name="sector">The sector number.</param>
        /// <returns><c>true</c> when the sector exists on such a chip.</returns>
        public static bool HasSector(ClassicCardSize size, int sector) => sector >= 0 && sector < SectorCount(size);

        /// <summary>Tells whether the chip of the size has the block.</summary>
        /// <param name="size">The size of the chip.</param>
        /// <param name="block">The block number.</param>
        /// <returns><c>true</c> when the block exists on such a chip.</returns>
        public static bool HasBlock(ClassicCardSize size, int block) => block >= 0 && block < BlockCount(size);

        /// <summary>Tells how many blocks a sector has, the trailer included.</summary>
        /// <param name="sector">The sector number, 0 to 39.</param>
        /// <returns>4 or 16.</returns>
        public static int BlocksInSector(int sector)
        {
            RequireSector(sector);

            return sector < FirstLargeSector ? SmallSectorBlocks : LargeSectorBlocks;
        }

        /// <summary>Tells the number of the first block of a sector.</summary>
        /// <param name="sector">The sector number, 0 to 39.</param>
        /// <returns>The block number, 0 to 240.</returns>
        public static int FirstBlock(int sector)
        {
            RequireSector(sector);

            return sector < FirstLargeSector
                ? sector * SmallSectorBlocks
                : FirstLargeBlock + ((sector - FirstLargeSector) * LargeSectorBlocks);
        }

        /// <summary>Tells the number of the sector trailer (the last block) of a sector.</summary>
        /// <param name="sector">The sector number, 0 to 39.</param>
        /// <returns>The block number of the trailer.</returns>
        public static int TrailerBlock(int sector) => FirstBlock(sector) + BlocksInSector(sector) - 1;

        /// <summary>Tells how many blocks of a sector are no trailer: the blocks that hold data (block 0 of sector 0 holds the manufacturer data).</summary>
        /// <param name="sector">The sector number, 0 to 39.</param>
        /// <returns>3 or 15.</returns>
        public static int DataBlocksInSector(int sector) => BlocksInSector(sector) - 1;

        /// <summary>Tells in which sector a block is.</summary>
        /// <param name="block">The block number, 0 to 255.</param>
        /// <returns>The sector number.</returns>
        public static int SectorOf(int block)
        {
            RequireBlock(block);

            return block < FirstLargeBlock
                ? block / SmallSectorBlocks
                : FirstLargeSector + ((block - FirstLargeBlock) / LargeSectorBlocks);
        }

        /// <summary>Tells the position of a block in its sector (0 is the first block of the sector).</summary>
        /// <param name="block">The block number, 0 to 255.</param>
        /// <returns>The position.</returns>
        public static int BlockInSector(int block) => block - FirstBlock(SectorOf(block));

        /// <summary>Tells whether a block is a sector trailer: it holds the two keys and the access bits.</summary>
        /// <param name="block">The block number, 0 to 255.</param>
        /// <returns><c>true</c> for the last block of a sector.</returns>
        public static bool IsTrailer(int block) => block == TrailerBlock(SectorOf(block));

        /// <summary>Tells whether a block is the manufacturer block (block 0 holds the UID and cannot be written on a genuine chip).</summary>
        /// <param name="block">The block number, 0 to 255.</param>
        /// <returns><c>true</c> for block 0.</returns>
        public static bool IsManufacturerBlock(int block)
        {
            RequireBlock(block);

            return block == 0;
        }

        /// <summary>
        /// Tells which access condition a block follows. A sector of 4 blocks has one condition per block (0 to 2) and one for the trailer
        /// (3). A sector of 16 blocks has one per group of 5 blocks (0 to 4, 5 to 9, 10 to 14) and one for the trailer.
        /// </summary>
        /// <param name="block">The block number, 0 to 255.</param>
        /// <returns>0, 1 or 2 for a data block, 3 for the trailer.</returns>
        public static int AccessGroup(int block)
        {
            if (IsTrailer(block))
            {
                return 3;
            }

            var position = BlockInSector(block);

            return SectorOf(block) < FirstLargeSector ? position : position / 5;
        }

        private static void RequireSector(int sector)
        {
            if (sector < 0 || sector > MaxSector)
            {
                throw new ArgumentOutOfRangeException(nameof(sector), sector, "A MIFARE Classic sector is a number from 0 to 39.");
            }
        }

        private static void RequireBlock(int block)
        {
            if (block < 0 || block > MaxBlock)
            {
                throw new ArgumentOutOfRangeException(nameof(block), block, "A MIFARE Classic block is a number from 0 to 255.");
            }
        }
    }
}
