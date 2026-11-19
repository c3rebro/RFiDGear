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

        [Fact]
        public void AnArchiveThatListsTensOfThousandsOfEntries_IsRefused_WithoutBuildingThem()
        {
            // 60,000 empty entries: below the limit of the 16-bit count of the format, so no zip64, and still about 3 MB of directory.
            var zip = Zip(archive =>
            {
                for (var i = 0; i < 60000; i++)
                {
                    archive.CreateEntry("f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), CompressionLevel.NoCompression);
                }
            });
            using var stream = new MemoryStream(zip);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = ExtensionPackageReader.Read(stream);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(ExtensionPackageProblem.TooLarge, result.Problem);

            // The zip library would build an object for each entry (tens of megabytes); the check of the end record builds none.
            Assert.True(allocated < 2 * 1024 * 1024, $"the reader allocated {allocated} bytes for an archive that it refuses");
        }
    }
}
