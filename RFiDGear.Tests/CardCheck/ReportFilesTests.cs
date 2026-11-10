using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class ReportFilesTests : IDisposable
    {
        private static readonly ReportFile[] AllFiles = (ReportFile[])Enum.GetValues(typeof(ReportFile));

        private readonly TempFolder _root = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose() => _root.Dispose();

        private IReportFiles Service() => CardCheckServices.CreateReportFiles(_log);

        private OrderReportPaths Paths(string job = "KP-000005", string chip = "5", bool subfolder = true) =>
            Service().Resolve(new ReportLocation(_root.Path, subfolder), job, chip).Value;

        private static void Touch(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "x");
        }

        // ── paths ───────────────────────────────────────────────────────────

        [Fact]
        public void Resolve_WithASubfolder_PutsEveryFileInAFolderNamedAfterTheJob()
        {
            var paths = Paths();

            Assert.Equal(_root.Path, paths.Root);
            Assert.Equal(Path.Combine(_root.Path, "KP-000005"), paths.Folder);
            Assert.Equal(Path.Combine(paths.Folder, "KP-000005-5.step1.pdf"), paths.Step1);
            Assert.Equal(Path.Combine(paths.Folder, "KP-000005-5.step2.pdf"), paths.Step2);
            Assert.Equal(Path.Combine(paths.Folder, "KP-000005-5.step3.pdf"), paths.Step3);
            Assert.Equal(Path.Combine(paths.Folder, "KP-000005-5_final.pdf"), paths.Final);
            Assert.Equal(Path.Combine(paths.Folder, "KP-000005-5.download.pdf"), paths.Download);
            Assert.Equal(Path.Combine(paths.Folder, "KP-000005-5.edit.pdf"), paths.Edit);
        }

        [Fact]
        public void Resolve_WithoutASubfolder_PutsTheFilesInTheRoot()
        {
            var paths = Paths(subfolder: false);

            Assert.Equal(paths.Root, paths.Folder);
            Assert.Equal(Path.Combine(_root.Path, "KP-000005-5.step1.pdf"), paths.Step1);
            Assert.Equal(Path.Combine(_root.Path, "KP-000005-5_final.pdf"), paths.Final);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Resolve_ForSafeNumbers_BuildsExactlyTheNamesTheOldAssistantUsed(bool subfolder)
        {
            // <Out>\[<Job>\]<Job>-<Chip> + .step1.pdf / .step2.pdf / .step3.pdf / _final.pdf
            var legacyBase = _root.Path + "\\" + (subfolder ? "J7\\" : string.Empty) + "J7" + "-" + "C9";
            var paths = Paths("J7", "C9", subfolder);

            Assert.Equal(legacyBase + ".step1.pdf", paths.Step1);
            Assert.Equal(legacyBase + ".step2.pdf", paths.Step2);
            Assert.Equal(legacyBase + ".step3.pdf", paths.Step3);
            Assert.Equal(legacyBase + "_final.pdf", paths.Final);
        }

        [Fact]
        public void Resolve_ARootWithATrailingSeparator_GivesTheSamePathsAsWithout()
        {
            var with = Service().Resolve(new ReportLocation(_root.Path + Path.DirectorySeparatorChar, true), "J", "C").Value;
            var without = Service().Resolve(new ReportLocation(_root.Path, true), "J", "C").Value;

            Assert.Equal(without, with);
        }

        [Fact]
        public void PathOf_ReturnsEachProperty_AndRefusesAnUndefinedFile()
        {
            var paths = Paths();

            Assert.Equal(paths.Step1, paths.PathOf(ReportFile.Step1));
            Assert.Equal(paths.Step2, paths.PathOf(ReportFile.Step2));
            Assert.Equal(paths.Step3, paths.PathOf(ReportFile.Step3));
            Assert.Equal(paths.Final, paths.PathOf(ReportFile.Final));
            Assert.Equal(paths.Download, paths.PathOf(ReportFile.Download));
            Assert.Equal(paths.Edit, paths.PathOf(ReportFile.Edit));
            Assert.Equal(6, AllFiles.Select(paths.PathOf).Distinct().Count());
            Assert.Throws<ArgumentOutOfRangeException>(() => paths.PathOf((ReportFile)99));
        }

        [Fact]
        public void Resolve_TheRedoFilesAreOfThisOrderAlone_NotOneSharedName()
        {
            // The old assistant downloaded every order's report to the same fixed name.
            var one = Paths("J1", "C1");
            var two = Paths("J2", "C2");

            Assert.NotEqual(one.Download, two.Download);
            Assert.NotEqual(one.Edit, two.Edit);
        }

        public static IEnumerable<object?[]> BadRoots()
        {
            yield return new object?[] { null, FailureKind.NotConfigured, "report.root.notSet" };
            yield return new object?[] { "", FailureKind.NotConfigured, "report.root.notSet" };
            yield return new object?[] { "   ", FailureKind.NotConfigured, "report.root.notSet" };
            yield return new object?[] { "reports", FailureKind.InvalidConfiguration, "report.root.notAbsolute" };
            yield return new object?[] { "..\\reports", FailureKind.InvalidConfiguration, "report.root.notAbsolute" };
            yield return new object?[] { "C:reports", FailureKind.InvalidConfiguration, "report.root.notAbsolute" };
            yield return new object?[] { "\\reports", FailureKind.InvalidConfiguration, "report.root.notAbsolute" };
            yield return new object?[] { Path.Combine(Path.GetTempPath(), "a|b"), FailureKind.InvalidConfiguration, "report.root.invalid" };
            yield return new object?[] { Path.Combine(Path.GetTempPath(), "a\"b"), FailureKind.InvalidConfiguration, "report.root.invalid" };
            yield return new object?[] { Path.Combine(Path.GetTempPath(), "a*b"), FailureKind.InvalidConfiguration, "report.root.invalid" };
            yield return new object?[] { Path.Combine(Path.GetTempPath(), "a?b"), FailureKind.InvalidConfiguration, "report.root.invalid" };
            yield return new object?[] { Path.Combine(Path.GetTempPath(), "a<b"), FailureKind.InvalidConfiguration, "report.root.invalid" };
            // Not Path.Combine: it would take "a:b" for a new drive-relative root and drop the folder in front of it.
            yield return new object?[] { Path.Combine(Path.GetTempPath(), "ok") + Path.DirectorySeparatorChar + "a:b", FailureKind.InvalidConfiguration, "report.root.invalid" };
            // An extended-length prefix is a root to Path.GetPathRoot, but the host would replace its "?" by a number.
            yield return new object?[] { @"\\?\" + Path.GetTempPath(), FailureKind.InvalidConfiguration, "report.root.invalid" };
            yield return new object?[] { @"\\?\UNC\server\share\reports", FailureKind.InvalidConfiguration, "report.root.invalid" };
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Resolve_JobAndChipThatDifferOnlyInWhereTheHyphenIs_NeverShareAFile(bool subfolder)
        {
            // "A-B" + "C" and "A" + "B-C" both used to be A-B-C.*, so cleaning up one order deleted the other's reports.
            var one = Service().Resolve(new ReportLocation(_root.Path, subfolder), "A-B", "C").Value;
            var two = Service().Resolve(new ReportLocation(_root.Path, subfolder), "A", "B-C").Value;

            var pathsOfOne = AllFiles.Select(one.PathOf).ToList();
            var pathsOfTwo = AllFiles.Select(two.PathOf).ToList();
            Assert.Empty(pathsOfOne.Intersect(pathsOfTwo, StringComparer.OrdinalIgnoreCase));
        }

        [Fact]
        public void Resolve_TheHyphenOfAChipNumberIsEscaped_TheHyphenOfAJobNumberIsKept()
        {
            var paths = Service().Resolve(new ReportLocation(_root.Path, false), "KP-000005", "B-C").Value;
            var name = Path.GetFileName(paths.Step1);

            Assert.StartsWith("KP-000005-B_C~", name, StringComparison.Ordinal);
            Assert.EndsWith(".step1.pdf", name, StringComparison.Ordinal);
        }

        [Fact]
        public void Resolve_NoTwoOrdersOfAnAlphabetOfTroublemakersShareAnyFile()
        {
            var alphabet = new[] { "a", "-", "/", "~", "_", "?" };
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var numbers = alphabet.SelectMany(x => alphabet.Select(y => x + y)).Concat(alphabet).ToList();

            foreach (var job in numbers)
            {
                foreach (var chip in numbers)
                {
                    var result = Service().Resolve(new ReportLocation(_root.Path, false), job, chip);

                    foreach (var file in AllFiles.Where(_ => result.IsSuccess))
                    {
                        var path = result.Value.PathOf(file);
                        var order = $"job '{job}' chip '{chip}' {file}";

                        Assert.False(seen.TryGetValue(path, out var other), $"{order} and {other} both are {path}");
                        seen[path] = order;
                    }
                }
            }

            Assert.True(seen.Count > 1000);
        }

        [Theory]
        [MemberData(nameof(BadRoots))]
        public void Resolve_ARootThatIsNotAFullPath_IsRefusedWithAReason(string? root, FailureKind kind, string code)
        {
            var result = Service().Resolve(new ReportLocation(root!, true), "J", "C");

            Assert.False(result.IsSuccess);
            Assert.Equal(kind, result.Failure!.Kind);
            Assert.Equal(code, result.Failure.Code);
        }

        [Fact]
        public void Resolve_ABadNumber_SaysWhichOne()
        {
            Assert.Equal("report.job.empty", Service().Resolve(new ReportLocation(_root.Path, true), " ", "C").Failure!.Code);
            Assert.Equal("report.chip.empty", Service().Resolve(new ReportLocation(_root.Path, true), "J", "").Failure!.Code);
            Assert.Equal("report.job.invalid", Service().Resolve(new ReportLocation(_root.Path, true), "NUL", "C").Failure!.Code);
            Assert.Equal("report.chip.invalid", Service().Resolve(new ReportLocation(_root.Path, true), "J", "..").Failure!.Code);
        }

        [Fact]
        public void Resolve_ThePathLengthLimit_IsOnTheLongestFileName()
        {
            var shortest = Paths("J", "1", subfolder: false);
            var longest = AllFiles.Max(f => shortest.PathOf(f).Length);
            var padding = ReportFiles.MaxPathLength - longest;

            var atTheLimit = Service().Resolve(new ReportLocation(_root.Path, false), "J", "1" + new string('x', padding));
            var oneOver = Service().Resolve(new ReportLocation(_root.Path, false), "J", "1" + new string('x', padding + 1));

            Assert.True(atTheLimit.IsSuccess);
            Assert.Equal(ReportFiles.MaxPathLength, AllFiles.Max(f => atTheLimit.Value.PathOf(f).Length));
            Assert.False(oneOver.IsSuccess);
            Assert.Equal("report.path.tooLong", oneOver.Failure!.Code);
        }

        public static IEnumerable<object[]> HostileNumbers() => new[]
        {
            "..\\..\\Windows\\System32", "../../etc/passwd", "C:\\Windows", "\\\\server\\share\\x", "a/../b", "..\\",
            "/", "\\", "x\0y", "CON\\x", "A?B", "A*B", "A:B", "A|B", " lead", "trail ", "\u202Ersolved",
        }.Select(value => new object[] { value });

        [Theory]
        [MemberData(nameof(HostileNumbers))]
        public void Resolve_HostileNumbers_NeverLeaveTheRoot(string hostile)
        {
            foreach (var subfolder in new[] { true, false })
            {
                var result = Service().Resolve(new ReportLocation(_root.Path, subfolder), hostile, hostile);

                if (!result.IsSuccess)
                {
                    continue;
                }

                foreach (var file in AllFiles)
                {
                    var path = result.Value.PathOf(file);

                    Assert.Equal(path, Path.GetFullPath(path));
                    Assert.StartsWith(_root.Path + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
                    Assert.True(
                        string.Equals(Path.GetDirectoryName(path), result.Value.Folder, StringComparison.OrdinalIgnoreCase),
                        $"{path} is not directly in {result.Value.Folder}");
                }

                Assert.True(
                    result.Value.Folder == _root.Path || string.Equals(Path.GetDirectoryName(result.Value.Folder), _root.Path, StringComparison.OrdinalIgnoreCase));
            }
        }

        // ── the extension's own check of a report target ────────────────────

        [Theory]
        [InlineData("NUL")]
        [InlineData("CON.txt")]
        [InlineData("lpt1")]
        [InlineData("abc.")]
        public void Resolve_AFileNameWindowsCannotCreate_IsRefused(string name)
        {
            // Windows cannot create a file with such a name, so the library builds none.
            Assert.False(Service().Resolve(new ReportLocation(_root.Path, true), name, "1").IsSuccess);
            Assert.False(Service().Resolve(new ReportLocation(_root.Path, false), "1", name).IsSuccess);
        }

        // ── the folder ──────────────────────────────────────────────────────

        [Fact]
        public void EnsureOrderFolder_CreatesTheOrdersFolder_AndMayBeCalledAgain()
        {
            var paths = Paths();

            Assert.False(Directory.Exists(paths.Folder));
            Assert.True(Service().EnsureOrderFolder(paths).IsSuccess);
            Assert.True(Directory.Exists(paths.Folder));
            Assert.True(Service().EnsureOrderFolder(paths).IsSuccess);
        }

        [Fact]
        public void EnsureOrderFolder_WithoutASubfolder_CreatesNothing()
        {
            var before = Directory.GetFileSystemEntries(_root.Path, "*", SearchOption.AllDirectories);

            Assert.True(Service().EnsureOrderFolder(Paths(subfolder: false)).IsSuccess);

            Assert.Equal(before, Directory.GetFileSystemEntries(_root.Path, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public void EnsureOrderFolder_WhenTheOutputFolderIsMissing_FailsAndCreatesNeitherIt_NorTheSubfolder()
        {
            var missingRoot = Path.Combine(_root.Path, "not-there");
            var paths = Service().Resolve(new ReportLocation(missingRoot, true), "J", "C").Value;

            var result = Service().EnsureOrderFolder(paths);

            Assert.False(result.IsSuccess);
            Assert.Equal(FailureKind.NotFound, result.Failure!.Kind);
            Assert.Equal("report.root.missing", result.Failure.Code);
            Assert.False(Directory.Exists(missingRoot));
        }

        [Fact]
        public void EnsureOrderFolder_WhenAFileIsInTheWay_FailsWithoutThrowing()
        {
            var paths = Paths();
            File.WriteAllText(paths.Folder, "a file where the folder should be");

            var result = Service().EnsureOrderFolder(paths);

            Assert.False(result.IsSuccess);
            Assert.Equal("report.folder.io", result.Failure!.Code);
        }

        // ── clearing a file before the run that writes it ───────────────────

        [Theory]
        [InlineData(ReportFile.Step1)]
        [InlineData(ReportFile.Step2)]
        [InlineData(ReportFile.Step3)]
        public void PrepareStep_RemovesWhatAnEarlierAttemptLeftBehind_AndOnlyThat(ReportFile file)
        {
            var paths = Paths();
            foreach (var each in AllFiles)
            {
                Touch(paths.PathOf(each));
            }

            Assert.True(Service().PrepareStep(paths, file).IsSuccess);

            foreach (var each in AllFiles)
            {
                Assert.Equal(each != file, File.Exists(paths.PathOf(each)));
            }
        }

        [Fact]
        public void PrepareStep_WhenThereIsNothingToRemove_Succeeds()
        {
            Assert.True(Service().PrepareStep(Paths(), ReportFile.Step3).IsSuccess);
        }

        [Fact]
        public void PrepareStep_WhenAFolderIsWhereTheReportShouldBe_Fails_ItIsNotNothingToRemove()
        {
            var paths = Paths();
            Directory.CreateDirectory(paths.Step3);

            var result = Service().PrepareStep(paths, ReportFile.Step3);

            Assert.False(result.IsSuccess);
            Assert.Equal(FailureKind.Io, result.Failure!.Kind);
            Assert.Equal("report.stale.io", result.Failure.Code);
            Assert.True(Directory.Exists(paths.Step3));
        }

        [Fact]
        public void Cleanup_WhenAFolderIsWhereAReportShouldBe_ReportsItAsFailedAndStillRemovesTheRest()
        {
            var paths = Paths();
            Directory.CreateDirectory(paths.Step1);
            Touch(paths.Step2);

            var report = Service().Cleanup(paths, CleanupScope.NotSupported, removeTemporaryReports: true);

            Assert.Equal(new[] { ReportFile.Step2 }, report.Removed);
            Assert.Equal(new[] { ReportFile.Step1 }, report.Failed);
            Assert.True(Directory.Exists(paths.Step1));
            Assert.Contains("Step1", _log.Everything, StringComparison.Ordinal);
        }

        [Fact]
        public void PrepareStepAndCleanup_NeverDeleteAPathOutsideTheOrdersFolder_WhateverTheRecordSays()
        {
            var paths = Paths();
            var elsewhere = Path.Combine(_root.Path, "elsewhere", "precious.docx");           // another folder in the root
            var inTheRoot = Path.Combine(_root.Path, "KP-000005-5.step2.pdf");                // the root, not the order's folder
            var wayOut = Path.Combine(paths.Folder, "..", "elsewhere", "precious.docx");      // a way out of the folder
            Touch(elsewhere);
            Touch(inTheRoot);

            var prepared = Service().PrepareStep(paths with { Step1 = elsewhere }, ReportFile.Step1);
            var viaDots = Service().PrepareStep(paths with { Step1 = wayOut }, ReportFile.Step1);
            var empty = Service().PrepareStep(paths with { Step1 = string.Empty }, ReportFile.Step1);
            var cleaned = Service().Cleanup(paths with { Step1 = elsewhere, Step2 = inTheRoot }, CleanupScope.NotSupported, removeTemporaryReports: true);

            Assert.All(new[] { prepared, viaDots, empty }, result => Assert.Equal("report.path.outside", result.Failure!.Code));
            Assert.Equal(new[] { ReportFile.Step1, ReportFile.Step2 }, cleaned.Failed);
            Assert.Empty(cleaned.Removed);
            Assert.True(File.Exists(elsewhere));
            Assert.True(File.Exists(inTheRoot));
        }

        [Fact]
        public void PrepareStep_RemovesAReadOnlyFile()
        {
            var paths = Paths();
            Touch(paths.Step3);
            File.SetAttributes(paths.Step3, FileAttributes.ReadOnly);

            Assert.True(Service().PrepareStep(paths, ReportFile.Step3).IsSuccess);
            Assert.False(File.Exists(paths.Step3));
        }

        [Theory]
        [InlineData(ReportFile.Final)]
        [InlineData(ReportFile.Download)]
        [InlineData(ReportFile.Edit)]
        public void PrepareStep_NeverTouchesTheFinalOrTheRedoFiles(ReportFile file)
        {
            var paths = Paths();
            Touch(paths.PathOf(file));

            var result = Service().PrepareStep(paths, file);

            Assert.False(result.IsSuccess);
            Assert.Equal(FailureKind.InvalidInput, result.Failure!.Kind);
            Assert.Equal("report.file.unsupported", result.Failure.Code);
            Assert.True(File.Exists(paths.PathOf(file)));
        }

        [Fact]
        public void PrepareStep_ARunningProgramHoldsTheFile_FailsWithAnIoFailureAndKeepsTheFile()
        {
            var paths = Paths();
            Touch(paths.Step3);

            using (new FileStream(paths.Step3, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                var result = Service().PrepareStep(paths, ReportFile.Step3);

                Assert.False(result.IsSuccess);
                Assert.Equal(FailureKind.Io, result.Failure!.Kind);
                Assert.Equal("report.stale.io", result.Failure.Code);
            }

            Assert.True(File.Exists(paths.Step3));
        }

        // ── cleaning up ─────────────────────────────────────────────────────

        public static IEnumerable<object[]> CleanupMatrix()
        {
            // scope, remove temporary reports, the files that must be gone afterwards
            yield return new object[] { CleanupScope.NotSupported, true, new[] { ReportFile.Step1, ReportFile.Step2 } };
            yield return new object[] { CleanupScope.NotSupported, false, Array.Empty<ReportFile>() };
            yield return new object[] { CleanupScope.SupportedOnly, true, Array.Empty<ReportFile>() };
            yield return new object[] { CleanupScope.SupportedOnly, false, Array.Empty<ReportFile>() };
            yield return new object[] { CleanupScope.Programmed, true, new[] { ReportFile.Step1, ReportFile.Step2, ReportFile.Step3 } };
            yield return new object[] { CleanupScope.Programmed, false, Array.Empty<ReportFile>() };
            yield return new object[] { CleanupScope.ReEdit, true, new[] { ReportFile.Download, ReportFile.Edit } };
            yield return new object[] { CleanupScope.ReEdit, false, new[] { ReportFile.Download, ReportFile.Edit } };
        }

        [Theory]
        [MemberData(nameof(CleanupMatrix))]
        public void Cleanup_RemovesExactlyTheFilesOfTheMatrix_AndNeverTheFinalReport(CleanupScope scope, bool remove, ReportFile[] expectedGone)
        {
            var paths = Paths();
            foreach (var each in AllFiles)
            {
                Touch(paths.PathOf(each));
            }

            var report = Service().Cleanup(paths, scope, remove);

            Assert.Equal(expectedGone.OrderBy(f => f), report.Removed.OrderBy(f => f));
            Assert.Empty(report.Failed);

            foreach (var each in AllFiles)
            {
                Assert.Equal(!expectedGone.Contains(each), File.Exists(paths.PathOf(each)));
            }

            Assert.True(File.Exists(paths.Final));
        }

        [Fact]
        public void Cleanup_FilesThatAreNotThere_AreNeitherRemovedNorFailed()
        {
            var paths = Paths();
            Touch(paths.Step1);

            var report = Service().Cleanup(paths, CleanupScope.Programmed, removeTemporaryReports: true);

            Assert.Equal(new[] { ReportFile.Step1 }, report.Removed);
            Assert.Empty(report.Failed);
        }

        [Fact]
        public void Cleanup_AFileThatIsOpen_IsReportedAndTheRestIsStillRemoved_WithoutThrowing()
        {
            var paths = Paths();
            Touch(paths.Step1);
            Touch(paths.Step2);
            Touch(paths.Step3);

            CleanupReport report;
            using (new FileStream(paths.Step2, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                report = Service().Cleanup(paths, CleanupScope.Programmed, removeTemporaryReports: true);
            }

            Assert.Equal(new[] { ReportFile.Step1, ReportFile.Step3 }, report.Removed);
            Assert.Equal(new[] { ReportFile.Step2 }, report.Failed);
            Assert.True(File.Exists(paths.Step2));
        }

        [Fact]
        public void Cleanup_AFailureIsLoggedByKind_NeverWithAPathOrANumber()
        {
            var paths = Paths("SECRET-JOB-77", "SECRET-CHIP-88");
            Touch(paths.Step1);

            using (new FileStream(paths.Step1, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                Service().Cleanup(paths, CleanupScope.NotSupported, removeTemporaryReports: true);
            }

            Assert.Contains("Step1", _log.Everything, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET", _log.Everything, StringComparison.Ordinal);
            Assert.DoesNotContain(_root.Path, _log.Everything, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Cleanup_RemovesAReadOnlyWorkingReport()
        {
            var paths = Paths();
            Touch(paths.Step1);
            File.SetAttributes(paths.Step1, FileAttributes.ReadOnly);

            var report = Service().Cleanup(paths, CleanupScope.NotSupported, removeTemporaryReports: true);

            Assert.Equal(new[] { ReportFile.Step1 }, report.Removed);
            Assert.False(File.Exists(paths.Step1));
        }

        [Fact]
        public void Cleanup_WithNothingToDo_WritesNothingToTheLog()
        {
            Service().Cleanup(Paths(), CleanupScope.SupportedOnly, removeTemporaryReports: true);

            Assert.Empty(_log.Entries);
        }

        [Fact]
        public void Arguments_AreChecked()
        {
            var service = Service();
            var paths = Paths();

            Assert.Throws<ArgumentNullException>(() => CardCheckServices.CreateReportFiles(null!));
            Assert.Throws<ArgumentNullException>(() => service.Resolve(null!, "J", "C"));
            Assert.Throws<ArgumentNullException>(() => service.EnsureOrderFolder(null!));
            Assert.Throws<ArgumentNullException>(() => service.PrepareStep(null!, ReportFile.Step1));
            Assert.Throws<ArgumentNullException>(() => service.Cleanup(null!, CleanupScope.Programmed, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.Cleanup(paths, (CleanupScope)99, true));
        }
    }
}
