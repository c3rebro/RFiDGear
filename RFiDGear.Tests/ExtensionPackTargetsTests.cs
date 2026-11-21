using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Infrastructure.ExtensionPackages;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The step that makes an installable package of a built extension, for the person who ships an update. It runs the real
    /// target with MSBuild on a small generated project that imports a copy of the targets file, and reads what it wrote
    /// with the same reader that RFiDGear installs with: a package that the build makes is a package that is accepted.
    /// </summary>
    public sealed class ExtensionPackTargetsTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();

        public void Dispose() => _folder.Dispose();

        private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

        private string Project => Path.Combine(_folder.Path, "project with space");

        private string Out => Path.Combine(_folder.Path, "out");

        private string PackageFolder => Path.Combine(Project, "bin", "package");

        // The name that the person is asked about; a test may give one that is full of what JSON and MSBuild take for syntax.
        private string DisplayName { get; set; } = "Fake Extension";

        private void WriteOutput()
        {
            Directory.CreateDirectory(Out);
            foreach (var name in new[]
            {
                "RFiDGear.Extensions.Fake.dll", "RFiDGear.Extensions.Fake.pdb", "RFiDGear.ExtensionLibraries.Fake.dll",
                "sni.dll", "System.Data.SqlClient.dll",
                "RFiDGear.ExtensionSdk.dll", "RFiDGear.Contracts.dll", "Serilog.dll", "Unrelated.dll",
            })
            {
                File.WriteAllText(Path.Combine(Out, name), "content of " + name);
            }
        }

        private string WriteProject()
        {
            Directory.CreateDirectory(Project);
            File.Copy(Path.Combine(RepositoryRoot(), "RFiDGear.Extensions", "PackExtension.targets"), Path.Combine(Project, "PackExtension.targets"), overwrite: true);

            var project = Path.Combine(Project, "fake.proj");
            File.WriteAllText(
                project,
                "<Project>"
                + "<PropertyGroup><TargetName>RFiDGear.Extensions.Fake</TargetName><OutDir>" + Out + Path.DirectorySeparatorChar + "</OutDir>"
                + "<IntermediateOutputPath>obj\\</IntermediateOutputPath><Version>1.2.3</Version><Platform>x64</Platform>"
                + "<ExtensionDisplayName>" + System.Security.SecurityElement.Escape(DisplayName) + "</ExtensionDisplayName><ExtensionMinHostVersion>2.1.0</ExtensionMinHostVersion></PropertyGroup>"
                + "<ItemGroup>"
                + "<ExtensionPayloadFile Include=\"$(OutDir)sni.dll\" Condition=\"Exists('$(OutDir)sni.dll')\" />"
                + "<ExtensionPayloadFile Include=\"$(OutDir)System.Data.SqlClient.dll\" Condition=\"Exists('$(OutDir)System.Data.SqlClient.dll')\" />"
                + "</ItemGroup>"
                + "<Import Project=\"PackExtension.targets\" /><Target Name=\"Build\" /></Project>");

            return project;
        }

        private async Task<(int ExitCode, string Output)> Build(params string[] properties)
        {
            var project = WriteProject();
            var start = new ProcessStartInfo(DotNetTool.Executable())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(project)!,
            };
            start.ArgumentList.Add("msbuild");
            start.ArgumentList.Add(project);
            start.ArgumentList.Add("-t:Build");
            start.ArgumentList.Add("-nologo");
            start.ArgumentList.Add("-v:m");
            foreach (var property in properties)
            {
                start.ArgumentList.Add(property);
            }

            start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            start.Environment["VSLANG"] = "1033";

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            return (process.ExitCode, await output + await errors);
        }

        [Fact]
        public async Task APackageIsMade_WithoutAskingForIt_AndTheBuildSaysWhereItIs()
        {
            WriteOutput();

            var (exitCode, output) = await Build();

            Assert.True(exitCode == 0, output);
            var path = Assert.Single(Directory.GetFiles(PackageFolder));
            Assert.Equal("RFiDGear.Extensions.Fake-1.2.3-x64.rfext", Path.GetFileName(path));
            Assert.Contains("Extension package: " + path, output, StringComparison.Ordinal);
            Assert.True(ExtensionPackageReader.Read(path).Success);
        }

        [Fact]
        public async Task NoPackageIsMade_WhenItIsSwitchedOff()
        {
            WriteOutput();

            var (exitCode, output) = await Build("-p:PackExtension=false");

            Assert.True(exitCode == 0, output);
            Assert.False(Directory.Exists(PackageFolder));
        }

        [Fact]
        public async Task ABuildForAnyCpu_ThatDidNotAskForAPackage_WarnsAndGoesOn()
        {
            WriteOutput();

            // The payload (sni.dll) is native, so an AnyCPU build cannot say which platform a package would be for: it is no reason to
            // fail a build that never asked for one, but the person is told why there is none.
            var (exitCode, output) = await Build("-p:Platform=AnyCPU");

            Assert.True(exitCode == 0, output);
            Assert.Contains("No extension package was made", output, StringComparison.Ordinal);
            Assert.Contains("ExtensionPackagePlatform", output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(PackageFolder) && Directory.GetFiles(PackageFolder).Length > 0);
        }

        [Fact]
        public async Task AProjectThatNamesThePlatformOfItsPayload_GetsAPackageForAnyCpuWithoutAskingForIt()
        {
            WriteOutput();

            var (exitCode, output) = await Build("-p:Platform=AnyCPU", "-p:ExtensionPackagePlatform=x86");

            Assert.True(exitCode == 0, output);
            Assert.Equal("RFiDGear.Extensions.Fake-1.2.3-x86.rfext", Path.GetFileName(Assert.Single(Directory.GetFiles(PackageFolder))));
        }

        [Fact]
        public async Task AnotherConfigurationThanRelease_HasAFileNameOfItsOwn_AndNeverReplacesTheReleasePackage()
        {
            WriteOutput();
            Assert.Equal(0, (await Build("-p:Configuration=Release")).ExitCode);
            var release = Path.Combine(PackageFolder, "RFiDGear.Extensions.Fake-1.2.3-x64.rfext");
            var before = File.ReadAllBytes(release);

            var (exitCode, output) = await Build("-p:Configuration=Debug");

            Assert.True(exitCode == 0, output);
            Assert.Equal(
                new[] { "RFiDGear.Extensions.Fake-1.2.3-x64-Debug.rfext", "RFiDGear.Extensions.Fake-1.2.3-x64.rfext" },
                Directory.GetFiles(PackageFolder).Select(file => Path.GetFileName(file)).OrderBy(name => name, StringComparer.Ordinal).ToArray());
            Assert.Equal(before, File.ReadAllBytes(release));
        }

        [Fact]
        public async Task AFolderThatCannotBeMade_IsAWarningForABuildThatDidNotAsk_AndNoPackageIsLeftHalfWritten()
        {
            WriteOutput();

            // A folder cannot be made below a file.
            var file = Path.Combine(_folder.Path, "a file");
            File.WriteAllText(file, "not a folder");
            var folder = "-p:ExtensionPackageFolder=" + Path.Combine(file, "package");

            var (exitCode, output) = await Build(folder);

            Assert.True(exitCode == 0, output);
            Assert.Contains("warning", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Extension package:", output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(PackageFolder));

            var (askedExitCode, _) = await Build(folder, "-p:PackExtension=true");
            Assert.NotEqual(0, askedExitCode);
        }

        [Fact]
        public async Task APackageFileThatIsHeldOpen_IsAWarningForABuildThatDidNotAsk_AndAnErrorForOneThatDid()
        {
            WriteOutput();
            Assert.Equal(0, (await Build()).ExitCode);
            var path = Assert.Single(Directory.GetFiles(PackageFolder));

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var (exitCode, output) = await Build();
                Assert.True(exitCode == 0, output);
                Assert.Contains("warning", output, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Extension package:", output, StringComparison.Ordinal);

                var (askedExitCode, _) = await Build("-p:PackExtension=true");
                Assert.NotEqual(0, askedExitCode);
            }
        }

        [Fact]
        public async Task TheExtensionItsLibrariesAndItsPayload_AreInThePackage_AndNothingElse()
        {
            WriteOutput();

            var (exitCode, output) = await Build("-p:PackExtension=true");

            Assert.True(exitCode == 0, output);
            var path = Assert.Single(Directory.GetFiles(PackageFolder));
            Assert.Equal("RFiDGear.Extensions.Fake-1.2.3-x64.rfext", Path.GetFileName(path));

            // The package is read by what installs it: every file has the hash that the manifest names.
            var read = ExtensionPackageReader.Read(path);
            Assert.True(read.Success, read.Problem?.ToString());
            var manifest = read.Package!.Manifest;
            Assert.Equal("RFiDGear.Extensions.Fake", manifest.Id);
            Assert.Equal("Fake Extension", manifest.DisplayName);
            Assert.Equal(new Version(1, 2, 3, 0), manifest.Version);
            Assert.Equal(new Version(2, 1, 0, 0), manifest.MinHostVersion);
            Assert.Equal("x64", manifest.Platform);   // what the build was for: the payload is native
            Assert.Equal(
                new[] { "RFiDGear.ExtensionLibraries.Fake.dll", "RFiDGear.Extensions.Fake.dll", "System.Data.SqlClient.dll", "sni.dll" },
                read.Package.Files.Select(file => file.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public async Task APackageWithNativeFiles_IsNotMadeForAnyCpu_AndTheProjectCanNameThePlatformOfItsPayload()
        {
            WriteOutput();

            // The payload (sni.dll) is native, so an AnyCPU build cannot say which platform the package is for.
            var (anyExitCode, anyOutput) = await Build("-p:PackExtension=true", "-p:Platform=AnyCPU");
            Assert.NotEqual(0, anyExitCode);
            Assert.Contains("ExtensionPackagePlatform", anyOutput, StringComparison.Ordinal);
            Assert.False(Directory.Exists(PackageFolder) && Directory.GetFiles(PackageFolder).Length > 0, "no package for a platform that is not known");

            // The project says what its payload is for (as the Card Check Assistant does when RFiDGearSqlNativePlatform is set).
            var (exitCode, output) = await Build("-p:PackExtension=true", "-p:Platform=AnyCPU", "-p:ExtensionPackagePlatform=x86");
            Assert.True(exitCode == 0, output);
            var path = Assert.Single(Directory.GetFiles(PackageFolder));
            Assert.Equal("RFiDGear.Extensions.Fake-1.2.3-x86.rfext", Path.GetFileName(path));
            Assert.Equal("x86", ExtensionPackageReader.Read(path).Package!.Manifest.Platform);
        }

        [Theory]
        [InlineData("RFID \"Pro\" Suite")]
        [InlineData("Back\\slash and \"quotes\" \\\"both\\\"")]
        [InlineData("Muster; GmbH & Co. <KG>")]
        public async Task ANameWithQuotesBackslashesOrSemicolons_IsWrittenIntoTheManifestAsItIs(string displayName)
        {
            WriteOutput();
            DisplayName = displayName;

            var (exitCode, output) = await Build("-p:PackExtension=true");

            Assert.True(exitCode == 0, output);
            var read = ExtensionPackageReader.Read(Assert.Single(Directory.GetFiles(PackageFolder)));
            Assert.True(read.Success, read.Problem?.ToString());
            Assert.Equal(displayName, read.Package!.Manifest.DisplayName);
        }

        [Fact]
        public async Task ABuildAfterAnotherOne_LeavesOnlyWhatTheLastOneMade()
        {
            WriteOutput();
            Assert.Equal(0, (await Build("-p:PackExtension=true")).ExitCode);

            // A file of the first build that the second does not have is not in the second package.
            File.Delete(Path.Combine(Out, "System.Data.SqlClient.dll"));
            var (exitCode, output) = await Build("-p:PackExtension=true", "-p:Version=1.2.4");

            Assert.True(exitCode == 0, output);
            var read = ExtensionPackageReader.Read(Path.Combine(PackageFolder, "RFiDGear.Extensions.Fake-1.2.4-x64.rfext"));
            Assert.True(read.Success, read.Problem?.ToString());
            Assert.DoesNotContain(read.Package!.Files, file => file.Name == "System.Data.SqlClient.dll");
        }
    }
}
