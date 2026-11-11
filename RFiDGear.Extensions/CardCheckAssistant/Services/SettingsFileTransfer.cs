using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>How an export or an import of the settings file ended.</summary>
    internal enum SettingsTransferOutcome
    {
        /// <summary>It worked.</summary>
        Done,

        /// <summary>The file could not be written or read (no access, in use, gone).</summary>
        Failed,

        /// <summary>The file is not a settings file of the assistant.</summary>
        NotSettings,

        /// <summary>The settings file was written by a newer version of the assistant.</summary>
        NewerVersion,

        /// <summary>The file is far larger than any settings file.</summary>
        TooLarge
    }

    /// <summary>What an import found.</summary>
    /// <param name="Outcome">How it ended.</param>
    /// <param name="Data">The settings of the file; <c>null</c> unless the outcome is <see cref="SettingsTransferOutcome.Done"/>.</param>
    internal sealed record SettingsImport(SettingsTransferOutcome Outcome, SettingsData? Data);

    /// <summary>
    /// Writes the settings to a file of the operator's choice and reads them back, so that they can be moved to another
    /// computer or kept before they are changed. It touches the disk, so the settings window calls it on a worker thread.
    /// </summary>
    internal interface ISettingsFileTransfer
    {
        /// <summary>Writes the settings to a file; an existing file is replaced.</summary>
        /// <param name="path">The file.</param>
        /// <param name="data">The settings, as the window shows them.</param>
        /// <returns>How it ended.</returns>
        SettingsTransferOutcome Export(string path, SettingsData data);

        /// <summary>Reads a settings file. Nothing is saved: the window decides what to do with the settings.</summary>
        /// <param name="path">The file.</param>
        /// <returns>What was found.</returns>
        SettingsImport Import(string path);
    }

    /// <summary>
    /// The real transfer: a small JSON file with a format name and a version, so that another kind of file or a file of a
    /// newer version is recognised. There is no password in the settings (the order database uses Windows authentication),
    /// so the file can be shared.
    /// </summary>
    internal sealed class SettingsFileTransfer : ISettingsFileTransfer
    {
        /// <summary>The name that marks the file as a settings file of the assistant.</summary>
        internal const string FormatName = "RFiDGear.CardCheckAssistant.Settings";

        /// <summary>The version of the file layout this build reads and writes.</summary>
        internal const int CurrentVersion = 1;

        /// <summary>The largest file that is read; a settings file is a few kilobytes.</summary>
        internal const long MaxBytes = 2 * 1024 * 1024;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        private sealed record Envelope(string? Format, int Version, SettingsData? Settings);

        /// <inheritdoc/>
        public SettingsTransferOutcome Export(string path, SettingsData data)
        {
            if (string.IsNullOrWhiteSpace(path) || data == null)
            {
                return SettingsTransferOutcome.Failed;
            }

            // A file of its own for this export, beside the target: two exports to the same file, or a file that happens to
            // have the name of a fixed temporary one, cannot meet.
            string? temporary = null;
            try
            {
                var text = JsonSerializer.Serialize(new Envelope(FormatName, CurrentVersion, data), Options);
                var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
                temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");

                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(text);
                }

                File.Move(temporary, path, overwrite: true);

                return SettingsTransferOutcome.Done;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                if (temporary != null)
                {
                    TryDelete(temporary);
                }

                return SettingsTransferOutcome.Failed;
            }
        }

        /// <inheritdoc/>
        public SettingsImport Import(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return new SettingsImport(SettingsTransferOutcome.Failed, null);
                }

                if (new FileInfo(path).Length > MaxBytes)
                {
                    return new SettingsImport(SettingsTransferOutcome.TooLarge, null);
                }

                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;

                // The header first: a file of a newer version may have settings of a shape this build cannot read, and that
                // is a newer file, not a foreign one.
                if (root.ValueKind != JsonValueKind.Object
                    || !TryGet(root, "format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != FormatName
                    || !TryGet(root, "version", out var version) || !version.TryGetInt32(out var versionNumber))
                {
                    return new SettingsImport(SettingsTransferOutcome.NotSettings, null);
                }

                if (versionNumber > CurrentVersion)
                {
                    return new SettingsImport(SettingsTransferOutcome.NewerVersion, null);
                }

                if (!TryGet(root, "settings", out var settings) || settings.ValueKind != JsonValueKind.Object)
                {
                    return new SettingsImport(SettingsTransferOutcome.NotSettings, null);
                }

                var data = settings.Deserialize<SettingsData>(Options);

                return IsUsable(data)
                    ? new SettingsImport(SettingsTransferOutcome.Done, data)
                    : new SettingsImport(SettingsTransferOutcome.NotSettings, null);
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is InvalidOperationException)
            {
                // Text that is no JSON, or JSON of another shape.
                return new SettingsImport(SettingsTransferOutcome.NotSettings, null);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return new SettingsImport(SettingsTransferOutcome.Failed, null);
            }
        }

        private static bool TryGet(JsonElement element, string name, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        // Settings the window can show: a language that exists and no empty entries in the list of texts.
        private static bool IsUsable(SettingsData? data) =>
            data != null
            && Enum.IsDefined(typeof(ReportLanguageChoice), data.ReportLanguage)
            && (data.TextBlocks == null || data.TextBlocks.All(block => block != null));

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // A leftover temporary file is harmless; the next export replaces it.
            }
        }
    }
}
