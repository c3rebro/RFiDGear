using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>
    /// The files that the order database needs beside the extension: the real System.Data.SqlClient.dll and the sni.dll of
    /// the platform. This project cannot see a missing one, because its own deps.json lists every package; so the tests ask
    /// MSBuild what the build would copy, and then use exactly those files in a process that knows nothing else.
    /// </summary>
    public sealed class SqlDriverPayloadTests : IDisposable
    {
        private const string DriverFile = "System.Data.SqlClient.dll";

        private const string NativeFile = "sni.dll";

        // Loads the driver from the folder it is given and opens a connection to a port nobody listens on. A driver that works
        // answers with a SqlException; a missing sni.dll or the stub of the package answers with something else.
        private const string ProbeSource = """
            using System;
            using System.IO;
            using System.Reflection;

            try
            {
                var assembly = Assembly.LoadFrom(Path.Combine(args[0], "System.Data.SqlClient.dll"));
                var type = assembly.GetType("System.Data.SqlClient.SqlConnection", throwOnError: true)!;
                var connection = (IDisposable)Activator.CreateInstance(type, "Server=127.0.0.1,1;Integrated Security=true;Connect Timeout=2")!;
                try
                {
                    type.GetMethod("Open", Type.EmptyTypes)!.Invoke(connection, null);
                    Console.WriteLine("RESULT: opened");
                }
                finally
                {
                    connection.Dispose();
                }
            }
            catch (TargetInvocationException ex)
            {
                Console.WriteLine("RESULT: " + ex.InnerException!.GetType().FullName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("RESULT: " + ex.GetType().FullName);
            }
            """;

        private readonly TempFolder _folder = new TempFolder();

        public void Dispose() => _folder.Dispose();

        private static string Library =>
            Path.Combine(DotNetTool.RepositoryRoot(), "RFiDGear.ExtensionLibraries", "CardCheck", "RFiDGear.ExtensionLibraries.CardCheck.csproj");

        private static string Extension =>
            Path.Combine(DotNetTool.RepositoryRoot(), "RFiDGear.Extensions", "CardCheckAssistant", "RFiDGear.Extensions.CardCheckAssistant.csproj");

        // The full paths MSBuild reports for a result set, whatever the JSON around them (a message before it is skipped).
        private static string[] Identities(string output, params string[] path)
        {
            using var json = JsonDocument.Parse(output[output.IndexOf('{', StringComparison.Ordinal)..]);
            var node = path.Aggregate(json.RootElement, (element, name) => element.GetProperty(name));

            return node.EnumerateArray().Select(item => item.GetProperty("Identity").GetString()!).ToArray();
        }

        // What the library says the build must carry beside it, for a platform.
        private static async Task<string[]> PayloadAsync(string platform, string? nativePlatform = null)
        {
            var arguments = new List<string> { "msbuild", Library, "-t:GetSqlDriverPayload", "-nologo", "-p:Platform=" + platform, "-getTargetResult:GetSqlDriverPayload" };
            if (nativePlatform != null)
            {
                arguments.Add("-p:RFiDGearSqlNativePlatform=" + nativePlatform);
            }

            var (exitCode, output) = await DotNetTool.RunAsync(Path.GetDirectoryName(Library)!, arguments.ToArray());
            Assert.True(exitCode == 0, output);

            return Identities(output, "TargetResults", "GetSqlDriverPayload", "Items");
        }

        [Theory]
        [InlineData("x64", null, "win-x64")]
        [InlineData("x86", null, "win-x86")]
        [InlineData("AnyCPU", null, "win-x64")]    // a build for any CPU takes the platform of the system, which is 64-bit
        [InlineData("AnyCPU", "x86", "win-x86")]   // for an RFiDGear of the other bitness
        public async Task Payload_IsTheRealDriverAndTheNativeFileOfThePlatform(string platform, string? nativePlatform, string runtime)
        {
            Assert.SkipUnless(Environment.Is64BitOperatingSystem, "the default platform of a build for any CPU is the system's");

            var files = await PayloadAsync(platform, nativePlatform);

            Assert.Equal(2, files.Length);
            Assert.EndsWith(Path.Combine("runtimes", "win", "lib", "netcoreapp2.1", DriverFile), files[0], StringComparison.OrdinalIgnoreCase);   // not lib\, which holds a stub
            Assert.EndsWith(Path.Combine("runtimes", runtime, "native", NativeFile), files[1], StringComparison.OrdinalIgnoreCase);
            Assert.All(files, file => Assert.True(File.Exists(file), file));
        }

        [Fact]
        public async Task Payload_WorksInAProcessThatKnowsNothingElse_AndWithoutItsNativeFileItDoesNot()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64, "the probe runs as an x64 process, so it can use the x64 file");

            var files = await PayloadAsync("x64");
            var complete = Directory.CreateDirectory(Path.Combine(_folder.Path, "complete")).FullName;
            var withoutNative = Directory.CreateDirectory(Path.Combine(_folder.Path, "withoutNative")).FullName;
            File.Copy(files[0], Path.Combine(complete, DriverFile));
            File.Copy(files[1], Path.Combine(complete, NativeFile));
            File.Copy(files[0], Path.Combine(withoutNative, DriverFile));

            var program = Path.Combine(Directory.CreateDirectory(Path.Combine(_folder.Path, "probe")).FullName, "probe.cs");
            File.WriteAllText(program, ProbeSource);

            Assert.Equal("System.Data.SqlClient.SqlException", await ProbeAsync(program, complete));        // it reached the network
            Assert.Equal("System.DllNotFoundException", await ProbeAsync(program, withoutNative));          // and the probe can tell when it does not
        }

        private static async Task<string> ProbeAsync(string program, string folder)
        {
            var (exitCode, output) = await DotNetTool.RunAsync(Path.GetDirectoryName(program)!, "run", program, "--", folder);
            var result = output.Split('\n').Select(line => line.Trim()).LastOrDefault(line => line.StartsWith("RESULT: ", StringComparison.Ordinal));

            Assert.True(result != null, "the probe answered nothing (exit code " + exitCode + "): " + output);

            return result["RESULT: ".Length..];
        }

        [Fact]
        public async Task TheExtensionBuild_PutsThePayloadBesideTheExtension_AndNamesItForTheDeployStep()
        {
            var output = Path.Combine(_folder.Path, "out") + Path.DirectorySeparatorChar;

            var (exitCode, text) = await DotNetTool.RunAsync(
                Path.GetDirectoryName(Extension)!,
                "msbuild", Extension, "-t:CopySqlDriverToOutput", "-nologo", "-p:Platform=x64", "-p:OutDir=" + output, "-getItem:ExtensionPayloadFile,FileWrites");

            Assert.True(exitCode == 0, text);
            var named = Identities(text, "Items", "ExtensionPayloadFile");
            var expected = new[] { Path.Combine(output, DriverFile), Path.Combine(output, NativeFile) }.Select(Path.GetFullPath).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            Assert.Equal(expected, named.Select(Path.GetFullPath).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            Assert.All(named, file => Assert.True(File.Exists(file), file));

            // What the build writes, it also cleans: the files are known to Clean, or they would stay behind when the extension goes.
            var written = Identities(text, "Items", "FileWrites").Select(Path.GetFullPath).ToArray();
            Assert.All(expected, file => Assert.Contains(file, written, StringComparer.OrdinalIgnoreCase));
        }
    }
}
