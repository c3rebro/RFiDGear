using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Tests
{
    /// <summary>The dotnet command line, for the tests that run MSBuild or a program of their own in a process.</summary>
    internal static class DotNetTool
    {
        /// <summary>The root of the repository, found from the file of the caller.</summary>
        /// <param name="callerFile">The file of the calling test (filled in by the compiler).</param>
        /// <returns>The full path of the folder that holds the solution.</returns>
        internal static string RepositoryRoot([CallerFilePath] string callerFile = "")
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(callerFile)!);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RFiDGear.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("The solution folder was not found above " + callerFile);
        }

        /// <summary>
        /// The dotnet that has the SDK: the one that started the test run if it says so, else the one next to this runtime. Not
        /// found by searching the path (a 32-bit test host would also find a 32-bit install that has no SDK).
        /// </summary>
        /// <returns>The full path of dotnet.exe.</returns>
        internal static string Executable()
        {
            var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrEmpty(host) && File.Exists(host))
            {
                return host;
            }

            return Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        }

        /// <summary>Runs dotnet with the arguments, English messages and no build node left behind, and waits for it.</summary>
        /// <param name="workingDirectory">The folder to run in.</param>
        /// <param name="arguments">The arguments, one per entry.</param>
        /// <returns>The exit code and everything the process wrote (output and errors).</returns>
        internal static async Task<(int ExitCode, string Output)> RunAsync(string workingDirectory, params string[] arguments)
        {
            var start = new ProcessStartInfo(Executable())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
            };

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            start.Environment["DOTNET_NOLOGO"] = "1";
            start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            start.Environment["VSLANG"] = "1033";

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // A process that hangs must not keep its folder busy for the cleanup, or hide why the test failed.
                process.Kill(entireProcessTree: true);

                throw;
            }

            return (process.ExitCode, await output + await errors);
        }
    }
}
