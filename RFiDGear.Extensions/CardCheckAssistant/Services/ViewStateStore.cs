using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>What the list of orders remembers between two windows: how it was grouped and sorted, and which groups the operator closed.</summary>
    internal sealed class GuidedViewStateData
    {
        /// <summary>Gets the name of the grouping the list had, or <c>null</c> when nothing was remembered.</summary>
        public string? Grouping { get; init; }

        /// <summary>Gets the name of the sort the list had, or <c>null</c> when nothing was remembered.</summary>
        public string? Sort { get; init; }

        /// <summary>Gets the groups the operator closed, each as the grouping and the name of the group (<c>Status:Created</c>).</summary>
        public IReadOnlyList<string> CollapsedGroups { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Where the page keeps what the operator arranged in the list of orders. It is a convenience: it never throws, it
    /// answers "nothing remembered" when it cannot read, and a write that fails is lost without anybody being told.
    /// </summary>
    internal interface IGuidedViewState
    {
        /// <summary>Reads what was remembered.</summary>
        /// <returns>The state; empty when there is none, it is damaged or it cannot be read.</returns>
        GuidedViewStateData Load();

        /// <summary>Remembers the state.</summary>
        /// <param name="state">What to remember.</param>
        void Save(GuidedViewStateData state);
    }

    /// <summary>
    /// The state in a small JSON file of its own beside the assistant's settings. It is not part of the settings: it is not
    /// something to export or to move to another machine, and a change of it must not need a new version of the settings.
    /// </summary>
    internal sealed class FileGuidedViewState : IGuidedViewState
    {
        /// <summary>The name of the file.</summary>
        internal const string FileName = "view-state.json";

        /// <summary>How many closed groups are kept. The page lists those of the grouping in use first, so a long tail of old ones cannot push them out.</summary>
        internal const int MaxCollapsedGroups = 200;

        private const int SchemaVersion = 1;
        private const int MaxFileBytes = 64 * 1024;
        private const int MaxGroupKeyLength = 200;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        private readonly string _path;

        /// <summary>Creates the state in a folder.</summary>
        /// <param name="folder">The folder of the file; it is made when something is saved.</param>
        /// <exception cref="ArgumentException"><paramref name="folder"/> is blank.</exception>
        public FileGuidedViewState(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("The folder is blank.", nameof(folder));
            }

            _path = Path.Combine(folder, FileName);
        }

        /// <summary>Gets the folder of the assistant's own files: the user's application data, then the assistant's folder, the one the settings are in.</summary>
        /// <returns>The full path.</returns>
        /// <exception cref="InvalidOperationException">The user has no application data folder.</exception>
        internal static string DefaultFolder()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(appData)
                ? throw new InvalidOperationException("The user has no application data folder.")
                : Path.Combine(appData, "RFiDGear", "Extensions", "CardCheckAssistant");
        }

        /// <inheritdoc/>
        public GuidedViewStateData Load()
        {
            try
            {
                var file = new FileInfo(_path);
                if (!file.Exists || file.Length > MaxFileBytes)
                {
                    return new GuidedViewStateData();
                }

                var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path), Options);
                if (document == null || document.SchemaVersion != SchemaVersion)
                {
                    return new GuidedViewStateData();
                }

                return new GuidedViewStateData
                {
                    Grouping = document.Grouping,
                    Sort = document.Sort,
                    CollapsedGroups = Sanitize(document.CollapsedGroups),
                };
            }
            catch (Exception ex) when (IsFileProblem(ex))
            {
                return new GuidedViewStateData();
            }
        }

        /// <inheritdoc/>
        public void Save(GuidedViewStateData state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var document = new Document
                {
                    SchemaVersion = SchemaVersion,
                    Grouping = state.Grouping,
                    Sort = state.Sort,
                    CollapsedGroups = Sanitize(state.CollapsedGroups).ToList(),
                };

                // A file that is written whole or not at all: a crash in the middle leaves the old one.
                File.WriteAllText(temporary, JsonSerializer.Serialize(document, Options));
                File.Move(temporary, _path, overwrite: true);
            }
            catch (Exception ex) when (IsFileProblem(ex))
            {
                TryDelete(temporary);
            }
        }

        private static IReadOnlyList<string> Sanitize(IEnumerable<string>? keys) =>
            (keys ?? Enumerable.Empty<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key) && key.Length <= MaxGroupKeyLength)
                .Distinct(StringComparer.Ordinal)
                .Take(MaxCollapsedGroups)
                .ToList();

        private static bool IsFileProblem(Exception ex) =>
            ex is IOException or UnauthorizedAccessException or SecurityException or JsonException or NotSupportedException or ArgumentException;

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (IsFileProblem(ex))
            {
                // A temporary file that stays is harmless.
            }
        }

        private sealed class Document
        {
            public int SchemaVersion { get; set; }

            public string? Grouping { get; set; }

            public string? Sort { get; set; }

            public List<string>? CollapsedGroups { get; set; }
        }
    }
}
