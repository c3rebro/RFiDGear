using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>The implementation of <see cref="ILegacySettingsImporter"/>.</summary>
    internal sealed class LegacySettingsImporter : ILegacySettingsImporter
    {
        private const string LegacyFolderName = "CardCheckAssistant";
        private const string LegacyFileName = "settings.xml";

        private readonly ISettingsStore _store;
        private readonly string _localAppData;
        private readonly ICardCheckLog _log;

        /// <summary>Creates an importer.</summary>
        /// <param name="store">The settings the values are merged into.</param>
        /// <param name="localAppData">The user's application data folder, where the old assistant's folders are looked for.</param>
        /// <param name="log">Where counts are noted (never a value, a path or a credential).</param>
        public LegacySettingsImporter(ISettingsStore store, string localAppData, ICardCheckLog log)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _localAppData = localAppData ?? throw new ArgumentNullException(nameof(localAppData));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>The clock for the import marker; replaceable so a test can fix the time.</summary>
        internal Func<DateTimeOffset> UtcNow { get; set; } = () => DateTimeOffset.UtcNow;

        /// <inheritdoc />
        public IReadOnlyList<LegacySettingsCandidate> Discover()
        {
            var found = new List<LegacySettingsCandidate>();

            try
            {
                if (string.IsNullOrWhiteSpace(_localAppData) || !Directory.Exists(_localAppData))
                {
                    return found;
                }

                AddIfPresent(found, Path.Combine(_localAppData, LegacyFolderName, LegacyFileName), "Unpackaged");

                var packages = Path.Combine(_localAppData, "Packages");

                if (Directory.Exists(packages))
                {
                    foreach (var package in Directory.EnumerateDirectories(packages, "*CardCheck*"))
                    {
                        AddIfPresent(found, Path.Combine(package, "LocalCache", "Local", LegacyFolderName, LegacyFileName), "Packaged");
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                _log.Warning("Looking for the old settings failed part of the way");
            }

            return found.OrderByDescending(candidate => candidate.LastWriteUtc).ToList();
        }

        /// <inheritdoc />
        public Result<LegacyImportPreview> Preview(string sourcePath)
        {
            var read = ReadSource(sourcePath);

            if (!read.IsSuccess)
            {
                return Result<LegacyImportPreview>.Fail(read.Failure!);
            }

            var (bytes, hash) = read.Value;
            var parsed = ParseAndClear(bytes);

            if (!parsed.IsSuccess)
            {
                return Result<LegacyImportPreview>.Fail(parsed.Failure!);
            }

            var current = _store.Load();

            if (current.Status == SettingsLoadStatus.NewerSchema || current.Status == SettingsLoadStatus.Unreadable)
            {
                return Result<LegacyImportPreview>.Fail(StoreUnavailable(current.Status));
            }

            var proposed = Merge(current.Settings, parsed.Value.Values, marker: null);

            return Result<LegacyImportPreview>.Ok(new LegacyImportPreview(sourcePath, hash, proposed, parsed.Value.Items, parsed.Value.Notes));
        }

        /// <inheritdoc />
        public Result Apply(LegacyImportPreview preview)
        {
            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            var read = ReadSource(preview.SourcePath);

            if (!read.IsSuccess)
            {
                return Result.Fail(read.Failure!);
            }

            var (bytes, hash) = read.Value;

            if (!string.Equals(hash, preview.SourceSha256, StringComparison.Ordinal))
            {
                CryptographicOperations.ZeroMemory(bytes);

                return Result.Fail(new Failure(FailureKind.InvalidInput, "import.source.changed", "The old settings file changed since it was previewed."));
            }

            var parsed = ParseAndClear(bytes);

            if (!parsed.IsSuccess)
            {
                return Result.Fail(parsed.Failure!);
            }

            var current = _store.Load();

            if (current.Status == SettingsLoadStatus.NewerSchema || current.Status == SettingsLoadStatus.Unreadable)
            {
                return Result.Fail(StoreUnavailable(current.Status));
            }

            if (current.Settings.LegacyImport != null)
            {
                return Result.Fail(new Failure(FailureKind.InvalidInput, "import.alreadyDone", "The old settings were already imported or declined."));
            }

            var marker = new LegacyImportMarker { SourceSha256 = hash, ImportedAtUtc = UtcNow() };
            var saved = _store.Save(Merge(current.Settings, parsed.Value.Values, marker));

            if (saved.IsSuccess)
            {
                var imported = parsed.Value.Items.Count(item => item.Disposition == LegacyElementDisposition.Imported);
                _log.Information("Old settings imported: {Imported} of {Elements} elements, {Notes} notes", imported, parsed.Value.Items.Count, parsed.Value.Notes.Count);
            }

            return saved;
        }

        /// <inheritdoc />
        public async Task<ImportOffer> OfferAsync(IImportPrompt prompt, CancellationToken cancellationToken)
        {
            if (prompt == null)
            {
                throw new ArgumentNullException(nameof(prompt));
            }

            try
            {
                var current = _store.Load();

                if (current.Status == SettingsLoadStatus.NewerSchema || current.Status == SettingsLoadStatus.Unreadable)
                {
                    return new ImportOffer(ImportOutcome.Unavailable, Array.Empty<ImportNote>(), StoreUnavailable(current.Status));
                }

                if (current.Settings.LegacyImport != null)
                {
                    return ImportOffer.Of(ImportOutcome.AlreadyHandled);
                }

                var candidates = Discover();

                if (candidates.Count == 0)
                {
                    return ImportOffer.Of(ImportOutcome.NothingToImport);
                }

                var decision = await prompt.ChooseAsync(candidates, cancellationToken).ConfigureAwait(false);

                return await FollowAsync(decision, prompt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ImportOffer.Of(ImportOutcome.NotNow);
            }
            catch (Exception ex)
            {
                _log.Unexpected("import.offer", UnexpectedFault.From(ex));

                return new ImportOffer(ImportOutcome.Failed, Array.Empty<ImportNote>(), new Failure(FailureKind.Unexpected, "import.unexpected", "The import failed."));
            }
        }

        private async Task<ImportOffer> FollowAsync(ImportDecision decision, IImportPrompt prompt, CancellationToken cancellationToken)
        {
            if (decision == null || decision.Choice == ImportChoice.NotNow)
            {
                return ImportOffer.Of(ImportOutcome.NotNow);
            }

            if (decision.Choice == ImportChoice.Never)
            {
                // Written onto the settings as they are now, not as they were before the question was asked.
                var fresh = _store.Load();

                if (fresh.Status == SettingsLoadStatus.NewerSchema || fresh.Status == SettingsLoadStatus.Unreadable)
                {
                    return new ImportOffer(ImportOutcome.Unavailable, Array.Empty<ImportNote>(), StoreUnavailable(fresh.Status));
                }

                var declined = new LegacyImportMarker { SourceSha256 = string.Empty, ImportedAtUtc = UtcNow(), Declined = true };
                var saved = _store.Save(SettingsStore.Normalize(fresh.Settings with { LegacyImport = declined }));

                return saved.IsSuccess
                    ? ImportOffer.Of(ImportOutcome.Declined)
                    : new ImportOffer(ImportOutcome.Failed, Array.Empty<ImportNote>(), saved.Failure);
            }

            var preview = Preview(decision.SourcePath ?? string.Empty);

            if (!preview.IsSuccess)
            {
                return new ImportOffer(ImportOutcome.Failed, Array.Empty<ImportNote>(), preview.Failure);
            }

            if (!await prompt.ConfirmAsync(preview.Value, cancellationToken).ConfigureAwait(false))
            {
                return ImportOffer.Of(ImportOutcome.Cancelled);
            }

            var applied = Apply(preview.Value);

            return applied.IsSuccess
                ? new ImportOffer(ImportOutcome.Imported, preview.Value.Notes)
                : new ImportOffer(ImportOutcome.Failed, Array.Empty<ImportNote>(), applied.Failure);
        }

        // The file holds the credentials of the old assistant; the library's copy of it does not outlive the parse.
        private static Result<LegacyReadResult> ParseAndClear(byte[] bytes)
        {
            try
            {
                return LegacySettingsReader.Read(bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        private static Failure StoreUnavailable(SettingsLoadStatus status) =>
            status == SettingsLoadStatus.NewerSchema
                ? new Failure(FailureKind.InvalidConfiguration, "settings.newerSchema", "The settings were written by a newer version and are not overwritten.")
                : new Failure(FailureKind.Io, "settings.unreadable", "The settings file cannot be read just now.");

        private static void AddIfPresent(List<LegacySettingsCandidate> found, string path, string kind)
        {
            var file = new FileInfo(path);

            if (file.Exists && file.Length <= LegacySettingsReader.MaxFileBytes)
            {
                found.Add(new LegacySettingsCandidate(file.FullName, kind, file.LastWriteTimeUtc, file.Length));
            }
        }

        // Reads the file for reading only, and shares it with the old assistant if that is still running.
        private static Result<(byte[] Bytes, string Sha256)> ReadSource(string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return Result<(byte[], string)>.Fail(new Failure(FailureKind.InvalidInput, "import.source.missing", "No settings file was chosen."));
            }

            try
            {
                using var stream = new FileStream(sourcePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                if (stream.Length > LegacySettingsReader.MaxFileBytes)
                {
                    return Result<(byte[], string)>.Fail(new Failure(FailureKind.Corrupt, "import.source.tooLarge", "The file is too large to be a settings file."));
                }

                var bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);

                return Result<(byte[], string)>.Ok((bytes, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return Result<(byte[], string)>.Fail(new Failure(FailureKind.NotFound, "import.source.missing", "The settings file does not exist."));
            }
            catch (UnauthorizedAccessException)
            {
                return Result<(byte[], string)>.Fail(new Failure(FailureKind.AccessDenied, "import.source.denied", "The settings file cannot be read: access denied."));
            }
            catch (Exception ex) when (ex is IOException || ex is NotSupportedException || ex is ArgumentException)
            {
                return Result<(byte[], string)>.Fail(new Failure(FailureKind.Io, "import.source.unreadable", "The settings file cannot be read."));
            }
        }

        /// <summary>
        /// Fills the settings that are still at their defaults with the imported values; what the operator has already
        /// entered in this version is kept. Text blocks are added unless one with the same name is there.
        /// </summary>
        internal static CardCheckSettings Merge(CardCheckSettings current, LegacyValues values, LegacyImportMarker? marker)
        {
            var defaults = new CardCheckSettings();

            string Fill(string now, string? imported, string standard) =>
                string.Equals(now, standard, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(imported) ? imported!.Trim() : now;

            bool FillBool(bool now, bool? imported) => !now && imported == true ? true : now;

            var templates = current.TextTemplates.ToList();

            foreach (var template in values.TextTemplates)
            {
                if (!templates.Any(existing => string.Equals(existing.Name, template.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    templates.Add(template);
                }
            }

            var lastProjectFolder = SafeDirectory(values.LastProject);

            return SettingsStore.Normalize(current with
            {
                Database = new DatabaseSettings
                {
                    Server = Fill(current.Database.Server, values.Server, defaults.Database.Server),
                    Database = Fill(current.Database.Database, values.Database, defaults.Database.Database),
                    Table = Fill(current.Database.Table, values.Table, defaults.Database.Table),
                },
                Report = new ReportSettings
                {
                    OutputPath = Fill(current.Report.OutputPath, values.OutputPath, defaults.Report.OutputPath),
                    CreateSubdirectory = FillBool(current.Report.CreateSubdirectory, values.CreateSubdirectory),
                    RemoveTemporaryReports = FillBool(current.Report.RemoveTemporaryReports, values.RemoveTemporaryReports),
                    DefaultLanguage = current.Report.DefaultLanguage == defaults.Report.DefaultLanguage && values.Language.HasValue
                        ? values.Language.Value
                        : current.Report.DefaultLanguage,
                },
                DefaultProjectPath = Fill(current.DefaultProjectPath, values.DefaultProject, defaults.DefaultProjectPath),
                CustomProjectPath = Fill(current.CustomProjectPath, values.CustomProject, defaults.CustomProjectPath),
                LastProjectFolder = Fill(current.LastProjectFolder, lastProjectFolder, defaults.LastProjectFolder),
                TextTemplates = templates,
                LegacyImport = marker ?? current.LegacyImport,
            });
        }

        private static string? SafeDirectory(string? path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
