using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>The values of the old settings file that this version can use.</summary>
    internal sealed class LegacyValues
    {
        public bool? UsesSqlServer { get; set; }

        public string? Server { get; set; }

        public string? Database { get; set; }

        public string? Table { get; set; }

        public ReportLanguage? Language { get; set; }

        public bool? RemoveTemporaryReports { get; set; }

        public bool? CreateSubdirectory { get; set; }

        public string? OutputPath { get; set; }

        public string? DefaultProject { get; set; }

        public string? CustomProject { get; set; }

        public string? LastProject { get; set; }

        public List<TextTemplate> TextTemplates { get; } = new List<TextTemplate>();
    }

    /// <summary>What reading one old settings file produced.</summary>
    internal sealed class LegacyReadResult
    {
        public LegacyReadResult(LegacyValues values, IReadOnlyList<LegacyImportItem> items, IReadOnlyList<ImportNote> notes)
        {
            Values = values;
            Items = items;
            Notes = notes;
        }

        public LegacyValues Values { get; }

        public IReadOnlyList<LegacyImportItem> Items { get; }

        public IReadOnlyList<ImportNote> Notes { get; }
    }

    /// <summary>
    /// Reads the settings file of the old assistant (an <c>XmlSerializer</c> document with the root
    /// <c>DefaultSettings</c>). The file is not trusted: a document type declaration is refused, nothing external is
    /// resolved, and the credential elements are skipped while reading, so their values never reach a setting, a preview
    /// or a log. It does not use the old assistant's own reader, which would also load them.
    /// </summary>
    internal static class LegacySettingsReader
    {
        /// <summary>A settings file larger than this is not one.</summary>
        internal const long MaxFileBytes = 1024 * 1024;

        /// <summary>Elements that hold a credential or the port of the connection. Never read.</summary>
        private static readonly string[] CredentialElements = { "SelectedDBUsername", "SelectedDBUserPwd", "SelectedDBServerPort" };

        /// <summary>Settings that do not exist in this version.</summary>
        private static readonly string[] UnusedElements =
        {
            "ManifestVersion", "DefaultTheme", "AutoCheckForUpdates", "DefaultLanguage", "AutoRunProjectOnStart",
            "AutoLoadProjectOnStart", "DefaultRFIDGearExePath", "ReaderVolume",
        };

        /// <summary>The placeholder entry of the old list of text blocks. It is not a text block.</summary>
        private const string PlaceholderTemplateName = "N/A";

        /// <summary>Reads a file that was already checked for its size.</summary>
        /// <param name="bytes">The bytes of the file.</param>
        /// <returns>What was read, or why the bytes are not an old settings file.</returns>
        public static Result<LegacyReadResult> Read(byte[] bytes)
        {
            var values = new LegacyValues();
            var items = new List<LegacyImportItem>();
            var notes = new List<ImportNote>();
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                MaxCharactersInDocument = MaxFileBytes * 4,
            };

            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                using var reader = XmlReader.Create(stream, settings);

                if (!reader.ReadToFollowing("DefaultSettings") || reader.Depth != 0)
                {
                    return NotSettings();
                }

                ReadChildren(reader, values, items, notes);

                // Whatever follows the root element has to be well formed as well: a damaged file is never taken
                // for a good one and marked as imported.
                while (reader.Read())
                {
                }
            }
            catch (Exception ex) when (ex is XmlException || ex is InvalidOperationException)
            {
                return Result<LegacyReadResult>.Fail(new Failure(FailureKind.Corrupt, "import.source.corrupt", "The file is not a readable settings file."));
            }

            Finish(values, items, notes);

            return Result<LegacyReadResult>.Ok(new LegacyReadResult(values, items, notes));
        }

        private static Result<LegacyReadResult> NotSettings() =>
            Result<LegacyReadResult>.Fail(new Failure(FailureKind.InvalidInput, "import.source.notSettings", "The file is not a settings file of the Card Check Assistant."));

        private static void ReadChildren(XmlReader reader, LegacyValues values, List<LegacyImportItem> items, List<ImportNote> notes)
        {
            if (reader.IsEmptyElement)
            {
                return;
            }

            reader.ReadStartElement();

            while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Skip();
                    continue;
                }

                ReadElement(reader, values, items, notes);
            }
        }

        private static void ReadElement(XmlReader reader, LegacyValues values, List<LegacyImportItem> items, List<ImportNote> notes)
        {
            var name = reader.LocalName;

            if (Array.IndexOf(CredentialElements, name) >= 0)
            {
                items.Add(new LegacyImportItem(name, LegacyElementDisposition.NeverRead));
                reader.Skip();
            }
            else if (Array.IndexOf(UnusedElements, name) >= 0)
            {
                items.Add(new LegacyImportItem(name, LegacyElementDisposition.NotUsed));
                reader.Skip();
            }
            else if (name == "CardCheckTextTemplates")
            {
                ReadTemplates(reader, values, items, notes);
            }
            else if (IsKnownValue(name))
            {
                Store(name, ReadText(reader), values, items, notes);
            }
            else
            {
                items.Add(new LegacyImportItem(name, LegacyElementDisposition.Unknown));
                reader.Skip();
            }
        }

        private static bool IsKnownValue(string name)
        {
            switch (name)
            {
                case "CardCheckUseMSSQL":
                case "SelectedDBServerName":
                case "SelectedDBName":
                case "SelectedDBTableName":
                case "DefaultReportLanguage":
                case "RemoveTemporaryReportsIsEnabled":
                case "CreateSubdirectoryIsEnabled":
                case "DefaultProjectOutputPath":
                case "LastUsedDefaultProject":
                case "LastUsedCustomProjectPath":
                case "LastUsedProjectPath":
                    return true;
                default:
                    return false;
            }
        }

        // The text of a simple element. An element that holds other elements is not simple: that is a damaged file.
        private static string ReadText(XmlReader reader)
        {
            if (reader.IsEmptyElement)
            {
                reader.Read();

                return string.Empty;
            }

            return reader.ReadElementContentAsString().Trim();
        }

        private static void Store(string name, string text, LegacyValues values, List<LegacyImportItem> items, List<ImportNote> notes)
        {
            if (text.Length == 0)
            {
                items.Add(new LegacyImportItem(name, LegacyElementDisposition.Empty));

                return;
            }

            var usable = true;

            switch (name)
            {
                case "CardCheckUseMSSQL":
                    usable = TryBool(text, out var useSql);
                    values.UsesSqlServer = usable ? useSql : values.UsesSqlServer;
                    break;
                case "SelectedDBServerName":
                    values.Server = text;
                    break;
                case "SelectedDBName":
                    values.Database = text;
                    break;
                case "SelectedDBTableName":
                    values.Table = text;
                    break;
                case "DefaultReportLanguage":
                    usable = TryLanguage(text, out var language);
                    values.Language = usable ? language : values.Language;
                    break;
                case "RemoveTemporaryReportsIsEnabled":
                    usable = TryBool(text, out var remove);
                    values.RemoveTemporaryReports = usable ? remove : values.RemoveTemporaryReports;
                    break;
                case "CreateSubdirectoryIsEnabled":
                    usable = TryBool(text, out var subdirectory);
                    values.CreateSubdirectory = usable ? subdirectory : values.CreateSubdirectory;
                    break;
                case "DefaultProjectOutputPath":
                    values.OutputPath = text;
                    break;
                case "LastUsedDefaultProject":
                    values.DefaultProject = text;
                    break;
                case "LastUsedCustomProjectPath":
                    values.CustomProject = text;
                    break;
                case "LastUsedProjectPath":
                    values.LastProject = text;
                    break;
                default:
                    break;
            }

            items.Add(new LegacyImportItem(name, usable ? LegacyElementDisposition.Imported : LegacyElementDisposition.Invalid));

            if (!usable)
            {
                notes.Add(new ImportNote("import.value.invalid", name, "A value in the old settings could not be understood and was left out."));
            }
        }

        private static void ReadTemplates(XmlReader reader, LegacyValues values, List<LegacyImportItem> items, List<ImportNote> notes)
        {
            var imported = 0;
            var lostText = false;

            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.ReadStartElement();

                while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "CardCheckTextTemplate")
                    {
                        var (template, lost) = ReadTemplate(reader);
                        lostText |= lost;

                        if (template != null)
                        {
                            values.TextTemplates.Add(template);
                            imported++;
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                reader.ReadEndElement();
            }

            items.Add(new LegacyImportItem("CardCheckTextTemplates", imported > 0 ? LegacyElementDisposition.Imported : LegacyElementDisposition.Empty));

            if (lostText)
            {
                notes.Add(new ImportNote("import.template.dropped", "CardCheckTextTemplates", "A text block without a usable name was left out."));
            }
        }

        // One text block. The placeholder entry "N/A" and an entry without a name are not text blocks: they are left out,
        // silently when they hold no text (the old list always started with such an entry) and with a note when they do.
        private static (TextTemplate? Template, bool LostText) ReadTemplate(XmlReader reader)
        {
            var name = string.Empty;
            var content = string.Empty;

            if (reader.IsEmptyElement)
            {
                reader.Read();

                return (null, false);
            }

            reader.ReadStartElement();

            while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "TemplateTextName")
                {
                    name = ReadText(reader);
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "TemplateTextContent")
                {
                    content = reader.IsEmptyElement ? ReadText(reader) : reader.ReadElementContentAsString();
                }
                else
                {
                    reader.Skip();
                }
            }

            reader.ReadEndElement();

            if (name.Length == 0 || string.Equals(name, PlaceholderTemplateName, StringComparison.OrdinalIgnoreCase))
            {
                return (null, content.Trim().Length > 0);
            }

            return (new TextTemplate { Name = name, Content = content }, false);
        }

        private static bool TryBool(string text, out bool value)
        {
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "1")
            {
                value = true;

                return true;
            }

            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || text == "0")
            {
                value = false;

                return true;
            }

            value = false;

            return false;
        }

        // The old setting held the word in the language of the project files: "deutsch" or "englisch".
        private static bool TryLanguage(string text, out ReportLanguage language)
        {
            switch (text.ToLowerInvariant())
            {
                case "deutsch":
                case "german":
                case "de":
                    language = ReportLanguage.German;

                    return true;
                case "englisch":
                case "english":
                case "en":
                    language = ReportLanguage.English;

                    return true;
                default:
                    language = ReportLanguage.German;

                    return false;
            }
        }

        // An SQLite configuration kept something else in the server fields; this version has no SQLite, so they are
        // not taken over and the operator is told.
        private static void Finish(LegacyValues values, List<LegacyImportItem> items, List<ImportNote> notes)
        {
            if (values.UsesSqlServer != false)
            {
                return;
            }

            values.Server = null;
            values.Database = null;
            values.Table = null;

            for (var i = 0; i < items.Count; i++)
            {
                var name = items[i].Element;

                if (name == "SelectedDBServerName" || name == "SelectedDBName" || name == "SelectedDBTableName")
                {
                    items[i] = new LegacyImportItem(name, LegacyElementDisposition.Skipped);
                }
            }

            notes.Add(new ImportNote(
                "import.sqlite.unsupported",
                "CardCheckUseMSSQL",
                "The old settings used SQLite, which this version does not support. Enter the SQL Server settings."));
        }
    }
}
