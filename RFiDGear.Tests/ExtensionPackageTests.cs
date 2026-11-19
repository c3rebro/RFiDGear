using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionPackages;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// An extension update that is installed from a package: what the package may hold, what the installer checks before it
    /// puts anything in place, and which copy of an extension RFiDGear loads when there are two.
    /// </summary>
    public sealed class ExtensionPackageTests : IDisposable
    {
        private const string Id = "RFiDGear.Extensions.Sample";
        private const string Main = Id + ".dll";

        private readonly string _root = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", "ext-package-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        // ── building packages ──

        private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

        // The name of a version folder, such as 1.1.0.0, written as what it is: a version with four parts.
        private static string Ver(int major, int minor) => new Version(major, minor, 0, 0).ToString();

        private static string ManifestJson(IEnumerable<KeyValuePair<string, byte[]>> files, string id = Id, string version = "1.1.0", string? minHost = null, int schema = 1)
        {
            var list = string.Join(",", files.Select(file => $"{{\"name\":\"{file.Key}\",\"sha256\":\"{Hash(file.Value)}\"}}"));
            var min = minHost == null ? string.Empty : $"\"minHostVersion\":\"{minHost}\",";
            return $"{{\"schema\":{schema},\"id\":\"{id}\",\"displayName\":\"Sample\",\"version\":\"{version}\",{min}\"files\":[{list}]}}";
        }

        // Zip entries are written as given, so a test can put any name into the archive.
        private static MemoryStream Zip(IReadOnlyDictionary<string, byte[]> entries)
        {
            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in entries)
                {
                    using var writer = archive.CreateEntry(entry.Key).Open();
                    writer.Write(entry.Value, 0, entry.Value.Length);
                }
            }

            stream.Position = 0;
            return stream;
        }

        private static Dictionary<string, byte[]> Files(params string[] names)
        {
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                files[name] = Bytes("content of " + name);
            }

            return files;
        }

        // A package whose manifest lists exactly the files, unless a test says otherwise.
        private static MemoryStream Package(Dictionary<string, byte[]> files, string? manifest = null)
        {
            var entries = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase)
            {
                [ExtensionPackageLayout.ManifestFileName] = Bytes(manifest ?? ManifestJson(files)),
            };

            return Zip(entries);
        }

        private static ExtensionPackageProblem? ProblemOf(Stream package) => ExtensionPackageReader.Read(package).Problem;

        // ── reading ──

        [Fact]
        public void APackage_IsReadWithItsFiles_AndWhatItSaysAboutItself()
        {
            var files = Files(Main, "RFiDGear.ExtensionLibraries.Sample.dll", "sni.dll");

            var read = ExtensionPackageReader.Read(Package(files));

            Assert.True(read.Success);
            var manifest = read.Package!.Manifest;
            Assert.Equal(Id, manifest.Id);
            Assert.Equal(new Version(1, 1, 0, 0), manifest.Version);
            Assert.Equal(3, read.Package.Files.Count);
            Assert.Equal(files[Main], read.Package.Files.Single(file => file.Name == Main).Content);
        }

        [Fact]
        public void APackage_CanBeReadFromAFile()
        {
            Directory.CreateDirectory(_root);
            var path = Path.Combine(_root, "sample" + ExtensionPackageLayout.PackageExtension);
            using (var package = Package(Files(Main)))
            {
                File.WriteAllBytes(path, package.ToArray());
            }

            Assert.True(ExtensionPackageReader.Read(path).Success);
            Assert.Equal(ExtensionPackageProblem.NotAPackage, ExtensionPackageReader.Read(Path.Combine(_root, "missing.rfext")).Problem);
        }

        [Fact]
        public void WhatIsNoZip_IsNoPackage()
        {
            Assert.Equal(ExtensionPackageProblem.NotAPackage, ProblemOf(new MemoryStream(Bytes("this is not an archive"))));
            Assert.Equal(ExtensionPackageProblem.NotAPackage, ProblemOf(new MemoryStream()));
        }

        [Fact]
        public void AnArchiveWithoutAManifest_IsNoPackage()
        {
            Assert.Equal(ExtensionPackageProblem.NoManifest, ProblemOf(Zip(Files(Main))));
        }

        [Theory]
        [InlineData("..\\evil.dll")]
        [InlineData("../evil.dll")]
        [InlineData("sub/RFiDGear.Extensions.Sample.dll")]
        [InlineData("C:\\evil.dll")]
        public void AFileWithAPathInItsName_IsNeverLookedAtFurther(string name)
        {
            var files = Files(Main);
            var entries = new Dictionary<string, byte[]>(files) { [name] = Bytes("x"), [ExtensionPackageLayout.ManifestFileName] = Bytes(ManifestJson(files)) };

            Assert.Equal(ExtensionPackageProblem.BadFileName, ProblemOf(Zip(entries)));
        }

        [Theory]
        [InlineData("evil.exe")]
        [InlineData("RFiDGear.Extensions.Sample.dll.config")]
        [InlineData("a b.dll")]
        [InlineData("..dll")]
        [InlineData("extension.json")]
        public void AManifestThatListsAFileOfAnotherTypeOrAnOddName_IsRefused(string name)
        {
            var files = Files(Main);
            files[name] = Bytes("x");

            Assert.Equal(ExtensionPackageProblem.BadFileName, ProblemOf(Package(files)));
        }

        [Theory]
        [InlineData("RFiDGear.Contracts.dll")]
        [InlineData("RFiDGear.ExtensionSdk.dll")]
        [InlineData("RFiDGear.dll")]
        [InlineData("Serilog.dll")]
        [InlineData("GemBox.Pdf.dll")]
        [InlineData("serilog.DLL")]
        [InlineData("RFiDGear.Extensions.Other.dll")]
        public void AFileThatBelongsToRFiDGearOrToAnotherExtension_IsRefused(string name)
        {
            var files = Files(Main);
            files[name] = Bytes("x");

            Assert.Equal(ExtensionPackageProblem.HostOwnedFile, ProblemOf(Package(files)));
        }

        [Fact]
        public void TheSupportLibrariesAndThePayloadOfTheExtension_AreAllowed()
        {
            Assert.True(ExtensionPackageReader.Read(Package(Files(Main, "RFiDGear.ExtensionLibraries.CardCheck.dll", "System.Data.SqlClient.dll", "sni.dll", Id + ".pdb"))).Success);
        }

        [Fact]
        public void AnExtensionWithoutItsOwnAssembly_IsNoPackage()
        {
            Assert.Equal(ExtensionPackageProblem.MainAssemblyMissing, ProblemOf(Package(Files("sni.dll"))));
        }

        [Fact]
        public void TheArchiveHoldsExactlyTheFilesTheManifestLists()
        {
            var files = Files(Main, "sni.dll");

            // A file that is not listed, and a file that is listed and not there.
            var unlisted = new Dictionary<string, byte[]>(files) { ["extra.dll"] = Bytes("x"), [ExtensionPackageLayout.ManifestFileName] = Bytes(ManifestJson(files)) };
            Assert.Equal(ExtensionPackageProblem.FileListMismatch, ProblemOf(Zip(unlisted)));

            var missing = new Dictionary<string, byte[]> { [Main] = files[Main], [ExtensionPackageLayout.ManifestFileName] = Bytes(ManifestJson(files)) };
            Assert.Equal(ExtensionPackageProblem.FileListMismatch, ProblemOf(Zip(missing)));
        }

        [Fact]
        public void AFileThatIsNotWhatTheManifestSays_IsRefused()
        {
            var files = Files(Main);
            var manifest = ManifestJson(files);
            files[Main] = Bytes("another content");

            Assert.Equal(ExtensionPackageProblem.HashMismatch, ProblemOf(Package(files, manifest)));
        }

        [Fact]
        public void TheSameNameTwice_IsRefused()
        {
            var files = Files(Main);
            var list = $"{{\"name\":\"{Main}\",\"sha256\":\"{Hash(files[Main])}\"}},{{\"name\":\"{Main.ToUpperInvariant()}\",\"sha256\":\"{Hash(files[Main])}\"}}";
            var manifest = $"{{\"schema\":1,\"id\":\"{Id}\",\"displayName\":\"Sample\",\"version\":\"1.1.0\",\"files\":[{list}]}}";

            Assert.Equal(ExtensionPackageProblem.BadFileName, ProblemOf(Package(files, manifest)));
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("null")]
        [InlineData("{\"schema\":1,\"id\":\"Other.Name\",\"displayName\":\"x\",\"version\":\"1.0\",\"files\":[]}")]
        public void AManifestOfTheWrongShape_IsRefused(string manifest)
        {
            Assert.Equal(ExtensionPackageProblem.BadManifest, ProblemOf(Package(Files(Main), manifest)));
        }

        [Fact]
        public void AManifestWithABadVersionOrHash_IsRefused()
        {
            var files = Files(Main);

            Assert.Equal(ExtensionPackageProblem.BadManifest, ProblemOf(Package(files, ManifestJson(files, version: "one"))));
            Assert.Equal(ExtensionPackageProblem.BadManifest, ProblemOf(Package(files, ManifestJson(files, minHost: "newer"))));
            Assert.Equal(ExtensionPackageProblem.BadManifest, ProblemOf(Package(files, ManifestJson(files).Replace(Hash(files[Main]), "abc"))));
        }

        [Fact]
        public void APackageOfAFormatThatThisVersionDoesNotKnow_SaysSo()
        {
            var files = Files(Main);

            Assert.Equal(ExtensionPackageProblem.UnsupportedSchema, ProblemOf(Package(files, ManifestJson(files, schema: 2))));
        }

        [Fact]
        public void MoreFilesThanAPackageMayHave_AreRefused()
        {
            var files = Files(new[] { Main }.Concat(Enumerable.Range(0, ExtensionPackageReader.MaxFiles + 1).Select(i => $"lib{i}.dll")).ToArray());

            Assert.Equal(ExtensionPackageProblem.TooLarge, ProblemOf(Package(files)));
        }

        // ── installing ──

        private string UserRoot => Path.Combine(_root, "user");

        private string MachineFolder => Path.Combine(_root, "machine");

        private static readonly Version Host = new Version(2, 1, 0, 0);

        // The installer reads the version of the main file; the content of a test package is no real assembly. A file that was
        // just unpacked (it is in the staging folder) has the version a test gives, any other file the one it was given in
        // InstalledVersions (none: the file has no version, as a text file has none).
        private ExtensionPackageInstaller Installer(Version? fileVersion = null, Version? host = null, Func<string, SignerInfo?>? signer = null) =>
            new ExtensionPackageInstaller(
                UserRoot,
                MachineFolder,
                host ?? Host,
                path => path.Contains(ExtensionPackageLayout.StagingFolderName, StringComparison.Ordinal)
                    ? fileVersion ?? new Version(1, 1, 0, 0)
                    : InstalledVersions.GetValueOrDefault(path),
                signer ?? (_ => null));

        private Dictionary<string, Version?> InstalledVersions { get; } = new Dictionary<string, Version?>(StringComparer.OrdinalIgnoreCase);

        private string WritePackage(Dictionary<string, byte[]> files, string? manifest = null)
        {
            Directory.CreateDirectory(_root);
            var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ExtensionPackageLayout.PackageExtension);
            using var package = Package(files, manifest);
            File.WriteAllBytes(path, package.ToArray());
            return path;
        }

        private void InstallMachineCopy(Version version)
        {
            Directory.CreateDirectory(MachineFolder);
            var path = Path.Combine(MachineFolder, Main);
            File.WriteAllText(path, "machine");
            InstalledVersions[path] = version;
        }

        [Fact]
        public void APackage_IsInstalledInAFolderOfItsVersion_AndNothingIsLeftOfTheStaging()
        {
            var files = Files(Main, "sni.dll");
            var installer = Installer();

            var prepared = installer.Prepare(WritePackage(files));

            Assert.Null(prepared.Problem);
            Assert.True(Directory.Exists(prepared.Staged!.StagingFolder));
            Assert.Null(prepared.Staged.InstalledVersion);

            Assert.Null(installer.Commit(prepared.Staged));

            var target = Path.Combine(UserRoot, Id, Ver(1, 1));
            Assert.Equal(files[Main], File.ReadAllBytes(Path.Combine(target, Main)));
            Assert.Equal(files["sni.dll"], File.ReadAllBytes(Path.Combine(target, "sni.dll")));
            Assert.False(Directory.Exists(prepared.Staged.StagingFolder));
        }

        [Fact]
        public void WhoSignedTheExtension_IsHandedToThePersonWhoIsAsked()
        {
            var installer = Installer(signer: path => Path.GetFileName(path) == Main ? new SignerInfo("Example Corp", true) : null);

            var prepared = installer.Prepare(WritePackage(Files(Main)));

            Assert.Equal(new SignerInfo("Example Corp", true), prepared.Staged!.Signer);
            installer.Discard(prepared.Staged);
        }

        [Fact]
        public void APackageThatNeedsANewerRFiDGear_IsNotInstalled()
        {
            var installer = Installer(host: new Version(2, 0, 0, 0));

            var prepared = installer.Prepare(WritePackage(Files(Main), ManifestJson(Files(Main), minHost: "2.1.0")));

            Assert.Equal(ExtensionPackageProblem.HostTooOld, prepared.Problem);
            Assert.False(Directory.Exists(Path.Combine(UserRoot, ExtensionPackageLayout.StagingFolderName)));
        }

        [Fact]
        public void TheSameOrAnOlderVersion_IsNotInstalled_ForTheVersionThatWouldBeUsedIsHigher()
        {
            InstallMachineCopy(new Version(1, 1, 0, 0));
            Assert.Equal(ExtensionPackageProblem.AlreadyInstalled, Installer().Prepare(WritePackage(Files(Main))).Problem);

            InstalledVersions[Path.Combine(MachineFolder, Main)] = new Version(1, 2, 0, 0);
            Assert.Equal(ExtensionPackageProblem.NotNewer, Installer().Prepare(WritePackage(Files(Main))).Problem);

            InstalledVersions[Path.Combine(MachineFolder, Main)] = new Version(1, 0, 0, 0);
            var newer = Installer().Prepare(WritePackage(Files(Main)));
            Assert.Null(newer.Problem);
            Assert.Equal(new Version(1, 0, 0, 0), newer.Staged!.InstalledVersion);
            Installer().Discard(newer.Staged);
        }

        [Fact]
        public void AnAssemblyThatIsNotOfTheVersionTheManifestSays_IsNotInstalled()
        {
            var prepared = Installer(fileVersion: new Version(1, 0, 5, 0)).Prepare(WritePackage(Files(Main)));

            Assert.Equal(ExtensionPackageProblem.VersionMismatch, prepared.Problem);
            Assert.Empty(Directory.EnumerateDirectories(Path.Combine(UserRoot, ExtensionPackageLayout.StagingFolderName)));
        }

        [Fact]
        public void AVersionFolderThatIsThereIsNeverOverwritten()
        {
            // Two packages of the same version are prepared before either is installed, as two windows could.
            var installer = Installer();
            var first = installer.Prepare(WritePackage(Files(Main)));
            var second = installer.Prepare(WritePackage(Files(Main)));
            Assert.Null(first.Problem);
            Assert.Null(second.Problem);

            Assert.Null(installer.Commit(first.Staged!));
            var marker = Path.Combine(UserRoot, Id, Ver(1, 1), "marker.txt");
            File.WriteAllText(marker, "first");

            Assert.Equal(ExtensionPackageProblem.AlreadyInstalled, installer.Commit(second.Staged!));
            Assert.False(Directory.Exists(second.Staged!.StagingFolder));
            Assert.Equal("first", File.ReadAllText(marker));
        }

        [Fact]
        public void APackageThatIsDiscarded_LeavesNothingBehind()
        {
            var installer = Installer();
            var prepared = installer.Prepare(WritePackage(Files(Main)));

            installer.Discard(prepared.Staged!);

            Assert.False(Directory.Exists(prepared.Staged!.StagingFolder));
            Assert.False(Directory.Exists(Path.Combine(UserRoot, Id)));
        }

        [Fact]
        public void AFolderThatCannotBeWritten_IsNotInstalledTo()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(UserRoot, "a file where the folder should be");

            Assert.Equal(ExtensionPackageProblem.CannotWrite, Installer().Prepare(WritePackage(Files(Main))).Problem);
        }

        [Fact]
        public void BeforeTheStart_WhatAnInstallLeftHalfDone_AndTheOldVersions_AreRemoved()
        {
            Directory.CreateDirectory(Path.Combine(UserRoot, ExtensionPackageLayout.StagingFolderName, "half-done"));
            foreach (var version in new[] { Ver(1, 0), Ver(1, 1), Ver(1, 2), Ver(1, 3) })
            {
                Directory.CreateDirectory(Path.Combine(UserRoot, Id, version));
            }

            Directory.CreateDirectory(Path.Combine(UserRoot, "CardCheckAssistant", Ver(1, 0)));   // not an extension: left alone

            ExtensionPackageInstaller.Tidy(UserRoot);

            Assert.Empty(Directory.EnumerateDirectories(Path.Combine(UserRoot, ExtensionPackageLayout.StagingFolderName)));
            Assert.Equal(new[] { Ver(1, 2), Ver(1, 3) }, Directory.EnumerateDirectories(Path.Combine(UserRoot, Id)).Select(Path.GetFileName).OrderBy(name => name).ToArray());
            Assert.True(Directory.Exists(Path.Combine(UserRoot, "CardCheckAssistant", Ver(1, 0))));
        }

        // ── which copy is loaded ──

        private string MakeInstalled(string version, string id = Id)
        {
            var folder = Path.Combine(UserRoot, id, version);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, id + ".dll");
            File.WriteAllText(path, version);
            return path;
        }

        private string MakeMachine(string id = Id)
        {
            Directory.CreateDirectory(MachineFolder);
            var path = Path.Combine(MachineFolder, id + ".dll");
            File.WriteAllText(path, "machine");
            return path;
        }

        // The file version that each given file has; any other file has none.
        private static Func<string, Version?> Versions(params (string Path, string Version)[] known) =>
            path => known.Where(entry => entry.Path == path).Select(entry => System.Version.Parse(entry.Version)).FirstOrDefault();

        [Fact]
        public void TheHigherVersionIsTheOneThatIsUsed_AndTheOtherIsOnlyAFallback()
        {
            var machine = MakeMachine();
            var user = MakeInstalled(Ver(1, 2));

            var selected = Assert.Single(ExtensionSelector.Select(new[] { machine }, UserRoot, Versions((machine, Ver(1, 1)), (user, Ver(1, 2)))));

            Assert.Equal(new[] { user, machine }, selected.Candidates.Select(candidate => candidate.AssemblyPath));
            Assert.True(selected.Candidates[0].IsUserInstalled);
        }

        [Fact]
        public void TheMachineWideCopyIsUsed_WhenItIsTheSameVersionOrNewer()
        {
            var machine = MakeMachine();
            var user = MakeInstalled(Ver(1, 1));

            Assert.Equal(machine, Assert.Single(ExtensionSelector.Select(new[] { machine }, UserRoot, Versions((machine, Ver(1, 1)), (user, Ver(1, 1))))).Candidates[0].AssemblyPath);
            Assert.Equal(machine, Assert.Single(ExtensionSelector.Select(new[] { machine }, UserRoot, Versions((machine, Ver(1, 3)), (user, Ver(1, 1))))).Candidates[0].AssemblyPath);
        }

        [Fact]
        public void AnExtensionThatIsOnlyInstalledForTheUser_IsUsed_AndItsNewestVersionFolderWins()
        {
            MakeInstalled(Ver(1, 0));
            var newest = MakeInstalled(Ver(1, 10));
            MakeInstalled(Ver(1, 9));

            var selected = Assert.Single(ExtensionSelector.Select(Array.Empty<string>(), UserRoot, Versions((newest, Ver(1, 10)))));

            Assert.Equal(newest, Assert.Single(selected.Candidates).AssemblyPath);   // 1.10 is newer than 1.9
        }

        [Fact]
        public void WhatIsNoInstalledExtension_IsNotLookedAt()
        {
            Directory.CreateDirectory(Path.Combine(UserRoot, ExtensionPackageLayout.StagingFolderName, "abc"));
            File.WriteAllText(Path.Combine(UserRoot, ExtensionPackageLayout.StagingFolderName, "abc", Main), "x");
            Directory.CreateDirectory(Path.Combine(UserRoot, "CardCheckAssistant", Ver(1, 0)));
            Directory.CreateDirectory(Path.Combine(UserRoot, Id, "not-a-version"));
            File.WriteAllText(Path.Combine(UserRoot, Id, "not-a-version", Main), "x");
            Directory.CreateDirectory(Path.Combine(UserRoot, Id, Ver(1, 0)));   // a folder without the extension in it

            Assert.Empty(ExtensionSelector.Select(Array.Empty<string>(), UserRoot, _ => null));
            Assert.Empty(ExtensionSelector.Select(Array.Empty<string>(), Path.Combine(_root, "no-such-folder"), _ => null));
            Assert.Empty(ExtensionSelector.Select(Array.Empty<string>(), null, _ => null));
        }

        private sealed class CatalogLog
        {
            public List<string> Tried { get; } = new List<string>();
        }

        private MefHelper Helper(CatalogLog log, Func<string, bool> fails)
        {
            return new MefHelper(
                MachineFolder,
                typeof(IRFiDGearExtensionHost).Assembly,
                path =>
                {
                    log.Tried.Add(path);
                    return fails(path) ? throw new InvalidOperationException("broken") : new TypeCatalog();
                },
                UserRoot);
        }

        [Fact]
        public void RFiDGearLoadsTheExtensionOnce_FromTheFolderThatWins()
        {
            var machine = MakeMachine();
            var user = MakeInstalled(Ver(9, 0));   // no real file version: the folder name is the version
            var log = new CatalogLog();
            using var cleanup = new Disposer(Helper(log, _ => false));
            var helper = cleanup.Helper;

            helper.Compose();

            var result = Assert.Single(helper.LastLoadResults);
            Assert.True(result.Loaded);
            Assert.True(result.IsUserInstalled);
            Assert.Equal(user, result.AssemblyPath);
            Assert.Equal(new[] { user }, log.Tried);
            Assert.Equal(user, helper.FindExtensionAssembly(Id));   // the resolver is told the same, so the older copy is never loaded
            Assert.NotEqual(machine, helper.FindExtensionAssembly(Id));
        }

        [Fact]
        public void WhenTheInstalledCopyCannotBeLoaded_TheMachineWideOneIsUsed_AndItIsSaid()
        {
            var machine = MakeMachine();
            var user = MakeInstalled(Ver(9, 0));
            var log = new CatalogLog();
            using var cleanup = new Disposer(Helper(log, path => path == user));
            var helper = cleanup.Helper;

            helper.Compose();

            Assert.Equal(new[] { user, machine }, helper.LastLoadResults.Select(result => result.AssemblyPath));
            Assert.False(helper.LastLoadResults[0].Loaded);
            Assert.NotNull(helper.LastLoadResults[0].Error);
            Assert.True(helper.LastLoadResults[1].Loaded);
            Assert.Equal(machine, helper.FindExtensionAssembly(Id));
        }

        [Fact]
        public void WithoutAFolderOfInstalledExtensions_OnlyTheMachineWideFolderIsLooked_AsBefore()
        {
            var machine = MakeMachine();
            var log = new CatalogLog();
            using var cleanup = new Disposer(new MefHelper(MachineFolder, typeof(IRFiDGearExtensionHost).Assembly, path => { log.Tried.Add(path); return new TypeCatalog(); }));

            cleanup.Helper.Compose();

            Assert.Equal(new[] { machine }, log.Tried);
            Assert.False(Assert.Single(cleanup.Helper.LastLoadResults).IsUserInstalled);
            Assert.Null(cleanup.Helper.FindExtensionAssembly("RFiDGear.Extensions.Unknown"));
        }

        private sealed class Disposer : IDisposable
        {
            public Disposer(MefHelper helper) => Helper = helper;

            public MefHelper Helper { get; }

            public void Dispose()
            {
                // MefHelper implements IDisposable explicitly, so it is only reachable through the interface.
                IDisposable helper = Helper;
                helper.Dispose();
            }
        }
    }
}
