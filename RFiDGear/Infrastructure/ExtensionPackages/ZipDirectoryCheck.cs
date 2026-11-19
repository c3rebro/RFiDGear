#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>
    /// Looks at the end-of-central-directory record of an archive before the zip library reads it. The record says how many
    /// entries the archive has and how big the directory of them is, and the zip library builds an object for each entry as soon as
    /// it opens the archive: a crafted file that lists tens of thousands of entries (a few megabytes of directory) would cost
    /// tens of megabytes before any limit of a package could be applied. The record is read from the end of the stream, 22 bytes
    /// and a comment, and nothing else is read.
    /// </summary>
    internal static class ZipDirectoryCheck
    {
        /// <summary>The most bytes the directory of a package may take: 66 entries with names and extra fields are a few kilobytes.</summary>
        internal const int MaxDirectoryBytes = 64 * 1024;

        /// <summary>The most bytes an archive may have: its files together (see <see cref="ExtensionPackageReader.MaxTotalBytes"/>) and a little for the headers.</summary>
        internal const long MaxArchiveBytes = ExtensionPackageReader.MaxTotalBytes + (1024 * 1024);

        private const uint EndSignature = 0x06054B50;
        private const uint Zip64LocatorSignature = 0x07064B50;
        private const int EndRecordLength = 22;
        private const int Zip64LocatorLength = 20;
        private const int DiskNumberAt = 4;
        private const int DirectoryDiskAt = 6;
        private const int TotalEntriesAt = 10;
        private const int DirectorySizeAt = 12;
        private const int DirectoryOffsetAt = 16;
        private const int CommentLengthAt = 20;

        /// <summary>Checks an archive.</summary>
        /// <param name="stream">The archive; its position is not defined afterwards.</param>
        /// <returns><c>null</c> if the archive may be read; else why not: too big, or no archive that a package can be.</returns>
        internal static ExtensionPackageProblem? Check(Stream stream)
        {
            if (!stream.CanSeek)
            {
                return ExtensionPackageProblem.NotAPackage;
            }

            var length = stream.Length;
            if (length > MaxArchiveBytes)
            {
                return ExtensionPackageProblem.TooLarge;
            }

            if (length < EndRecordLength)
            {
                return ExtensionPackageProblem.NotAPackage;
            }

            var tail = ReadTail(stream, length);
            var at = FindEndRecord(tail);
            if (at < 0)
            {
                return ExtensionPackageProblem.NotAPackage;
            }

            return Judge(tail, at, length - tail.Length + at);
        }

        // What the record says: only one disk, no zip64 (a package is far below what it is for), few entries, a small directory,
        // and a directory that ends exactly where the record begins. That last one makes the declared size the real size: the
        // zip library reads entries from the offset of the directory until the signature of an entry is not there, so a record that
        // says "one entry, one byte" over a longer run of entries must not pass because the library happens to read only what the
        // record says today. Archives of the tools that make packages (and of Windows) end their directory at the record.
        private static ExtensionPackageProblem? Judge(byte[] tail, int at, long recordPosition)
        {
            // The record has to be whole, and its comment has to end the archive: nothing after it that the library could skip.
            if (at + EndRecordLength > tail.Length || at + EndRecordLength + ReadUInt16(tail, at + CommentLengthAt) != tail.Length)
            {
                return ExtensionPackageProblem.NotAPackage;
            }

            if (ReadUInt16(tail, at + DiskNumberAt) != 0 || ReadUInt16(tail, at + DirectoryDiskAt) != 0)
            {
                return ExtensionPackageProblem.NotAPackage;
            }

            var entries = ReadUInt16(tail, at + TotalEntriesAt);
            var size = ReadUInt32(tail, at + DirectorySizeAt);
            var offset = ReadUInt32(tail, at + DirectoryOffsetAt);
            var hasLocator = at >= Zip64LocatorLength && ReadUInt32(tail, at - Zip64LocatorLength) == Zip64LocatorSignature;

            if (hasLocator || entries == ushort.MaxValue || size == uint.MaxValue || offset == uint.MaxValue ||
                entries > ExtensionPackageReader.MaxFiles + 1 || size > MaxDirectoryBytes)
            {
                return ExtensionPackageProblem.TooLarge;
            }

            return offset + (long)size == recordPosition ? null : ExtensionPackageProblem.NotAPackage;
        }

        // The last bytes of the archive: the record, and the comment that may follow it (at most 64 KiB).
        private static byte[] ReadTail(Stream stream, long length)
        {
            var tail = new byte[(int)Math.Min(length, EndRecordLength + ushort.MaxValue)];
            stream.Seek(length - tail.Length, SeekOrigin.Begin);
            var read = 0;
            while (read < tail.Length)
            {
                var count = stream.Read(tail, read, tail.Length - read);
                if (count == 0)
                {
                    throw new EndOfStreamException("The archive ended while its last bytes were read.");
                }

                read += count;
            }

            return tail;
        }

        // The record is the last signature of the end record that there is, which is the one that the zip library looks for too, from
        // the end of the archive backwards. Not "the first one whose comment fits": an archive can hold an honest record in front
        // of a second one that its comment contains (and a byte of padding after that), and a library that takes the last signature
        // and lets the comment end early would then read the second one. Whether the comment of this record ends the archive is
        // judged afterwards, and an archive where it does not is no package.
        private static int FindEndRecord(byte[] tail)
        {
            for (var i = tail.Length - sizeof(uint); i >= 0; i--)
            {
                if (ReadUInt32(tail, i) == EndSignature)
                {
                    return i;
                }
            }

            return -1;
        }

        private static ushort ReadUInt16(byte[] bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at));

        private static uint ReadUInt32(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    }
}
