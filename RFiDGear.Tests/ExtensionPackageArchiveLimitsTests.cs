using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

using RFiDGear.Infrastructure.ExtensionPackages;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What the reader looks at before it lets the zip library read an archive: the end-of-central-directory record, which says
    /// how many entries there are and how big the directory of them is. An archive that says too much is refused there, so the
    /// library never builds an object for each of a million entries that a crafted file lists.
    /// </summary>
    public sealed class ExtensionPackageArchiveLimitsTests
    {
        private const string Id = "RFiDGear.Extensions.Sample";

        // The offsets of the fields of the end-of-central-directory record (APPNOTE 4.3.16).
        private const int DiskNumber = 4;
        private const int TotalEntries = 10;
        private const int DirectorySize = 12;
        private const int DirectoryOffset = 16;

        private static byte[] Zip(Action<ZipArchive> fill, string? comment = null)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                fill(archive);
                if (comment != null)
                {
                    archive.Comment = comment;
                }
            }

            return stream.ToArray();
        }

        private static byte[] ValidPackage(string? comment = null) => Zip(
            archive =>
            {
                var main = Encoding.UTF8.GetBytes("main");
                var json = $"{{\"schema\":1,\"id\":\"{Id}\",\"displayName\":\"Sample\",\"version\":\"1.1.0\"," +
                    $"\"files\":[{{\"name\":\"{Id}.dll\",\"sha256\":\"{Convert.ToHexString(SHA256.HashData(main))}\"}}]}}";
                foreach (var (name, content) in new[] { (Id + ".dll", main), ("extension.json", Encoding.UTF8.GetBytes(json)) })
                {
                    using var entry = archive.CreateEntry(name).Open();
                    entry.Write(content, 0, content.Length);
                }
            },
            comment);

        // Where the record starts: the last place with its signature (PK 05 06).
        private static int EndRecord(byte[] zip)
        {
            for (var i = zip.Length - 22; i >= 0; i--)
            {
                if (zip[i] == 0x50 && zip[i + 1] == 0x4B && zip[i + 2] == 0x05 && zip[i + 3] == 0x06)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("The archive has no end record.");
        }

        private static ExtensionPackageProblem? ProblemOf(byte[] zip) => ExtensionPackageReader.Read(new MemoryStream(zip)).Problem;

        [Fact]
        public void APackage_IsStillRead_AlsoWithAComment()
        {
            Assert.True(ExtensionPackageReader.Read(new MemoryStream(ValidPackage())).Success);
            Assert.True(ExtensionPackageReader.Read(new MemoryStream(ValidPackage("made by the build of 2026-11-20"))).Success);
        }

        [Theory]
        [InlineData(TotalEntries, 5000)]                   // more entries than a package may have
        [InlineData(TotalEntries, 65535)]                  // the marker that says "look in the zip64 record"
        [InlineData(DirectorySize, 10 * 1024 * 1024)]      // a directory of entries far bigger than 66 of them
        public void AnArchiveThatSaysItIsTooBig_IsRefused_BeforeItIsRead(int field, int value)
        {
            var zip = ValidPackage();
            var end = EndRecord(zip);
            if (field == TotalEntries)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(zip.AsSpan(end + field), (ushort)value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(end + field), (uint)value);
            }

            Assert.Equal(ExtensionPackageProblem.TooLarge, ProblemOf(zip));
        }

        [Fact]
        public void TheMarkersOfTheZip64Format_AreRefused_ForAPackageIsFarBelowItsLimits()
        {
            var zip = ValidPackage();
            BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(EndRecord(zip) + DirectoryOffset), uint.MaxValue);

            Assert.Equal(ExtensionPackageProblem.TooLarge, ProblemOf(zip));
        }

        [Fact]
        public void ADirectoryThatEndsAfterTheEndRecord_IsNoPackage()
        {
            var zip = ValidPackage();
            BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(EndRecord(zip) + DirectoryOffset), 0x7FFFFFFF);

            Assert.Equal(ExtensionPackageProblem.NotAPackage, ProblemOf(zip));
        }

        [Fact]
        public void AnArchiveOfSeveralDisks_IsNoPackage()
        {
            var zip = ValidPackage();
            BinaryPrimitives.WriteUInt16LittleEndian(zip.AsSpan(EndRecord(zip) + DiskNumber), 1);

            Assert.Equal(ExtensionPackageProblem.NotAPackage, ProblemOf(zip));
        }

        [Fact]
        public void AnArchiveThatIsCutOff_IsNoPackage()
        {
            var zip = ValidPackage();
            var cut = new byte[zip.Length - 30];
            Array.Copy(zip, cut, cut.Length);

            Assert.Equal(ExtensionPackageProblem.NotAPackage, ProblemOf(cut));
            Assert.Equal(ExtensionPackageProblem.NotAPackage, ProblemOf(Array.Empty<byte>()));
            Assert.Equal(ExtensionPackageProblem.NotAPackage, ProblemOf(new byte[10]));
        }

        // 60,000 empty entries: below the limit of the 16-bit count of the format, so no zip64, and still about 3 MB of directory.
        private static byte[] ArchiveOfTensOfThousandsOfEntries() => Zip(archive =>
        {
            for (var i = 0; i < 60000; i++)
            {
                archive.CreateEntry("f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), CompressionLevel.NoCompression);
            }
        });

        // What the reader allocates for the archive; the zip library would build an object for each of the entries (tens of megabytes).
        private static (ExtensionPackageReadResult Result, long Allocated) ReadAndMeasure(byte[] zip)
        {
            using var stream = new MemoryStream(zip);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = ExtensionPackageReader.Read(stream);
            return (result, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void AnArchiveThatListsTensOfThousandsOfEntries_IsRefused_WithoutBuildingThem()
        {
            var (result, allocated) = ReadAndMeasure(ArchiveOfTensOfThousandsOfEntries());

            Assert.Equal(ExtensionPackageProblem.TooLarge, result.Problem);
            Assert.True(allocated < 2 * 1024 * 1024, $"the reader allocated {allocated} bytes for an archive that it refuses");
        }

        [Theory]
        [InlineData("count and size say 1")]
        [InlineData("count says 1 and the size is the real one")]
        [InlineData("count, size and offset say that the directory is the last byte")]
        public void AnArchiveThatUnderstatesItsDirectory_IsRefused_WithoutBuildingTheEntriesItReallyHas(string lie)
        {
            // The zip library reads entries from the offset of the directory until the signature of an entry is not there, whatever
            // the end record says about their number: a record that says "one entry" over a directory of 60,000 must not pass.
            var zip = ArchiveOfTensOfThousandsOfEntries();
            var end = EndRecord(zip);
            switch (lie)
            {
                case "count and size say 1":
                    BinaryPrimitives.WriteUInt16LittleEndian(zip.AsSpan(end + TotalEntries), 1);
                    BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(end + DirectorySize), 1);
                    break;
                case "count says 1 and the size is the real one":
                    BinaryPrimitives.WriteUInt16LittleEndian(zip.AsSpan(end + TotalEntries), 1);
                    break;
                default:
                    BinaryPrimitives.WriteUInt16LittleEndian(zip.AsSpan(end + TotalEntries), 1);
                    BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(end + DirectorySize), 1);
                    BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(end + DirectoryOffset), (uint)(end - 1));
                    break;
            }

            var (result, allocated) = ReadAndMeasure(zip);

            Assert.False(result.Success);
            Assert.True(allocated < 2 * 1024 * 1024, $"the reader allocated {allocated} bytes for an archive that it refuses");
        }

        [Fact]
        public void TheDirectoryHasToEndWhereTheEndRecordBegins_SoItsRealSizeIsTheDeclaredOne()
        {
            var zip = ValidPackage();
            var end = EndRecord(zip);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(end + DirectorySize));

            // One byte of the directory missing at the front, or one byte more before the record: not what a package is.
            BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(end + DirectorySize), size - 1);
            Assert.Equal(ExtensionPackageProblem.NotAPackage, ProblemOf(zip));

            BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(end + DirectorySize), size + 1);
            Assert.Equal(ExtensionPackageProblem.NotAPackage, ProblemOf(zip));
        }

        [Fact]
        public void TwoEndRecords_AreNoPackage_TheRecordThatIsLookedAtIsTheOneTheZipLibraryReads()
        {
            // The first record (honest: no entries) has a comment that holds a second record, the one of the 60,000 entries, and a
            // byte of padding after it. A reader that takes "the first record whose comment ends at the end of the file" looks at the
            // honest one; a zip library that takes the last signature it finds and lets the comment end early reads the second.
            var big = ArchiveOfTensOfThousandsOfEntries();
            var bigEnd = EndRecord(big);
            var twoRecords = new byte[bigEnd + 22 + 22 + 1];
            Array.Copy(big, twoRecords, bigEnd);                    // the data and the directory of the 60,000 entries
            Array.Copy(big, bigEnd, twoRecords, bigEnd + 22, 22);   // the second record, as it was: 60,000 entries
            twoRecords[bigEnd] = 0x50;                              // the first record: PK 05 06, no disks, no entries,
            twoRecords[bigEnd + 1] = 0x4B;                          // a directory of no bytes that ends where it begins,
            twoRecords[bigEnd + 2] = 0x05;                          // and a comment that reaches to the end of the file
            twoRecords[bigEnd + 3] = 0x06;
            BinaryPrimitives.WriteUInt32LittleEndian(twoRecords.AsSpan(bigEnd + DirectoryOffset), (uint)bigEnd);
            BinaryPrimitives.WriteUInt16LittleEndian(twoRecords.AsSpan(bigEnd + 20), 22 + 1);

            var (result, allocated) = ReadAndMeasure(twoRecords);

            Assert.False(result.Success);
            Assert.True(allocated < 2 * 1024 * 1024, $"the reader allocated {allocated} bytes for an archive that it refuses");
        }
    }
}
