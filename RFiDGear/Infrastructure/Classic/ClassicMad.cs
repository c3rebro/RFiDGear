#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>The version of the MIFARE Application Directory (MAD) of a chip.</summary>
    public enum ClassicMadVersion
    {
        /// <summary>MAD1: sector 0 holds the directory for the sectors 1 to 15 (a 1K, or a 4K that uses only the first 15 sectors).</summary>
        Mad1 = 1,

        /// <summary>MAD2: sector 16 holds a second directory for the sectors 17 to 39 (a 4K).</summary>
        Mad2 = 2
    }

    /// <summary>
    /// The application identifier (AID) of the MAD: two bytes, the function cluster code in the high byte and the application code in the
    /// low byte. On the chip the low byte comes first: the AID <c>E103</c> of an NDEF sector is stored as <c>03 E1</c>.
    /// </summary>
    public static class ClassicMadAid
    {
        /// <summary>The sector is free.</summary>
        internal const ushort Free = 0x0000;

        /// <summary>The sector is defect.</summary>
        internal const ushort Defect = 0x0001;

        /// <summary>The sector is reserved.</summary>
        internal const ushort Reserved = 0x0002;

        /// <summary>The sector holds further directory information (it continues the directory).</summary>
        internal const ushort Continuation = 0x0003;

        /// <summary>The sector holds card holder information.</summary>
        internal const ushort CardHolderInformation = 0x0004;

        /// <summary>The sector is not applicable (it lies beyond the memory of the chip).</summary>
        internal const ushort NotApplicable = 0x0005;

        /// <summary>The AID of an NFC Forum NDEF application.</summary>
        internal const ushort NdefApplication = 0xE103;

        /// <summary>Makes an AID from its two parts.</summary>
        /// <param name="functionCluster">The function cluster code (the high byte).</param>
        /// <param name="application">The application code (the low byte).</param>
        /// <returns>The AID.</returns>
        public static ushort Create(byte functionCluster, byte application) => (ushort)((functionCluster << 8) | application);

        /// <summary>Gives the function cluster code of an AID.</summary>
        /// <param name="aid">The AID.</param>
        /// <returns>The high byte.</returns>
        public static byte FunctionCluster(ushort aid) => (byte)(aid >> 8);

        /// <summary>Gives the application code of an AID.</summary>
        /// <param name="aid">The AID.</param>
        /// <returns>The low byte.</returns>
        public static byte Application(ushort aid) => (byte)(aid & 0xFF);
    }

    /// <summary>What the general purpose byte (GPB) of a MAD sector trailer says.</summary>
    /// <param name="MadAvailable">The DA bit: the chip has a MAD.</param>
    /// <param name="MultiApplication">The MA bit: the chip holds more than one application.</param>
    /// <param name="Version">The version of the MAD.</param>
    public readonly record struct ClassicMadGpbInfo(bool MadAvailable, bool MultiApplication, ClassicMadVersion Version);

    /// <summary>The general purpose byte of the trailer of a MAD sector: bit 7 DA (a MAD is there), bit 6 MA (more than one application), bits 5 to 2 reserved (0), bits 1 and 0 the version (01 MAD1, 10 MAD2).</summary>
    public static class ClassicMadGpb
    {
        private const int DataAvailableBit = 0x80;
        private const int MultiApplicationBit = 0x40;
        private const int ReservedBits = 0x3C;
        private const int VersionBits = 0x03;

        /// <summary>Makes the general purpose byte of a chip that has a MAD.</summary>
        /// <param name="version">The version of the MAD.</param>
        /// <param name="multiApplication">Whether the chip holds more than one application.</param>
        /// <returns>For example <c>C1</c> for a MAD1 of a multi-application chip.</returns>
        public static byte Create(ClassicMadVersion version, bool multiApplication)
        {
            if (version != ClassicMadVersion.Mad1 && version != ClassicMadVersion.Mad2)
            {
                throw new ArgumentOutOfRangeException(nameof(version), version, "Not a version of the MAD.");
            }

            return (byte)(DataAvailableBit | (multiApplication ? MultiApplicationBit : 0) | (int)version);
        }

        /// <summary>Reads a general purpose byte.</summary>
        /// <param name="gpb">The byte.</param>
        /// <param name="info">What it says, when it is a valid one.</param>
        /// <returns><c>false</c> when a reserved bit is set or the version bits are neither 01 nor 10: the byte is no MAD byte (a chip as it leaves the factory has <c>69</c> there).</returns>
        public static bool TryParse(byte gpb, out ClassicMadGpbInfo info)
        {
            var version = gpb & VersionBits;

            if ((gpb & ReservedBits) != 0 || (version != (int)ClassicMadVersion.Mad1 && version != (int)ClassicMadVersion.Mad2))
            {
                info = default;

                return false;
            }

            info = new ClassicMadGpbInfo((gpb & DataAvailableBit) != 0, (gpb & MultiApplicationBit) != 0, (ClassicMadVersion)version);

            return true;
        }
    }

    /// <summary>What is wrong with the bytes of a MAD that were read.</summary>
    [Flags]
    public enum ClassicMadProblems
    {
        /// <summary>Nothing.</summary>
        None = 0,

        /// <summary>The CRC byte of MAD1 is not the CRC of its content.</summary>
        Mad1CrcMismatch = 1,

        /// <summary>The CRC byte of MAD2 is not the CRC of its content.</summary>
        Mad2CrcMismatch = 2
    }

    /// <summary>
    /// The MIFARE Application Directory: for each sector from 1 on (except sector 16, which holds MAD2) the AID of the application that
    /// owns it. MAD1 is the 32 bytes of the blocks 1 and 2 of sector 0 (CRC, info byte, then the AIDs of the sectors 1 to 15); MAD2 is the 48
    /// bytes of the blocks 0 to 2 of sector 16, which are blocks 64 to 66 (CRC, info byte, then the AIDs of the sectors 17 to 39). Each AID
    /// is two bytes, the low byte first. The CRC is an 8-bit CRC (polynomial 1D, start value C7) over all bytes after the CRC byte.
    /// <para>
    /// This class is the format only. It reads and writes no chip, and it never trusts a chip: <see cref="Problems"/> says what was wrong
    /// with the bytes that were read, and the bytes that it makes always carry the right CRC. The keys of the MAD sectors and their access
    /// bits (<see cref="ClassicAccessBits.MadStandard"/>) are the business of the operation that writes them.
    /// </para>
    /// </summary>
    public sealed class ClassicMad
    {
        /// <summary>The length of MAD1 in bytes (blocks 1 and 2 of sector 0).</summary>
        internal const int Mad1Length = 32;

        /// <summary>The length of MAD2 in bytes (blocks 0 to 2 of sector 16).</summary>
        internal const int Mad2Length = 48;

        /// <summary>The sector of MAD1.</summary>
        internal const int Mad1Sector = 0;

        /// <summary>The sector of MAD2.</summary>
        internal const int Mad2Sector = 16;

        /// <summary>The first block of MAD1 (it has two blocks).</summary>
        internal const int Mad1FirstBlock = 1;

        /// <summary>The first block of MAD2 (it has three blocks).</summary>
        internal const int Mad2FirstBlock = 64;

        /// <summary>The sector trailer of the MAD1 sector.</summary>
        internal const int Mad1TrailerBlock = 3;

        /// <summary>The sector trailer of the MAD2 sector.</summary>
        internal const int Mad2TrailerBlock = 67;

        /// <summary>The key A that the MAD prescribes for the MAD sectors, as 12 hexadecimal digits. It is public: it is only for reading.</summary>
        internal const string DefaultKeyA = "A0A1A2A3A4A5";

        /// <summary>The key A of a sector that is formatted for NFC Forum NDEF, as 12 hexadecimal digits. It is public.</summary>
        internal const string NfcForumKeyA = "D3F7D3F7D3F7";

        private const int CrcStart = 0xC7;
        private const int CrcPolynomial = 0x1D;
        private const int FirstMad1Sector = 1;
        private const int LastMad1Sector = 15;
        private const int FirstMad2Sector = 17;

        private readonly ushort[] aids = new ushort[ClassicLayout.MaxSector + 1];

        private ClassicMad(ClassicMadVersion version, ClassicMadProblems problems)
        {
            Version = version;
            Problems = problems;
        }

        /// <summary>Gets the version: MAD2 when the chip has the second directory.</summary>
        public ClassicMadVersion Version { get; }

        /// <summary>Gets what was wrong with the bytes that this directory was read from; <see cref="ClassicMadProblems.None"/> for a directory that was made here.</summary>
        public ClassicMadProblems Problems { get; }

        /// <summary>Gets or sets the info byte of MAD1: the number of the sector that holds the card holder information (1 to 15), or 0 for none.</summary>
        public byte InfoByte1 { get; set; }

        /// <summary>Gets or sets the info byte of MAD2: the number of the sector that holds the card holder information (17 to 39), or 0 for none. It is only written for <see cref="ClassicMadVersion.Mad2"/>.</summary>
        public byte InfoByte2 { get; set; }

        /// <summary>Makes an empty directory: every sector free, both info bytes 0.</summary>
        /// <param name="version">The version.</param>
        /// <returns>The directory.</returns>
        public static ClassicMad Create(ClassicMadVersion version)
        {
            if (version != ClassicMadVersion.Mad1 && version != ClassicMadVersion.Mad2)
            {
                throw new ArgumentOutOfRangeException(nameof(version), version, "Not a version of the MAD.");
            }

            return new ClassicMad(version, ClassicMadProblems.None);
        }

        /// <summary>Reads a directory from the bytes of a chip.</summary>
        /// <param name="mad1">The 32 bytes of the blocks 1 and 2 of sector 0.</param>
        /// <param name="mad2">The 48 bytes of the blocks 64 to 66, or an empty span for a chip that is read as MAD1 only.</param>
        /// <param name="mad">The directory. A wrong CRC does not stop it: it is in <see cref="Problems"/>, and the caller decides what to do.</param>
        /// <returns><c>false</c> when a span has a length that no MAD has.</returns>
        public static bool TryParse(ReadOnlySpan<byte> mad1, ReadOnlySpan<byte> mad2, [NotNullWhen(true)] out ClassicMad? mad)
        {
            mad = null;

            if (mad1.Length != Mad1Length || (mad2.Length != 0 && mad2.Length != Mad2Length))
            {
                return false;
            }

            var problems = ClassicMadProblems.None;

            if (mad1[0] != ComputeCrc(mad1))
            {
                problems |= ClassicMadProblems.Mad1CrcMismatch;
            }

            if (mad2.Length != 0 && mad2[0] != ComputeCrc(mad2))
            {
                problems |= ClassicMadProblems.Mad2CrcMismatch;
            }

            var result = new ClassicMad(mad2.Length == 0 ? ClassicMadVersion.Mad1 : ClassicMadVersion.Mad2, problems)
            {
                InfoByte1 = mad1[1]
            };

            for (var sector = FirstMad1Sector; sector <= LastMad1Sector; sector++)
            {
                result.aids[sector] = ReadAid(mad1, sector);
            }

            if (mad2.Length != 0)
            {
                result.InfoByte2 = mad2[1];

                for (var sector = FirstMad2Sector; sector <= ClassicLayout.MaxSector; sector++)
                {
                    result.aids[sector] = ReadAid(mad2, sector - Mad2Sector);
                }
            }

            mad = result;

            return true;
        }

        /// <summary>Computes the CRC of a MAD: over every byte after the first, which is where the CRC itself is stored.</summary>
        /// <param name="madBytes">The 32 bytes of MAD1 or the 48 bytes of MAD2; the first byte is ignored.</param>
        /// <returns>The CRC byte.</returns>
        public static byte ComputeCrc(ReadOnlySpan<byte> madBytes)
        {
            var crc = CrcStart;

            for (var i = 1; i < madBytes.Length; i++)
            {
                crc ^= madBytes[i];

                for (var bit = 0; bit < 8; bit++)
                {
                    var carry = (crc & 0x80) != 0;

                    crc = (crc << 1) & 0xFF;

                    if (carry)
                    {
                        crc ^= CrcPolynomial;
                    }
                }
            }

            return (byte)crc;
        }

        /// <summary>Tells whether the directory has an entry for a sector: the sectors 1 to 15, and with MAD2 the sectors 17 to 39. Sector 0 and sector 16 hold the directory itself.</summary>
        /// <param name="sector">The sector number.</param>
        /// <returns><c>true</c> when there is an entry.</returns>
        public bool Covers(int sector) =>
            (sector >= FirstMad1Sector && sector <= LastMad1Sector)
            || (Version == ClassicMadVersion.Mad2 && sector >= FirstMad2Sector && sector <= ClassicLayout.MaxSector);

        /// <summary>Gives the AID of a sector.</summary>
        /// <param name="sector">A sector that the directory <see cref="Covers"/>.</param>
        /// <returns>The AID; 0 for a free sector.</returns>
        public ushort GetAid(int sector)
        {
            RequireCovered(sector);

            return aids[sector];
        }

        /// <summary>Sets the AID of a sector.</summary>
        /// <param name="sector">A sector that the directory <see cref="Covers"/>.</param>
        /// <param name="aid">The AID; 0 frees the sector.</param>
        public void SetAid(int sector, ushort aid)
        {
            RequireCovered(sector);

            aids[sector] = aid;
        }

        /// <summary>Finds all sectors that carry an AID (an application may own more than one).</summary>
        /// <param name="aid">The AID.</param>
        /// <returns>The sector numbers in ascending order; empty when none carries it.</returns>
        public IReadOnlyList<int> FindSectors(ushort aid)
        {
            var found = new List<int>();

            for (var sector = FirstMad1Sector; sector <= ClassicLayout.MaxSector; sector++)
            {
                if (Covers(sector) && aids[sector] == aid)
                {
                    found.Add(sector);
                }
            }

            return found;
        }

        /// <summary>Finds the first sector that carries an AID.</summary>
        /// <param name="aid">The AID.</param>
        /// <returns>The lowest sector number, or <c>null</c> when none carries it.</returns>
        public int? FindFirstSector(ushort aid)
        {
            var sectors = FindSectors(aid);

            return sectors.Count == 0 ? null : sectors[0];
        }

        /// <summary>Gives the 32 bytes of MAD1 as they go to the blocks 1 and 2 of sector 0, with the right CRC.</summary>
        /// <returns>A new array.</returns>
        public byte[] ToMad1Bytes()
        {
            var bytes = new byte[Mad1Length];

            bytes[1] = InfoByte1;

            for (var sector = FirstMad1Sector; sector <= LastMad1Sector; sector++)
            {
                WriteAid(bytes, sector, aids[sector]);
            }

            bytes[0] = ComputeCrc(bytes);

            return bytes;
        }

        /// <summary>Gives the 48 bytes of MAD2 as they go to the blocks 64 to 66, with the right CRC.</summary>
        /// <returns>A new array.</returns>
        public byte[] ToMad2Bytes()
        {
            if (Version != ClassicMadVersion.Mad2)
            {
                throw new InvalidOperationException("This directory is MAD1: it has no second part.");
            }

            var bytes = new byte[Mad2Length];

            bytes[1] = InfoByte2;

            for (var sector = FirstMad2Sector; sector <= ClassicLayout.MaxSector; sector++)
            {
                WriteAid(bytes, sector - Mad2Sector, aids[sector]);
            }

            bytes[0] = ComputeCrc(bytes);

            return bytes;
        }

        private static ushort ReadAid(ReadOnlySpan<byte> bytes, int entry) =>
            (ushort)(bytes[entry * 2] | (bytes[(entry * 2) + 1] << 8));

        private static void WriteAid(byte[] bytes, int entry, ushort aid)
        {
            bytes[entry * 2] = (byte)(aid & 0xFF);
            bytes[(entry * 2) + 1] = (byte)(aid >> 8);
        }

        private void RequireCovered(int sector)
        {
            if (!Covers(sector))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sector),
                    sector,
                    "The directory has no entry for this sector: sector 0 and 16 hold the directory, and the sectors from 17 on need MAD2.");
            }
        }
    }
}
