#nullable enable
using RFiDGear.Infrastructure.AccessControl;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>
    /// What the block selection of the MIFARE Classic Data Explorer names inside a sector. The selection offers "Block 0", "Block 1",
    /// "Block 2" and "Sector Trailer" (the value <see cref="DataExplorer_DataBlock.Block3"/>). The first three are the data blocks
    /// 0 to 2 of any sector. The trailer is the last block of the sector: the block 3 of a sector of 4 blocks (sectors 0 to 31), and
    /// the block 15 of a sector of 16 blocks (sectors 32 to 39), where the block 3 is a data block.
    /// </summary>
    internal static class DataExplorerBlocks
    {
        /// <summary>Tells which block of a sector (counted from the first block of the sector) the selection names.</summary>
        /// <param name="selection">The selection of the dialog.</param>
        /// <param name="sector">The sector the dialog is on (0 to 39). A number that is no sector is taken as one of 4 blocks.</param>
        /// <returns>The block number within the sector: 0 to 3, or 0 to 15 for the trailer of a sector of 16 blocks.</returns>
        internal static int IndexInSector(DataExplorer_DataBlock selection, int sector)
        {
            if (selection != DataExplorer_DataBlock.Block3)
            {
                return (int)selection;
            }

            var blocks = sector is >= ClassicLayout.FirstLargeSector and <= ClassicLayout.MaxSector
                ? ClassicLayout.LargeSectorBlocks
                : ClassicLayout.SmallSectorBlocks;

            return blocks - 1;
        }
    }
}
