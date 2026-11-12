#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// What the person who runs the hardware tests has asked for, read from <c>hil.settings.json</c>. The reader-scan
    /// tests open a reader only when the file says so, so that they never touch a reader by accident: without the file
    /// they are reported as skipped. The file is looked for in the folder of the test assembly and in every folder above
    /// it, which finds the one next to <c>Run-HilTests.bat</c> from the build folder, from Visual Studio and from
    /// <c>dotnet test</c> alike. <c>Run-HilTests.bat -Interactive</c> turns the questions on for one run, whatever the file
    /// says. <c>RFiDGear.Tests</c> compiles this file too and tests the parts that need no reader.
    /// </summary>
    internal sealed class HilSettings
    {
        /// <summary>The name of the file with the settings, which is not in git.</summary>
        public const string FileName = "hil.settings.json";

        /// <summary>The name of the file with the settings to start from, which is in git.</summary>
        public const string SampleFileName = "hil.settings.sample.json";

        /// <summary>
        /// The variable with which <c>Run-HilTests.ps1 -Interactive</c> tells the tests that the questions are on for this
        /// run. It lives in the process of the runner and dies with it; nobody sets it by hand.
        /// </summary>
        public const string RunnerInteractiveVariable = "RFIDGEAR_HIL_INTERACTIVE";

        private const string ReaderScanName = "readerScan";
        private const string InteractiveName = "interactive";
        private const string SecureKeysName = "secureKeys";

        private static readonly string[] SettingNames = { ReaderScanName, InteractiveName, SecureKeysName };

        private static readonly JsonDocumentOptions ReadOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly Lazy<HilSettings> CurrentSettings = new Lazy<HilSettings>(() => LoadForThisRun(AppContext.BaseDirectory));

        private HilSettings(bool readerScan, bool interactive, bool secureKeys, string? source, string? problem)
        {
            ReaderScan = readerScan;
            Interactive = interactive;
            SecureKeys = secureKeys;
            Source = source;
            Problem = problem;
        }

        /// <summary>Gets the settings of this test run, read once from the folder of the test assembly upwards.</summary>
        public static HilSettings Current => CurrentSettings.Value;

        /// <summary>Gets what the file says about the reader-scan tests.</summary>
        public bool ReaderScan { get; }

        /// <summary>Gets what the file says about the cases that ask for cards to be moved.</summary>
        public bool Interactive { get; }

        /// <summary>
        /// Gets what the file says about the secure-key cases, which change the keys of the test application on the card and
        /// write credentials into the Windows Credential Manager of the user (and remove them again).
        /// </summary>
        public bool SecureKeys { get; }

        /// <summary>Gets the full path of the file, or <c>null</c> when there is none.</summary>
        public string? Source { get; }

        /// <summary>Gets what is wrong with the file, or <c>null</c>. A file with a problem asks for nothing.</summary>
        public string? Problem { get; }

        /// <summary>Gets whether the reader-scan tests were asked for.</summary>
        public bool HardwareRequested => Problem == null && ReaderScan;

        /// <summary>Gets whether the cases that ask for cards to be moved were asked for; they need the reader-scan tests.</summary>
        public bool InteractiveRequested => HardwareRequested && Interactive;

        /// <summary>Gets whether the secure-key cases were asked for. They do not depend on the reader-scan tests.</summary>
        public bool SecureKeysRequested => Problem == null && SecureKeys;

        /// <summary>Finds the file in a folder or in the nearest folder above it.</summary>
        /// <param name="startDirectory">The folder to start in.</param>
        /// <returns>The full path of the file, or <c>null</c>.</returns>
        public static string? Find(string startDirectory)
        {
            for (var directory = new DirectoryInfo(startDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, FileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Reads the settings of this run: the nearest file, and what the runner said with <c>-Interactive</c>.</summary>
        /// <param name="startDirectory">The folder to start looking in.</param>
        /// <returns>The settings.</returns>
        public static HilSettings LoadForThisRun(string startDirectory) =>
            Load(startDirectory, Environment.GetEnvironmentVariable(RunnerInteractiveVariable));

        /// <summary>
        /// Reads the settings from the nearest file; without one, nothing is asked for. The runner can turn the questions on
        /// for one run, but not the reader-scan tests: those have to be on in the file, and a file with a problem still
        /// asks for nothing.
        /// </summary>
        /// <param name="startDirectory">The folder to start looking in.</param>
        /// <param name="interactiveFromRunner"><c>1</c> when the runner was started with <c>-Interactive</c>.</param>
        /// <returns>The settings.</returns>
        public static HilSettings Load(string startDirectory, string? interactiveFromRunner = null)
        {
            var settings = LoadFile(startDirectory);

            return string.Equals(interactiveFromRunner, "1", StringComparison.Ordinal) && settings.Problem == null
                ? new HilSettings(settings.ReaderScan, true, settings.SecureKeys, settings.Source, null)
                : settings;
        }

        private static HilSettings LoadFile(string startDirectory)
        {
            var path = Find(startDirectory);
            if (path == null)
            {
                return new HilSettings(false, false, false, null, null);
            }

            try
            {
                return Parse(File.ReadAllText(path), path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return new HilSettings(false, false, false, path, "the file cannot be read: " + ex.Message);
            }
        }

        /// <summary>
        /// Reads the settings from the text of a file: one JSON object with the boolean settings <c>readerScan</c>,
        /// <c>interactive</c> and <c>secureKeys</c> (names in any case; comments and trailing commas are allowed; a setting
        /// that is left out is off). Anything else is a problem (an unknown name, a value that is not true or false, a
        /// setting that is set twice), and a file with a problem asks for nothing: a typo must not switch a test on.
        /// </summary>
        /// <param name="json">The text of the file.</param>
        /// <param name="source">The full path of the file, for the messages.</param>
        /// <returns>The settings.</returns>
        public static HilSettings Parse(string json, string source)
        {
            try
            {
                using var document = JsonDocument.Parse(json, ReadOptions);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return Invalid(source, "the file must hold one JSON object");
                }

                // The value of each setting that the file names; a setting that is not in it is off.
                var values = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (Array.FindIndex(SettingNames, name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)) < 0)
                    {
                        return Invalid(source, $"unknown setting \"{property.Name}\"; the settings are \"{ReaderScanName}\", \"{InteractiveName}\" and \"{SecureKeysName}\"");
                    }

                    if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                    {
                        return Invalid(source, $"\"{property.Name}\" must be true or false");
                    }

                    // The dictionary ignores the case of a name, so "readerScan" and "ReaderScan" are one setting.
                    if (!values.TryAdd(property.Name, property.Value.GetBoolean()))
                    {
                        return Invalid(source, $"\"{property.Name}\" is set twice");
                    }
                }

                return new HilSettings(
                    values.GetValueOrDefault(ReaderScanName),
                    values.GetValueOrDefault(InteractiveName),
                    values.GetValueOrDefault(SecureKeysName),
                    source,
                    null);
            }
            catch (JsonException ex)
            {
                return Invalid(source, "it is not valid JSON: " + ex.Message);
            }
        }

        /// <summary>Says why a test that needs the settings is skipped, in words that tell what to change.</summary>
        /// <param name="movesCards"><c>true</c> for a case that asks for cards to be moved.</param>
        /// <returns>The reason, with the path of the file to edit.</returns>
        public string SkipReason(bool movesCards)
        {
            if (Source == null)
            {
                return $"Needs a reader. There is no {FileName} in the folder of the tests or above it: copy {SampleFileName} to {FileName} " +
                       "(next to Run-HilTests.bat) and see RFiDGear.HIL.Tests/README.md.";
            }

            if (Problem != null)
            {
                return $"{Source} cannot be used: {Problem}. See RFiDGear.HIL.Tests/README.md.";
            }

            if (!ReaderScan)
            {
                return $"Switched off. Set \"{ReaderScanName}\" to true in {Source} to run it.";
            }

            return movesCards
                ? $"Asks you to move cards. Set \"{InteractiveName}\" to true in {Source}, or start Run-HilTests.bat with -Interactive, to run it."
                : "Needs a reader.";
        }

        /// <summary>
        /// Says why a secure-key case is skipped. These cases change the keys of the test application on the card and put
        /// credentials into the Windows Credential Manager of the user for a moment, so the file has to ask for them.
        /// </summary>
        /// <returns>The reason, with the path of the file to edit.</returns>
        public string SecureKeysSkipReason()
        {
            if (Source == null)
            {
                return $"Switched off. There is no {FileName} in the folder of the tests or above it: copy {SampleFileName} to {FileName} " +
                       $"(next to Run-HilTests.bat), set \"{SecureKeysName}\" to true, and see RFiDGear.HIL.Tests/SECURE-KEYS.md.";
            }

            if (Problem != null)
            {
                return $"{Source} cannot be used: {Problem}. See RFiDGear.HIL.Tests/README.md.";
            }

            return $"Switched off: it changes the keys of the test application on the card and puts credentials into the Windows " +
                   $"Credential Manager for a moment (it removes them again). Set \"{SecureKeysName}\" to true in {Source} to run it.";
        }

        private static HilSettings Invalid(string source, string problem) => new HilSettings(false, false, false, source, problem);
    }
}
