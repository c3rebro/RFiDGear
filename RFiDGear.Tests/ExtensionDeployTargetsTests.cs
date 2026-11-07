using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The step that copies a built extension to RFiDGear's extension folder. It writes outside the repository, so what
    /// matters is that it does nothing unless asked, copies exactly the extension and its support libraries (never the
    /// contract, the SDK or the host, whose names the host's scan would also pick up), lets the last build replace what is
    /// there, and does not leave a new library next to an old extension. The tests run the real target with MSBuild on a
    /// small generated project that imports a copy of the targets file, so nothing of the developer's own setup (an
    /// environment variable, a local props file) changes the outcome.
    /// </summary>
    public sealed class ExtensionDeployTargetsTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();

        public void Dispose() => _folder.Dispose();

        private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

        // A folder name full of what MSBuild or XML would take for syntax, as a developer's own TEMP path may well be: the
        // output and the destination are below it, so every test also shows that such a path works.
        private string Base => Path.Combine(_folder.Path, "odd & name; 100% $(x) @y");

        private string Out => Path.Combine(Base, "out");

        private string Destination => Path.Combine(Base, "RFiDGear", "Extensions");

        // A value for an MSBuild property, written as the text MSBuild reads back as exactly this value.
        private static string MsBuildEscape(string value) =>
            value.Replace("%", "%25").Replace(";", "%3B").Replace("$", "%24").Replace("@", "%40").Replace("'", "%27").Replace("*", "%2A").Replace("?", "%3F");

        // What a build leaves in its output folder: the extension, its library, and what must never be copied.
        private static readonly string[] Wanted =
        {
            "RFiDGear.Extensions.Fake.dll", "RFiDGear.Extensions.Fake.pdb",
            "RFiDGear.ExtensionLibraries.Fake.dll", "RFiDGear.ExtensionLibraries.Fake.pdb",
        };

        private static readonly string[] Decoys =
        {
            "RFiDGear.ExtensionSdk.dll", "RFiDGear.Contracts.dll", "RFiDGear.dll", "RFiDGear.Extensions.Other.dll", "Serilog.dll",
        };

        private void WriteOutput(string content)
        {
            Directory.CreateDirectory(Out);

            foreach (var name in Wanted.Concat(Decoys))
            {
                File.WriteAllText(Path.Combine(Out, name), content);
            }
        }

        // Builds the little project in a folder of its own, so the local props file next to the real targets file is not seen.
        private string WriteProject()
        {
            var directory = Path.Combine(_folder.Path, "project");
            Directory.CreateDirectory(directory);
            File.Copy(Path.Combine(RepositoryRoot(), "RFiDGear.Extensions", "DeployToRFiDGear.targets"), Path.Combine(directory, "DeployToRFiDGear.targets"), overwrite: true);

            var project = Path.Combine(directory, "fake.proj");
            File.WriteAllText(
                project,
                "<Project>"
                + "<PropertyGroup><TargetName>RFiDGear.Extensions.Fake</TargetName><OutDir>" + SecurityElement.Escape(MsBuildEscape(Out + Path.DirectorySeparatorChar)) + "</OutDir>"
                + "<Configuration>Debug</Configuration><Platform>AnyCPU</Platform></PropertyGroup>"
                + "<Import Project=\"DeployToRFiDGear.targets\" /><Target Name=\"Build\" /></Project>");

            return project;
        }

        // The dotnet that has the SDK: the one that started the test run if it says so, else the one next to this runtime. Not
        // found by searching the path (a 32-bit test host would also find a 32-bit install that has no SDK).
        private static string DotNet()
        {
            var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrEmpty(host) && File.Exists(host))
            {
                return host;
            }

            return Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        }

        private Task<(int ExitCode, string Output)> Build(params string[] properties) => BuildWithSwitch(string.Empty, properties);

        // The environment switch is set to a known value: what a developer may have set for their own use must not change the outcome.
        private async Task<(int ExitCode, string Output)> BuildWithSwitch(string environmentSwitch, params string[] properties)
        {
            var project = WriteProject();
            var start = new ProcessStartInfo(DotNet())
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
            start.ArgumentList.Add("-p:RFiDGearExtensionsFolder=" + MsBuildEscape(Destination));
            foreach (var property in properties)
            {
                start.ArgumentList.Add(property);
            }

            start.Environment["RFIDGEAR_DEPLOY_EXTENSIONS"] = environmentSwitch;
            start.Environment["DeployExtensionsToRFiDGear"] = string.Empty;
            start.Environment["MSBUILDDISABLENODEREUSE"] = "1";   // no build node left behind, holding the folder
            start.Environment["VSLANG"] = "1033";                 // English messages

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
                // A build that hangs must not keep the folder busy for the cleanup, or hide why the test failed.
                process.Kill(entireProcessTree: true);

                throw;
            }

            return (process.ExitCode, await output + await errors);
        }

        private string[] Deployed() =>
            Directory.Exists(Destination)
                ? Directory.GetFiles(Destination).Select(file => Path.GetFileName(file)!).OrderBy(name => name, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();

        private static string[] Sorted(string[] names) => names.OrderBy(name => name, StringComparer.Ordinal).ToArray();

        [Fact]
        public async Task Deploy_DoesNothingUnlessAskedFor_NotEvenCreatingTheFolder()
        {
            WriteOutput("build 1");

            var (exitCode, output) = await Build();

            Assert.True(exitCode == 0, output);
            Assert.False(Directory.Exists(Destination));
        }

        [Fact]
        public async Task Deploy_CopiesTheExtensionAndItsLibraries_NeverTheContractTheSdkOrTheHost()
        {
            WriteOutput("build 1");

            var (exitCode, output) = await Build("-p:DeployExtensionsToRFiDGear=true");

            Assert.True(exitCode == 0, output);
            Assert.Equal(Sorted(Wanted), Deployed());
            Assert.Contains("all 4 file(s) are in place", output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Deploy_TheLastBuildReplacesWhatIsThere_EvenAnOlderOneOverANewerFile()
        {
            WriteOutput("build 1");
            await Build("-p:DeployExtensionsToRFiDGear=true");

            // A newer build replaces what is there, and so does a build of something older (another checkout, a review
            // of an earlier branch): what counts is which build ran last, not the time stamp of the file.
            WriteOutput("build 2, a little longer");
            Assert.True((await Build("-p:DeployExtensionsToRFiDGear=true")).ExitCode == 0);
            Assert.Equal("build 2, a little longer", File.ReadAllText(Path.Combine(Destination, "RFiDGear.Extensions.Fake.dll")));

            foreach (var file in Directory.GetFiles(Destination))
            {
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(1));
            }

            WriteOutput("older build");
            Assert.True((await Build("-p:DeployExtensionsToRFiDGear=true")).ExitCode == 0);
            Assert.Equal("older build", File.ReadAllText(Path.Combine(Destination, "RFiDGear.Extensions.Fake.dll")));
            Assert.Equal(Sorted(Wanted), Deployed());
        }

        [Fact]
        public async Task Deploy_WhenTheExtensionCannotBeReplaced_ItIsAWarning_NotAFailedBuild_AndNothingElseIsCopied()
        {
            // First a complete older set, then RFiDGear "runs": it holds the extension, which is what it loads first.
            WriteOutput("older build");
            await Build("-p:DeployExtensionsToRFiDGear=true");
            WriteOutput("new build, longer");
            var held = Path.Combine(Destination, "RFiDGear.Extensions.Fake.dll");

            int exitCode;
            string output;
            using (new FileStream(held, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                (exitCode, output) = await Build("-p:DeployExtensionsToRFiDGear=true");
            }

            Assert.True(exitCode == 0, output);
            Assert.Contains("MSB30", output, StringComparison.Ordinal);                  // the copy said what went wrong, as a warning
            Assert.Contains("NOT completely deployed", output, StringComparison.Ordinal);   // and the last line does not say "restart"
            Assert.DoesNotContain("Restart RFiDGear", output, StringComparison.Ordinal);

            // One older set that belongs together: no new library next to the old extension, no new symbols next to the old DLL.
            Assert.All(Wanted, name => Assert.Equal("older build", File.ReadAllText(Path.Combine(Destination, name))));
        }

        [Fact]
        public async Task Deploy_AnExtensionThatIsAlreadyThere_NeedsNoCopy_SoARunningRFiDGearIsNoProblem()
        {
            WriteOutput("build 1");
            await Build("-p:DeployExtensionsToRFiDGear=true");
            var held = Path.Combine(Destination, "RFiDGear.Extensions.Fake.dll");

            // The same build again, while RFiDGear holds the extension: nothing differs, so there is nothing to replace and
            // nothing to warn about. (Files count as the same when size and time stamp are; a build gives every file it
            // changes a new time stamp.)
            int exitCode;
            string output;
            using (new FileStream(held, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                (exitCode, output) = await Build("-p:DeployExtensionsToRFiDGear=true");
            }

            Assert.True(exitCode == 0, output);
            Assert.DoesNotContain("MSB30", output, StringComparison.Ordinal);
            Assert.Contains("all 4 file(s) are in place", output, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("true ")]   // what `set X=true && dotnet build` leaves in cmd.exe
        [InlineData("TRUE")]
        public async Task Deploy_TheEnvironmentSwitch_IsReadWithoutBlanksOrCase(string value)
        {
            WriteOutput("build 1");

            var (exitCode, output) = await BuildWithSwitch(value);

            Assert.True(exitCode == 0, output);
            Assert.Equal(Sorted(Wanted), Deployed());
        }

        [Fact]
        public async Task Deploy_ASwitchThatIsNotTrue_DoesNothing_AndSaysWhy()
        {
            WriteOutput("build 1");

            var (exitCode, output) = await BuildWithSwitch("1");

            Assert.True(exitCode == 0, output);
            Assert.False(Directory.Exists(Destination));
            Assert.Contains("only true (or yes, or on) turns it on", output, StringComparison.Ordinal);
        }

        [Fact]
        public void BothExtensionProjects_ImportTheTargets_AndTheMachineLocalFileIsNotInTheRepository()
        {
            foreach (var project in new[]
            {
                Path.Combine("CardCheckAssistant", "RFiDGear.Extensions.CardCheckAssistant.csproj"),
                Path.Combine("DesfirePluginSample", "RFiDGear.Extensions.DesfirePluginSample.csproj"),
            })
            {
                var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "RFiDGear.Extensions", project));
                Assert.Contains("<Import Project=\"..\\DeployToRFiDGear.targets\" />", text, StringComparison.Ordinal);
            }

            var ignored = File.ReadAllLines(Path.Combine(RepositoryRoot(), ".gitignore")).Select(line => line.Trim());
            Assert.Contains("/RFiDGear.Extensions/deploy.local.props", ignored);
        }
    }
}
