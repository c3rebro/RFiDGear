using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>The JSON-file implementation of <see cref="ISettingsStore"/>.</summary>
    internal sealed class SettingsStore : ISettingsStore
    {
        /// <summary>The version of the file layout this build reads and writes.</summary>
        internal const int CurrentSchema = 1;

        /// <summary>A settings file larger than this is not a settings file.</summary>
        internal const long MaxFileBytes = 1024 * 1024;

        /// <summary>The name of the settings file.</summary>
        internal const string FileName = "settings.json";

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new LenientReportLanguageConverter() },
        };

        // How long a read or a write waits for another store (or program) that is in the middle of its own.
        private static readonly TimeSpan GuardWait = TimeSpan.FromSeconds(10);

        private readonly string _folder;
        private readonly ICardCheckLog _log;
        private readonly object _gate = new object();

        /// <summary>Creates a store.</summary>
        /// <param name="folder">The folder that holds the settings file; created on the first save.</param>
        /// <param name="log">Where status is noted (never a value of a setting).</param>
        public SettingsStore(string folder, ICardCheckLog log)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("A settings folder is needed.", nameof(folder));
            }

            _folder = folder;
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>The clock that names a damaged file that is set aside; replaceable so a test can fix the time.</summary>
        internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

        private string FilePath => Path.Combine(_folder, FileName);

        /// <inheritdoc />
        public SettingsLoad Load()
        {
            lock (_gate)
            {
                using var guard = FileGuard.Acquire(FilePath, GuardWait);

                if (guard == null)
                {
                    _log.Warning("The settings file is in use by another program; defaults are used for now");

                    return new SettingsLoad(new CardCheckSettings(), SettingsLoadStatus.Unreadable);
                }

                return LoadCore();
            }
        }

        /// <inheritdoc />
        public Result Save(CardCheckSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            lock (_gate)
            {
                using var guard = FileGuard.Acquire(FilePath, GuardWait);

                return guard == null ? Busy() : SaveCore(settings);
            }
        }

        /// <inheritdoc />
        public Result Update(Func<CardCheckSettings, Result<CardCheckSettings>> change)
        {
            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            lock (_gate)
            {
                // Read, change and write are one step for every store of this file: another window of the extension or
                // another program cannot read in between and then write over what this call wrote.
                using var guard = FileGuard.Acquire(FilePath, GuardWait);

                if (guard == null)
                {
                    return Busy();
                }

                var load = LoadCore();

                if (load.Status == SettingsLoadStatus.NewerSchema)
                {
                    return Result.Fail(new Failure(FailureKind.InvalidConfiguration, "settings.newerSchema", "The settings were written by a newer version and are not overwritten."));
                }

                if (load.Status == SettingsLoadStatus.Unreadable)
                {
                    return Result.Fail(new Failure(FailureKind.Io, "settings.unreadable", "The settings file cannot be read just now."));
                }

                var next = change(load.Settings);

                if (!next.IsSuccess)
                {
                    return Result.Fail(next.Failure!);
                }

                return ReferenceEquals(next.Value, load.Settings) ? Result.Ok() : SaveCore(next.Value);
            }
        }

        private static Result Busy() =>
            Result.Fail(new Failure(FailureKind.Io, "settings.busy", "The settings are in use by another program just now."));

        private SettingsLoad LoadCore()
        {
            lock (_gate)
            {
                var path = FilePath;

                if (!File.Exists(path))
                {
                    return new SettingsLoad(new CardCheckSettings(), SettingsLoadStatus.NotFound);
                }

                byte[] bytes;

                try
                {
                    if (new FileInfo(path).Length > MaxFileBytes)
                    {
                        return SetAside(path);
                    }

                    bytes = File.ReadAllBytes(path);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    _log.Warning("The settings file could not be read; defaults are used");

                    return new SettingsLoad(new CardCheckSettings(), SettingsLoadStatus.Unreadable);
                }

                return Parse(bytes, path);
            }
        }

        private Result SaveCore(CardCheckSettings settings)
        {
            lock (_gate)
            {
                var path = FilePath;
                string? temp = null;

                try
                {
                    var existing = ExistingSchemaVersion(path);

                    if (existing == ExistingFile.Unreadable)
                    {
                        return Result.Fail(new Failure(FailureKind.Io, "settings.save.unreadable", "The settings file cannot be read, so it is not replaced."));
                    }

                    if (existing == ExistingFile.Newer)
                    {
                        return Result.Fail(new Failure(FailureKind.InvalidConfiguration, "settings.newerSchema", "The settings were written by a newer version and are not overwritten."));
                    }

                    var bytes = JsonSerializer.SerializeToUtf8Bytes(new SettingsFile { SchemaVersion = CurrentSchema, Settings = Normalize(settings) }, Options);

                    // Load sets a bigger file aside as damaged, so a save must never produce one: every successful save
                    // can be loaded again.
                    if (bytes.LongLength > MaxFileBytes)
                    {
                        return Result.Fail(new Failure(FailureKind.InvalidInput, "settings.save.tooLarge", "The settings are too large to be saved."));
                    }

                    Directory.CreateDirectory(_folder);
                    temp = Path.Combine(_folder, FileName + ".tmp-" + Guid.NewGuid().ToString("N"));

                    // Flushed to the disk before the swap: otherwise a power loss right after it could leave the new
                    // name pointing at a file whose content never arrived, with the old, good file already gone.
                    using (var stream = new FileStream(temp, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(flushToDisk: true);
                    }

                    if (File.Exists(path))
                    {
                        File.Replace(temp, path, destinationBackupFileName: null);
                    }
                    else
                    {
                        File.Move(temp, path);
                    }

                    temp = null;

                    return Result.Ok();
                }
                catch (UnauthorizedAccessException)
                {
                    _log.Warning("The settings could not be saved: access denied");

                    return Result.Fail(new Failure(FailureKind.AccessDenied, "settings.save.denied", "The settings cannot be saved: access denied."));
                }
                catch (Exception ex) when (ex is IOException || ex is NotSupportedException || ex is ArgumentException)
                {
                    _log.Warning("The settings could not be saved: an I/O error");

                    return Result.Fail(new Failure(FailureKind.Io, "settings.save.io", "The settings cannot be saved."));
                }
                finally
                {
                    DeleteQuietly(temp);
                }
            }
        }

        private enum ExistingFile
        {
            None,
            Current,
            Newer,
            Unreadable,
        }

        private ExistingFile ExistingSchemaVersion(string path)
        {
            if (!File.Exists(path))
            {
                return ExistingFile.None;
            }

            try
            {
                if (new FileInfo(path).Length > MaxFileBytes)
                {
                    return ExistingFile.Current;
                }

                using var document = JsonDocument.Parse(WithoutByteOrderMark(File.ReadAllBytes(path)), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

                return TryReadSchema(document.RootElement, out var version) && version > CurrentSchema
                    ? ExistingFile.Newer
                    : ExistingFile.Current;
            }
            catch (JsonException)
            {
                // A damaged file may be replaced; Load sets such a file aside.
                return ExistingFile.Current;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return ExistingFile.Unreadable;
            }
        }

        // Tools that people edit the file with (Windows PowerShell, older Notepad, Visual Studio) put a UTF-8 byte order
        // mark in front, which the JSON parser rejects. A valid file with a mark is read; it is not set aside as damaged,
        // and it is not taken for a replaceable one when a newer version wrote it.
        private static ReadOnlyMemory<byte> WithoutByteOrderMark(byte[] bytes) =>
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
                ? bytes.AsMemory(3)
                : bytes.AsMemory();

        private SettingsLoad Parse(byte[] bytes, string path)
        {
            try
            {
                using var document = JsonDocument.Parse(WithoutByteOrderMark(bytes), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var root = document.RootElement;

                if (!TryReadSchema(root, out var version))
                {
                    return SetAside(path);
                }

                if (version > CurrentSchema)
                {
                    _log.Warning("The settings file was written by a newer version; defaults are used and it is left alone");

                    return new SettingsLoad(new CardCheckSettings(), SettingsLoadStatus.NewerSchema);
                }

                if (!root.TryGetProperty("settings", out var settingsElement) || settingsElement.ValueKind == JsonValueKind.Null)
                {
                    return new SettingsLoad(new CardCheckSettings(), SettingsLoadStatus.Loaded);
                }

                var settings = JsonSerializer.Deserialize<CardCheckSettings>(settingsElement.GetRawText(), Options);

                return new SettingsLoad(Normalize(settings ?? new CardCheckSettings()), SettingsLoadStatus.Loaded);
            }
            catch (JsonException)
            {
                return SetAside(path);
            }
        }

        private static bool TryReadSchema(JsonElement root, out int version)
        {
            version = 0;

            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("schemaVersion", out var element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out version)
                && version >= 1;
        }

        private SettingsLoad SetAside(string path)
        {
            var aside = path + ".corrupt-" + UtcNow().ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

            try
            {
                File.Move(path, File.Exists(aside) ? aside + "-" + Guid.NewGuid().ToString("N") : aside);
                _log.Warning("The settings file was damaged and was set aside; defaults are used");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _log.Warning("The settings file was damaged and could not be set aside; defaults are used");
            }

            return new SettingsLoad(new CardCheckSettings(), SettingsLoadStatus.Recovered);
        }

        private static void DeleteQuietly(string? path)
        {
            if (path == null)
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // A leftover temporary file is harmless; the next save uses a new name.
            }
        }

        /// <summary>Makes sure no value is null and removes blanks around names and paths.</summary>
        internal static CardCheckSettings Normalize(CardCheckSettings settings)
        {
            static string Clean(string? value) => value?.Trim() ?? string.Empty;

            var database = settings.Database ?? new DatabaseSettings();
            var report = settings.Report ?? new ReportSettings();
            var templates = (settings.TextTemplates ?? Array.Empty<TextTemplate>())
                .Where(t => t != null)
                .Select(t => new TextTemplate { Name = t.Name ?? string.Empty, Content = t.Content ?? string.Empty })
                .ToList();
            var marker = settings.LegacyImport == null
                ? null
                : new LegacyImportMarker
                {
                    SourceSha256 = Clean(settings.LegacyImport.SourceSha256),
                    Declined = settings.LegacyImport.Declined,
                    ImportedAtUtc = settings.LegacyImport.ImportedAtUtc,
                };

            return new CardCheckSettings
            {
                Database = new DatabaseSettings { Server = Clean(database.Server), Database = Clean(database.Database), Table = Clean(database.Table) },
                Report = new ReportSettings
                {
                    OutputPath = Clean(report.OutputPath),
                    CreateSubdirectory = report.CreateSubdirectory,
                    RemoveTemporaryReports = report.RemoveTemporaryReports,
                    DefaultLanguage = report.DefaultLanguage,
                },
                DefaultProjectPath = Clean(settings.DefaultProjectPath),
                CustomProjectPath = Clean(settings.CustomProjectPath),
                LastProjectFolder = Clean(settings.LastProjectFolder),
                LastReportFolder = Clean(settings.LastReportFolder),
                TextTemplates = templates,
                LegacyImport = marker,
            };
        }

        private sealed class SettingsFile
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("settings")]
            public CardCheckSettings? Settings { get; set; }
        }

        /// <summary>
        /// Writes the language as its name and reads it back; anything that is not the name of a language (an
        /// unknown word, a number, a stray object) is German, so a hand-edited file does not lose all its settings.
        /// </summary>
        private sealed class LenientReportLanguageConverter : JsonConverter<ReportLanguage>
        {
            public override ReportLanguage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();

                    foreach (var name in Enum.GetNames(typeof(ReportLanguage)))
                    {
                        if (string.Equals(name, text?.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            return (ReportLanguage)Enum.Parse(typeof(ReportLanguage), name);
                        }
                    }
                }
                else if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                return ReportLanguage.German;
            }

            public override void Write(Utf8JsonWriter writer, ReportLanguage value, JsonSerializerOptions options) =>
                writer.WriteStringValue(value.ToString());
        }
    }
}
