#nullable enable
using System;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>Why a write to a block or a sector is not allowed (before any chip is asked).</summary>
    public enum ClassicWriteViolation
    {
        /// <summary>Nothing: the write is allowed.</summary>
        None = 0,

        /// <summary>The chip has no such block or sector (or no chip has).</summary>
        OutsideTheChip = 1,

        /// <summary>There is no data, or not the number of bytes that the write takes (16 for a block, the data blocks of the sector for a sector).</summary>
        WrongLength = 2,

        /// <summary>The block is the manufacturer block (block 0: the UID and the manufacturer data), and the write did not opt in to writing it.</summary>
        ManufacturerBlock = 3,

        /// <summary>The block is a sector trailer (the keys and the access bits), and the write did not opt in to writing it.</summary>
        TrailerNotAllowed = 4,

        /// <summary>The bits of the trailer that was to be written are not the inverse of their copy: a trailer like that makes the sector unusable for good, and no option allows it.</summary>
        AccessBitsNotConsistent = 5,

        /// <summary>The access bits that were to be written can never be changed again, and the write did not opt in to that.</summary>
        AccessBitsAreFinal = 6,

        /// <summary>The access bits of the sector could not be read from the chip (the trailer is damaged), so the key that may write cannot be told.</summary>
        AccessBitsUnknown = 7
    }

    /// <summary>
    /// What a write is allowed to touch beyond the data blocks. The default touches data blocks only: the manufacturer block and the sector
    /// trailers are the places where a wrong write costs a chip or a sector for good, so each needs its own say-so, and the access bits that
    /// can never be changed again need one more.
    /// </summary>
    public sealed class ClassicWriteOptions
    {
        /// <summary>Gets the options of a write that touches data blocks only.</summary>
        public static ClassicWriteOptions DataOnly { get; } = new ClassicWriteOptions();

        /// <summary>Gets a value that says whether block 0 may be written (it cannot on a genuine chip; some copies of chips allow it).</summary>
        public bool AllowManufacturerBlock { get; init; }

        /// <summary>Gets a value that says whether a sector trailer may be written. Its access bits are checked first (see <see cref="ClassicAccessBits"/>).</summary>
        public bool AllowTrailer { get; init; }

        /// <summary>Gets a value that says whether access bits may be written that nobody can change again (the condition of the trailer grants no write of the access bits).</summary>
        public bool AllowFinalAccessBits { get; init; }
    }

    /// <summary>The answer of <see cref="ClassicWritePolicy"/>.</summary>
    /// <param name="Violation">Why the write is not allowed, or <see cref="ClassicWriteViolation.None"/>.</param>
    public readonly record struct ClassicWriteDecision(ClassicWriteViolation Violation)
    {
        /// <summary>Gets a value that says whether the write is allowed.</summary>
        public bool IsAllowed => Violation == ClassicWriteViolation.None;
    }

    /// <summary>
    /// The one place that decides whether data may be written at all: what is checked here needs no chip, so a request that is refused here
    /// touches none. A sector write takes the data blocks of the sector and never its trailer; a block write may take any block, if the options say so.
    /// </summary>
    public static class ClassicWritePolicy
    {
        /// <summary>Tells how many bytes a sector write takes: the data blocks of the sector (the manufacturer block of sector 0 only when it may be written).</summary>
        /// <param name="sector">The sector, 0 to 39.</param>
        /// <param name="options">What may be written.</param>
        /// <returns>The number of bytes: 32 for sector 0, 48 for the other small sectors, 240 for the large ones (plus 16 for sector 0 with the manufacturer block).</returns>
        public static int SectorDataLength(int sector, ClassicWriteOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var blocks = ClassicLayout.DataBlocksInSector(sector);

            if (sector == 0 && !options.AllowManufacturerBlock)
            {
                blocks--;
            }

            return blocks * ClassicLayout.BytesPerBlock;
        }

        /// <summary>Decides whether one block may be written.</summary>
        /// <param name="size">The size of the chip.</param>
        /// <param name="block">The block, as the chip numbers it.</param>
        /// <param name="data">The 16 bytes that were to be written.</param>
        /// <param name="options">What may be written.</param>
        /// <returns>The decision.</returns>
        public static ClassicWriteDecision CheckBlock(ClassicCardSize size, int block, byte[]? data, ClassicWriteOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!ClassicLayout.HasBlock(size, block))
            {
                return Refuse(ClassicWriteViolation.OutsideTheChip);
            }

            if (data is not { Length: ClassicLayout.BytesPerBlock })
            {
                return Refuse(ClassicWriteViolation.WrongLength);
            }

            if (ClassicLayout.IsManufacturerBlock(block))
            {
                return options.AllowManufacturerBlock ? Allow() : Refuse(ClassicWriteViolation.ManufacturerBlock);
            }

            return ClassicLayout.IsTrailer(block) ? CheckTrailer(data, options) : Allow();
        }

        /// <summary>Decides whether the data blocks of one sector may be written.</summary>
        /// <param name="size">The size of the chip.</param>
        /// <param name="sector">The sector, 0 to 39.</param>
        /// <param name="data">The bytes that were to be written (see <see cref="SectorDataLength(int, ClassicWriteOptions)"/>).</param>
        /// <param name="options">What may be written.</param>
        /// <returns>The decision.</returns>
        public static ClassicWriteDecision CheckSector(ClassicCardSize size, int sector, byte[]? data, ClassicWriteOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!ClassicLayout.HasSector(size, sector))
            {
                return Refuse(ClassicWriteViolation.OutsideTheChip);
            }

            return data != null && data.Length == SectorDataLength(sector, options)
                ? Allow()
                : Refuse(ClassicWriteViolation.WrongLength);
        }

        /// <summary>
        /// Decides whether one block may be written at all, before the card is asked what it is: everything that does not depend on the size of the
        /// chip (the data, block 0, a trailer and its access bits), and the blocks that no chip has. A chip that is smaller refuses the block later.
        /// </summary>
        /// <param name="block">The block, as the chip numbers it.</param>
        /// <param name="data">The 16 bytes that were to be written.</param>
        /// <param name="options">What may be written.</param>
        /// <returns>The decision.</returns>
        public static ClassicWriteDecision CheckBlock(int block, byte[]? data, ClassicWriteOptions options) =>
            CheckBlock(ClassicCardSize.K4, block, data, options);

        /// <summary>Decides whether the data blocks of one sector may be written at all, before the card is asked what it is (see <see cref="CheckBlock(int, byte[], ClassicWriteOptions)"/>).</summary>
        /// <param name="sector">The sector, 0 to 39.</param>
        /// <param name="data">The bytes that were to be written (see <see cref="SectorDataLength(int, ClassicWriteOptions)"/>).</param>
        /// <param name="options">What may be written.</param>
        /// <returns>The decision.</returns>
        public static ClassicWriteDecision CheckSector(int sector, byte[]? data, ClassicWriteOptions options) =>
            CheckSector(ClassicCardSize.K4, sector, data, options);

        private static ClassicWriteDecision CheckTrailer(byte[] data, ClassicWriteOptions options)
        {
            if (!options.AllowTrailer)
            {
                return Refuse(ClassicWriteViolation.TrailerNotAllowed);
            }

            if (!ClassicAccessBits.TryFromTrailer(data, out var bits))
            {
                return Refuse(ClassicWriteViolation.AccessBitsNotConsistent);
            }

            return bits.AccessBitsAreFinal && !options.AllowFinalAccessBits
                ? Refuse(ClassicWriteViolation.AccessBitsAreFinal)
                : Allow();
        }

        private static ClassicWriteDecision Allow() => new ClassicWriteDecision(ClassicWriteViolation.None);

        private static ClassicWriteDecision Refuse(ClassicWriteViolation violation) => new ClassicWriteDecision(violation);
    }
}
