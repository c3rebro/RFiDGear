#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>What an access condition lets a key do with a data block (NXP MF1S50 / MF1S70 data sheet, table "access conditions for data blocks").</summary>
    /// <param name="Read">Who may read the block.</param>
    /// <param name="Write">Who may write the block.</param>
    /// <param name="Increment">Who may increment a value block.</param>
    /// <param name="DecrementTransferRestore">Who may decrement, transfer or restore a value block.</param>
    public readonly record struct ClassicDataBlockRights(
        ClassicKeyAccess Read,
        ClassicKeyAccess Write,
        ClassicKeyAccess Increment,
        ClassicKeyAccess DecrementTransferRestore);

    /// <summary>
    /// What an access condition lets a key do with the sector trailer (data sheet, table "access conditions for the sector trailer").
    /// Key A can never be read, whatever the condition, so there is no right for it.
    /// </summary>
    /// <param name="KeyAWrite">Who may write key A.</param>
    /// <param name="AccessBitsRead">Who may read the access bits.</param>
    /// <param name="AccessBitsWrite">Who may write the access bits.</param>
    /// <param name="KeyBRead">Who may read key B. While anybody may, key B is plain data and cannot log in.</param>
    /// <param name="KeyBWrite">Who may write key B.</param>
    public readonly record struct ClassicTrailerRights(
        ClassicKeyAccess KeyAWrite,
        ClassicKeyAccess AccessBitsRead,
        ClassicKeyAccess AccessBitsWrite,
        ClassicKeyAccess KeyBRead,
        ClassicKeyAccess KeyBWrite);

    /// <summary>
    /// The access bits of a sector of a MIFARE Classic chip: for each of the three groups of data blocks and for the sector trailer one
    /// of eight conditions, written the way the data sheet does as <c>C1 C2 C3</c> (the condition number here is
    /// <c>C1 + 2 * C2 + 4 * C3</c>). On the chip they are three bytes of the trailer (bytes 6 to 8), each bit stored once as it is and once
    /// inverted; a trailer whose bits do not match their inverse makes the whole sector unusable, which is why a value of this type can only
    /// be made from a condition that exists or from bytes that are consistent.
    /// <para>
    /// The value does not have a default: use <see cref="Transport"/> for a chip as it leaves the factory.
    /// </para>
    /// </summary>
    public sealed class ClassicAccessBits : IEquatable<ClassicAccessBits>
    {
        /// <summary>The number of groups: three groups of data blocks, and the trailer.</summary>
        internal const int GroupCount = 4;

        /// <summary>The group number of the sector trailer.</summary>
        internal const int TrailerGroup = 3;

        private const int ConditionCount = 8;

        // Index = C1 + 2 * C2 + 4 * C3. The rows are transcribed from the data sheet by the C1 C2 C3 column, and each row is pinned by a test.
        private static readonly ClassicDataBlockRights[] DataRights =
        {
            /* 000 */ new(ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.KeyAOrB),
            /* 100 */ new(ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.KeyB, ClassicKeyAccess.None, ClassicKeyAccess.None),
            /* 010 */ new(ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.None, ClassicKeyAccess.None, ClassicKeyAccess.None),
            /* 110 */ new(ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.KeyB, ClassicKeyAccess.KeyB, ClassicKeyAccess.KeyAOrB),
            /* 001 */ new(ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.None, ClassicKeyAccess.None, ClassicKeyAccess.KeyAOrB),
            /* 101 */ new(ClassicKeyAccess.KeyB, ClassicKeyAccess.None, ClassicKeyAccess.None, ClassicKeyAccess.None),
            /* 011 */ new(ClassicKeyAccess.KeyB, ClassicKeyAccess.KeyB, ClassicKeyAccess.None, ClassicKeyAccess.None),
            /* 111 */ new(ClassicKeyAccess.None, ClassicKeyAccess.None, ClassicKeyAccess.None, ClassicKeyAccess.None)
        };

        private static readonly ClassicTrailerRights[] TrailerConditions =
        {
            /* 000 */ new(ClassicKeyAccess.KeyA, ClassicKeyAccess.KeyA, ClassicKeyAccess.None, ClassicKeyAccess.KeyA, ClassicKeyAccess.KeyA),
            /* 100 */ new(ClassicKeyAccess.KeyB, ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.None, ClassicKeyAccess.None, ClassicKeyAccess.KeyB),
            /* 010 */ new(ClassicKeyAccess.None, ClassicKeyAccess.KeyA, ClassicKeyAccess.None, ClassicKeyAccess.KeyA, ClassicKeyAccess.None),
            /* 110 */ new(ClassicKeyAccess.None, ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.None, ClassicKeyAccess.None, ClassicKeyAccess.None),
            /* 001 */ new(ClassicKeyAccess.KeyA, ClassicKeyAccess.KeyA, ClassicKeyAccess.KeyA, ClassicKeyAccess.KeyA, ClassicKeyAccess.KeyA),
            /* 101 */ new(ClassicKeyAccess.None, ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.KeyB, ClassicKeyAccess.None, ClassicKeyAccess.None),
            /* 011 */ new(ClassicKeyAccess.KeyB, ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.KeyB, ClassicKeyAccess.None, ClassicKeyAccess.KeyB),
            /* 111 */ new(ClassicKeyAccess.None, ClassicKeyAccess.KeyAOrB, ClassicKeyAccess.None, ClassicKeyAccess.None, ClassicKeyAccess.None)
        };

        private readonly int[] conditions;

        /// <summary>Creates the access bits from the conditions of the three groups of data blocks and of the trailer.</summary>
        /// <param name="dataGroup0">The condition of the first group of data blocks, 0 to 7 (<c>C1 + 2 * C2 + 4 * C3</c>).</param>
        /// <param name="dataGroup1">The condition of the second group of data blocks, 0 to 7.</param>
        /// <param name="dataGroup2">The condition of the third group of data blocks, 0 to 7.</param>
        /// <param name="trailer">The condition of the sector trailer, 0 to 7.</param>
        public ClassicAccessBits(int dataGroup0, int dataGroup1, int dataGroup2, int trailer)
        {
            conditions = new[]
            {
                Require(dataGroup0, nameof(dataGroup0)),
                Require(dataGroup1, nameof(dataGroup1)),
                Require(dataGroup2, nameof(dataGroup2)),
                Require(trailer, nameof(trailer))
            };
        }

        /// <summary>Gets the access bits of a chip as it leaves the factory: the data blocks <c>000</c> (both keys may do anything) and the trailer <c>001</c> (key A may do anything, key B is data); the bytes are <c>FF 07 80</c>.</summary>
        public static ClassicAccessBits Transport { get; } = new ClassicAccessBits(0, 0, 0, 4);

        /// <summary>Gets the access bits that the MIFARE Application Directory prescribes for the MAD sectors: the data blocks <c>100</c> (key A reads, key B writes) and the trailer <c>011</c> (key B writes everything); the bytes are <c>78 77 88</c>.</summary>
        public static ClassicAccessBits MadStandard { get; } = new ClassicAccessBits(1, 1, 1, 6);

        /// <summary>Gets the rights of the sector trailer.</summary>
        public ClassicTrailerRights TrailerRights => TrailerConditions[conditions[TrailerGroup]];

        /// <summary>Gets a value that says whether key B is plain data: while a key may read it, it is part of the trailer and cannot log in.</summary>
        public bool KeyBIsData => TrailerRights.KeyBRead != ClassicKeyAccess.None;

        /// <summary>Gets a value that says whether the access bits can never be written again (the condition of the trailer grants nobody a write of the access bits). Writing such bits is for good.</summary>
        public bool AccessBitsAreFinal => TrailerRights.AccessBitsWrite == ClassicKeyAccess.None;

        /// <summary>Gets a value that says whether nobody may write anything in the trailer any more: neither key A, key B nor the access bits.</summary>
        public bool TrailerIsFrozen
        {
            get
            {
                var rights = TrailerRights;

                return rights.KeyAWrite == ClassicKeyAccess.None
                       && rights.KeyBWrite == ClassicKeyAccess.None
                       && rights.AccessBitsWrite == ClassicKeyAccess.None;
            }
        }

        /// <summary>Makes access bits from the three bytes of the trailer (bytes 6 to 8).</summary>
        /// <param name="bytes">The three bytes.</param>
        /// <param name="bits">The access bits, when the bytes are consistent.</param>
        /// <returns><c>false</c> when there are not three bytes, or when a bit is not the inverse of its copy: the trailer is damaged, or it is not a trailer.</returns>
        public static bool TryFromBytes(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out ClassicAccessBits? bits)
        {
            bits = null;

            if (bytes.Length != 3)
            {
                return false;
            }

            int byte6 = bytes[0];
            int byte7 = bytes[1];
            int byte8 = bytes[2];

            var c1 = byte7 >> 4;
            var c2 = byte8 & 0x0F;
            var c3 = byte8 >> 4;

            var consistent = ((~byte6) & 0x0F) == c1
                             && (((~byte6) >> 4) & 0x0F) == c2
                             && ((~byte7) & 0x0F) == c3;

            if (!consistent)
            {
                return false;
            }

            bits = new ClassicAccessBits(
                ConditionOf(c1, c2, c3, 0),
                ConditionOf(c1, c2, c3, 1),
                ConditionOf(c1, c2, c3, 2),
                ConditionOf(c1, c2, c3, 3));

            return true;
        }

        /// <summary>Makes access bits from a sector trailer that was read from a chip.</summary>
        /// <param name="trailer">The 16 bytes of the trailer.</param>
        /// <param name="bits">The access bits, when the trailer is consistent.</param>
        /// <returns><c>false</c> when it is not 16 bytes, or when the access bits are inconsistent.</returns>
        public static bool TryFromTrailer(ReadOnlySpan<byte> trailer, [NotNullWhen(true)] out ClassicAccessBits? bits)
        {
            if (trailer.Length != ClassicLayout.BytesPerBlock)
            {
                bits = null;

                return false;
            }

            return TryFromBytes(trailer.Slice(6, 3), out bits);
        }

        /// <summary>Tells the condition of a group.</summary>
        /// <param name="group">0 to 2 for the groups of data blocks, 3 for the trailer (see <see cref="ClassicLayout.AccessGroup(int)"/>).</param>
        /// <returns>The condition, 0 to 7 (<c>C1 + 2 * C2 + 4 * C3</c>).</returns>
        public int GetCondition(int group)
        {
            if (group < 0 || group >= GroupCount)
            {
                throw new ArgumentOutOfRangeException(nameof(group), group, "A group is 0 to 2 (data blocks) or 3 (the trailer).");
            }

            return conditions[group];
        }

        /// <summary>Tells what the keys may do with the data blocks of a group.</summary>
        /// <param name="group">0 to 2.</param>
        /// <returns>The rights.</returns>
        public ClassicDataBlockRights GetDataBlockRights(int group)
        {
            if (group < 0 || group >= TrailerGroup)
            {
                throw new ArgumentOutOfRangeException(nameof(group), group, "A group of data blocks is 0 to 2.");
            }

            return DataRights[conditions[group]];
        }

        /// <summary>Tells whether a key can log in to the sector at all: key A always can, key B only while no key may read it.</summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> when logging in with the key can work.</returns>
        public bool AllowsLoginWith(ClassicKeyType key) => key == ClassicKeyType.KeyA || !KeyBIsData;

        /// <summary>
        /// Chooses the key for an operation: key A when it is allowed and available, otherwise key B when it is allowed, available and able to
        /// log in. The caller passes what the condition grants for the operation (for example <see cref="ClassicDataBlockRights.Write"/>).
        /// </summary>
        /// <param name="allowed">Who the condition lets do it.</param>
        /// <param name="hasKeyA">Whether the caller has key A.</param>
        /// <param name="hasKeyB">Whether the caller has key B.</param>
        /// <param name="key">The key to log in with.</param>
        /// <returns><c>false</c> when none of the keys that the caller has can do it.</returns>
        public bool TryChooseKey(ClassicKeyAccess allowed, bool hasKeyA, bool hasKeyB, out ClassicKeyType key)
        {
            if (hasKeyA && allowed.Allows(ClassicKeyType.KeyA))
            {
                key = ClassicKeyType.KeyA;

                return true;
            }

            if (hasKeyB && allowed.Allows(ClassicKeyType.KeyB) && AllowsLoginWith(ClassicKeyType.KeyB))
            {
                key = ClassicKeyType.KeyB;

                return true;
            }

            key = default;

            return false;
        }

        /// <summary>Gives the three bytes of the trailer (bytes 6 to 8), with every bit followed by its inverse as the chip wants it.</summary>
        /// <returns>A new array of three bytes.</returns>
        public byte[] ToBytes()
        {
            var c1 = 0;
            var c2 = 0;
            var c3 = 0;

            for (var group = 0; group < GroupCount; group++)
            {
                var condition = conditions[group];

                c1 |= (condition & 1) << group;
                c2 |= ((condition >> 1) & 1) << group;
                c3 |= ((condition >> 2) & 1) << group;
            }

            return new[]
            {
                (byte)((((~c2) & 0x0F) << 4) | ((~c1) & 0x0F)),
                (byte)((c1 << 4) | ((~c3) & 0x0F)),
                (byte)((c3 << 4) | c2)
            };
        }

        /// <summary>Builds the 16 bytes of a sector trailer.</summary>
        /// <param name="keyA">Key A, 6 bytes.</param>
        /// <param name="generalPurposeByte">The general purpose byte (byte 9).</param>
        /// <param name="keyB">Key B, 6 bytes.</param>
        /// <returns>A new array of 16 bytes: key A, these access bits, the general purpose byte, key B.</returns>
        public byte[] BuildTrailer(ReadOnlySpan<byte> keyA, byte generalPurposeByte, ReadOnlySpan<byte> keyB)
        {
            if (keyA.Length != 6)
            {
                throw new ArgumentException("Key A is 6 bytes.", nameof(keyA));
            }

            if (keyB.Length != 6)
            {
                throw new ArgumentException("Key B is 6 bytes.", nameof(keyB));
            }

            var trailer = new byte[ClassicLayout.BytesPerBlock];

            keyA.CopyTo(trailer);
            ToBytes().CopyTo(trailer, 6);
            trailer[9] = generalPurposeByte;
            keyB.CopyTo(trailer.AsSpan(10));

            return trailer;
        }

        /// <summary>Whether two access bits are the same.</summary>
        /// <param name="other">The other access bits.</param>
        /// <returns><c>true</c> when all four conditions are the same.</returns>
        public bool Equals(ClassicAccessBits? other) =>
            other is not null && conditions.AsSpan().SequenceEqual(other.conditions);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ClassicAccessBits other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(conditions[0], conditions[1], conditions[2], conditions[3]);

        /// <summary>Whether two access bits are the same.</summary>
        /// <param name="left">The first access bits.</param>
        /// <param name="right">The second access bits.</param>
        /// <returns><c>true</c> when they are equal.</returns>
        public static bool operator ==(ClassicAccessBits? left, ClassicAccessBits? right) => left is null ? right is null : left.Equals(right);

        /// <summary>Whether two access bits differ.</summary>
        /// <param name="left">The first access bits.</param>
        /// <param name="right">The second access bits.</param>
        /// <returns><c>true</c> when they are not equal.</returns>
        public static bool operator !=(ClassicAccessBits? left, ClassicAccessBits? right) => !(left == right);

        /// <summary>Gives the three bytes in hexadecimal, for example <c>FF0780</c>.</summary>
        /// <returns>The text.</returns>
        public override string ToString() => Convert.ToHexString(ToBytes());

        private static int Require(int condition, string name)
        {
            if (condition < 0 || condition >= ConditionCount)
            {
                throw new ArgumentOutOfRangeException(name, condition, "A condition is 0 to 7 (C1 + 2 * C2 + 4 * C3).");
            }

            return condition;
        }

        private static int ConditionOf(int c1, int c2, int c3, int group) =>
            ((c1 >> group) & 1) | (((c2 >> group) & 1) << 1) | (((c3 >> group) & 1) << 2);
    }
}
