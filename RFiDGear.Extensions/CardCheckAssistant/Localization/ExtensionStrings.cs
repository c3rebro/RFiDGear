using System;
using System.Collections.Generic;

namespace RFiDGear.Extensions.CardCheckAssistant.Localization
{
    /// <summary>
    /// The page's texts in English and German. The extension owns them (it must not reach into the application's
    /// resources); English is the fallback for any other language and for a missing German entry.
    /// </summary>
    internal static class ExtensionStrings
    {
        /// <summary>The keys the page and its view model use, so a typo is a compile error.</summary>
        internal static class Keys
        {
            public const string WindowHint = "WindowHint";
            public const string ReaderLabel = "ReaderLabel";
            public const string ReaderConnected = "ReaderConnected";
            public const string ReaderNotConnected = "ReaderNotConnected";
            public const string ReaderUnknown = "ReaderUnknown";
            public const string OpenProjectLabel = "OpenProjectLabel";
            public const string NoOpenProject = "NoOpenProject";
            public const string OpenProjectFormat = "OpenProjectFormat";
            public const string ProjectLabel = "ProjectLabel";
            public const string Browse = "Browse";
            public const string PickerTitle = "PickerTitle";
            public const string PickerFilter = "PickerFilter";
            public const string JobNumberLabel = "JobNumberLabel";
            public const string ChipNumberLabel = "ChipNumberLabel";
            public const string ReportTargetLabel = "ReportTargetLabel";
            public const string RunButton = "RunButton";
            public const string StatusChecking = "StatusChecking";
            public const string StatusLoading = "StatusLoading";
            public const string StatusRunning = "StatusRunning";
            public const string StatusRunningElsewhere = "StatusRunningElsewhere";
            public const string ResultHeader = "ResultHeader";
            public const string OutcomeSuccess = "OutcomeSuccess";
            public const string OutcomeIntentionalFailures = "OutcomeIntentionalFailures";
            public const string OutcomeFailure = "OutcomeFailure";
            public const string ReasonFormat = "ReasonFormat";
            public const string ReasonCancelled = "ReasonCancelled";
            public const string ReasonTimedOut = "ReasonTimedOut";
            public const string ReasonFaulted = "ReasonFaulted";
            public const string SummaryFormat = "SummaryFormat";
            public const string ColumnPosition = "ColumnPosition";
            public const string ColumnTaskIndex = "ColumnTaskIndex";
            public const string ColumnType = "ColumnType";
            public const string ColumnResult = "ColumnResult";
            public const string ColumnErrorCode = "ColumnErrorCode";
            public const string RowOk = "RowOk";
            public const string RowFailed = "RowFailed";
            public const string RowUnknown = "RowUnknown";
            public const string RowNotReached = "RowNotReached";
            public const string RowConditionNotMet = "RowConditionNotMet";
            public const string RowNotExecuted = "RowNotExecuted";
            public const string RowTaskUnavailable = "RowTaskUnavailable";
            public const string ProjectRequired = "ProjectRequired";
            public const string ProjectNotAbsolute = "ProjectNotAbsolute";
            public const string ProjectNotFound = "ProjectNotFound";
            public const string LoadPathNotFound = "LoadPathNotFound";
            public const string LoadUnsupportedFormat = "LoadUnsupportedFormat";
            public const string LoadBusy = "LoadBusy";
            public const string LoadUnknown = "LoadUnknown";
            public const string ReportNotAbsolute = "ReportNotAbsolute";
            public const string ReportFolderMissing = "ReportFolderMissing";
            public const string ReportIsFolder = "ReportIsFolder";
            public const string ReportUnusable = "ReportUnusable";
            public const string RunBusy = "RunBusy";
            public const string RunUnexpectedFormat = "RunUnexpectedFormat";
            public const string CloseBlocked = "CloseBlocked";
            public const string ComponentMissing = "ComponentMissing";
            public const string ImportTitle = "ImportTitle";
            public const string ImportFound = "ImportFound";
            public const string ImportMore = "ImportMore";
            public const string ImportButton = "ImportButton";
            public const string ImportNotNow = "ImportNotNow";
            public const string ImportNever = "ImportNever";
            public const string ImportConfirm = "ImportConfirm";
            public const string ImportSqliteNote = "ImportSqliteNote";
            public const string SettingsDamaged = "SettingsDamaged";
            public const string SettingsNewerVersion = "SettingsNewerVersion";
            public const string SettingsUnreadable = "SettingsUnreadable";
            public const string SettingsUnavailable = "SettingsUnavailable";
            public const string SaveNewerVersion = "SaveNewerVersion";
            public const string SaveUnreadable = "SaveUnreadable";
            public const string SaveBusy = "SaveBusy";
            public const string SaveTooLarge = "SaveTooLarge";
            public const string SaveFailed = "SaveFailed";
            public const string FixEntries = "FixEntries";
            public const string FolderNotAbsolute = "FolderNotAbsolute";
            public const string FolderUnusable = "FolderUnusable";
            public const string FolderIsFile = "FolderIsFile";
            public const string FolderNotFound = "FolderNotFound";
            public const string DatabaseIncomplete = "DatabaseIncomplete";
            public const string TextBlockNameBlank = "TextBlockNameBlank";
            public const string TextBlockNameDuplicate = "TextBlockNameDuplicate";
            public const string TextBlockNewName = "TextBlockNewName";
            public const string PickFolderTitle = "PickFolderTitle";
            public const string DiscardTitle = "DiscardTitle";
            public const string DiscardMessage = "DiscardMessage";
            public const string TestTesting = "TestTesting";
            public const string TestSucceeded = "TestSucceeded";
            public const string TestNotConfigured = "TestNotConfigured";
            public const string TestTableInvalid = "TestTableInvalid";
            public const string TestConnectionInvalid = "TestConnectionInvalid";
            public const string TestUnreachable = "TestUnreachable";
            public const string TestAccessDenied = "TestAccessDenied";
            public const string TestSchemaMismatch = "TestSchemaMismatch";
            public const string TestTimeout = "TestTimeout";
            public const string TestDriverMissing = "TestDriverMissing";
            public const string TestFailed = "TestFailed";
            public const string SettingsButton = "SettingsButton";
            public const string SettingsTitle = "SettingsTitle";
            public const string SectionReports = "SectionReports";
            public const string SectionProjects = "SectionProjects";
            public const string SectionDatabase = "SectionDatabase";
            public const string SectionTextBlocks = "SectionTextBlocks";
            public const string OutputFolderLabel = "OutputFolderLabel";
            public const string OneFolderPerJobLabel = "OneFolderPerJobLabel";
            public const string RemoveTemporaryReportsLabel = "RemoveTemporaryReportsLabel";
            public const string ReportLanguageLabel = "ReportLanguageLabel";
            public const string LanguageGerman = "LanguageGerman";
            public const string LanguageEnglish = "LanguageEnglish";
            public const string DefaultProjectLabel = "DefaultProjectLabel";
            public const string DefaultProjectHint = "DefaultProjectHint";
            public const string MemoryProjectLabel = "MemoryProjectLabel";
            public const string DatabaseHint = "DatabaseHint";
            public const string DatabaseServerLabel = "DatabaseServerLabel";
            public const string DatabaseNameLabel = "DatabaseNameLabel";
            public const string DatabaseTableLabel = "DatabaseTableLabel";
            public const string TestConnectionButton = "TestConnectionButton";
            public const string TextBlocksHint = "TextBlocksHint";
            public const string TextBlockNameLabel = "TextBlockNameLabel";
            public const string TextBlockContentLabel = "TextBlockContentLabel";
            public const string AddTextBlockButton = "AddTextBlockButton";
            public const string RemoveTextBlockButton = "RemoveTextBlockButton";
            public const string SaveButton = "SaveButton";
            public const string CancelButton = "CancelButton";
        }

        private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Keys.WindowHint] = "Runs the tasks of a project once, on the card that is on the reader.",
            [Keys.ReaderLabel] = "Reader:",
            [Keys.ReaderConnected] = "connected",
            [Keys.ReaderNotConnected] = "not connected",
            [Keys.ReaderUnknown] = "unknown",
            [Keys.OpenProjectLabel] = "Open in RFiDGear:",
            [Keys.NoOpenProject] = "no project",
            [Keys.OpenProjectFormat] = "{0} ({1} tasks)",
            [Keys.ProjectLabel] = "Project file (.rfPrj)",
            [Keys.Browse] = "Browse...",
            [Keys.PickerTitle] = "Select the project file",
            [Keys.PickerFilter] = "RFiDGear projects (*.rfPrj;*.xml)|*.rfPrj;*.xml|All files (*.*)|*.*",
            [Keys.JobNumberLabel] = "Job number",
            [Keys.ChipNumberLabel] = "Chip number",
            [Keys.ReportTargetLabel] = "Report file (optional, full path)",
            [Keys.RunButton] = "Run once",
            [Keys.StatusChecking] = "Checking the entries...",
            [Keys.StatusLoading] = "Loading the project...",
            [Keys.StatusRunning] = "Running the tasks...",
            [Keys.StatusRunningElsewhere] = "RFiDGear is running tasks. Wait until it has finished.",
            [Keys.ResultHeader] = "Result",
            [Keys.OutcomeSuccess] = "Success",
            [Keys.OutcomeIntentionalFailures] = "Completed with intentional failures",
            [Keys.OutcomeFailure] = "Failed",
            [Keys.ReasonFormat] = "Reason: {0}",
            [Keys.ReasonCancelled] = "the run was cancelled",
            [Keys.ReasonTimedOut] = "the run timed out",
            [Keys.ReasonFaulted] = "the run stopped because of an error (see the application log)",
            [Keys.SummaryFormat] = "{0} of {1} tasks failed",
            [Keys.ColumnPosition] = "#",
            [Keys.ColumnTaskIndex] = "Task index",
            [Keys.ColumnType] = "Type",
            [Keys.ColumnResult] = "Result",
            [Keys.ColumnErrorCode] = "Error code",
            [Keys.RowOk] = "OK",
            [Keys.RowFailed] = "Failed",
            [Keys.RowUnknown] = "-",
            [Keys.RowNotReached] = "Not reached",
            [Keys.RowConditionNotMet] = "Skipped: condition not met",
            [Keys.RowNotExecuted] = "Not executed",
            [Keys.RowTaskUnavailable] = "Task unavailable",
            [Keys.ProjectRequired] = "Select a project file.",
            [Keys.ProjectNotAbsolute] = "Enter the full path of the project file.",
            [Keys.ProjectNotFound] = "The project file does not exist.",
            [Keys.LoadPathNotFound] = "RFiDGear could not find the project file.",
            [Keys.LoadUnsupportedFormat] = "RFiDGear cannot read this file as a project.",
            [Keys.LoadBusy] = "RFiDGear is busy. Wait until the current operation has finished and try again.",
            [Keys.LoadUnknown] = "The project could not be loaded (see the application log).",
            [Keys.ReportNotAbsolute] = "The report file must be a full path, for example C:\\Reports\\report.pdf.",
            [Keys.ReportFolderMissing] = "The folder of the report file does not exist.",
            [Keys.ReportIsFolder] = "The report target must be a file, not a folder.",
            [Keys.ReportUnusable] = "This path cannot be used as a report file.",
            [Keys.RunBusy] = "RFiDGear is busy. Wait until the current operation has finished and try again.",
            [Keys.RunUnexpectedFormat] = "The run could not be completed ({0}). See the application log.",
            [Keys.CloseBlocked] = "A run is in progress. Wait until it has finished before closing this window.",
            [Keys.ComponentMissing] = "The Card Check Assistant cannot start. The file {0} was not found next to {1}, or it does not match this version. Copy both files to the Extensions folder of RFiDGear (%ProgramData%\\RFiDGear\\Extensions) and restart RFiDGear.",
            [Keys.ImportTitle] = "Settings of the old Card Check Assistant",
            [Keys.ImportFound] = "Settings of the old Card Check Assistant were found:\n{0}\n(last changed {1})\n\nDo you want to take them over into this version? The old files are only read, never changed.",
            [Keys.ImportMore] = "{0} more file(s) were found; the newest is offered.",
            [Keys.ImportButton] = "Import",
            [Keys.ImportNotNow] = "Not now",
            [Keys.ImportNever] = "Never ask again",
            [Keys.ImportConfirm] = "{0} setting(s) of the old assistant can be taken over; {1} cannot (they are empty, or no longer used). What you have already entered here is kept, so fewer may actually change. The user name, password and port of the old assistant are never read.\n\nContinue?",
            [Keys.ImportSqliteNote] = "The old assistant used SQLite. This version does not, so its database settings are not taken over.\n",
            [Keys.SettingsDamaged] = "The settings file was damaged. It was kept under another name and the default values are shown.",
            [Keys.SettingsNewerVersion] = "The settings were written by a newer version of this extension. The default values are shown and nothing can be saved.",
            [Keys.SettingsUnreadable] = "The settings file cannot be read just now; another program may be using it. Close this window and try again in a moment.",
            [Keys.SettingsUnavailable] = "There is no place to keep settings (the user account has no folder for application data). Nothing can be saved.",
            [Keys.SaveNewerVersion] = "Not saved: the settings file was written by a newer version and is not overwritten.",
            [Keys.SaveUnreadable] = "Not saved: the settings file cannot be read just now. Try again in a moment.",
            [Keys.SaveBusy] = "Not saved: another window or program is using the settings file. Try again in a moment.",
            [Keys.SaveTooLarge] = "Not saved: the settings are too large. Shorten the text blocks.",
            [Keys.SaveFailed] = "Not saved: the settings file could not be written (see the application log).",
            [Keys.FixEntries] = "Correct the marked entries first.",
            [Keys.FolderNotAbsolute] = "Enter the full path of the folder.",
            [Keys.FolderUnusable] = "This path cannot be used for reports. Characters such as ? * < > | and quotation marks are not allowed in it.",
            [Keys.FolderIsFile] = "This path is a file, not a folder.",
            [Keys.FolderNotFound] = "This folder does not exist. It must exist when a report is written.",
            [Keys.DatabaseIncomplete] = "Enter server, database and table, or leave server and database empty.",
            [Keys.TextBlockNameBlank] = "Enter a name.",
            [Keys.TextBlockNameDuplicate] = "Another text block has this name.",
            [Keys.TextBlockNewName] = "New text block",
            [Keys.PickFolderTitle] = "Select the folder for the reports",
            [Keys.DiscardTitle] = "Unsaved changes",
            [Keys.DiscardMessage] = "You have changed settings that are not saved. Discard the changes?",
            [Keys.TestTesting] = "Testing the connection...",
            [Keys.TestSucceeded] = "Connected. The table has the columns the assistant uses.",
            [Keys.TestNotConfigured] = "Enter the server and the database first.",
            [Keys.TestTableInvalid] = "The table name is not valid. Use a name like T_Orders or dbo.T_Orders.",
            [Keys.TestConnectionInvalid] = "The server or database name cannot be used as entered.",
            [Keys.TestUnreachable] = "The server cannot be reached. Check the server name and the network.",
            [Keys.TestAccessDenied] = "The server refused your Windows account.",
            [Keys.TestSchemaMismatch] = "The server answered, but the table or one of its columns was not found.",
            [Keys.TestTimeout] = "The server did not answer in time.",
            [Keys.TestDriverMissing] = "The database driver is missing. Copy the files that came with the extension next to it and restart RFiDGear.",
            [Keys.TestFailed] = "The connection could not be tested (see the application log).",
            [Keys.SettingsButton] = "Settings...",
            [Keys.SettingsTitle] = "Card Check Assistant settings",
            [Keys.SectionReports] = "Reports",
            [Keys.SectionProjects] = "Projects",
            [Keys.SectionDatabase] = "Order database",
            [Keys.SectionTextBlocks] = "Text blocks",
            [Keys.OutputFolderLabel] = "Folder for the reports",
            [Keys.OneFolderPerJobLabel] = "Make a folder for each job (named after the job number)",
            [Keys.RemoveTemporaryReportsLabel] = "Remove the working reports when a check is finished",
            [Keys.ReportLanguageLabel] = "Language of the reports",
            [Keys.LanguageGerman] = "German",
            [Keys.LanguageEnglish] = "English",
            [Keys.DefaultProjectLabel] = "Default project (.rfPrj)",
            [Keys.DefaultProjectHint] = "Next to name.rfPrj there may be the files name.deutsch.rfPrj and name.englisch.rfPrj. The one for the language of the report is used if it exists.",
            [Keys.MemoryProjectLabel] = "Project that reads the memory of the card (.rfPrj)",
            [Keys.DatabaseHint] = "The Windows account of the person who runs the check is used to log in.",
            [Keys.DatabaseServerLabel] = "Server (host or host\\instance)",
            [Keys.DatabaseNameLabel] = "Database",
            [Keys.DatabaseTableLabel] = "Table (with or without schema)",
            [Keys.TestConnectionButton] = "Test connection",
            [Keys.TextBlocksHint] = "Text blocks can be inserted into the notes of a report.",
            [Keys.TextBlockNameLabel] = "Name of the text block",
            [Keys.TextBlockContentLabel] = "Text to insert",
            [Keys.AddTextBlockButton] = "Add",
            [Keys.RemoveTextBlockButton] = "Remove",
            [Keys.SaveButton] = "Save",
            [Keys.CancelButton] = "Cancel",
        };

        private static readonly IReadOnlyDictionary<string, string> German = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Keys.WindowHint] = "Führt die Aufgaben eines Projekts einmal auf der Karte aus, die auf dem Leser liegt.",
            [Keys.ReaderLabel] = "Leser:",
            [Keys.ReaderConnected] = "verbunden",
            [Keys.ReaderNotConnected] = "nicht verbunden",
            [Keys.ReaderUnknown] = "unbekannt",
            [Keys.OpenProjectLabel] = "In RFiDGear geöffnet:",
            [Keys.NoOpenProject] = "kein Projekt",
            [Keys.OpenProjectFormat] = "{0} ({1} Aufgaben)",
            [Keys.ProjectLabel] = "Projektdatei (.rfPrj)",
            [Keys.Browse] = "Durchsuchen...",
            [Keys.PickerTitle] = "Projektdatei auswählen",
            [Keys.PickerFilter] = "RFiDGear-Projekte (*.rfPrj;*.xml)|*.rfPrj;*.xml|Alle Dateien (*.*)|*.*",
            [Keys.JobNumberLabel] = "Auftragsnummer",
            [Keys.ChipNumberLabel] = "Chipnummer",
            [Keys.ReportTargetLabel] = "Berichtsdatei (optional, vollständiger Pfad)",
            [Keys.RunButton] = "Einmal ausführen",
            [Keys.StatusChecking] = "Die Eingaben werden geprüft...",
            [Keys.StatusLoading] = "Das Projekt wird geladen...",
            [Keys.StatusRunning] = "Die Aufgaben werden ausgeführt...",
            [Keys.StatusRunningElsewhere] = "RFiDGear führt gerade Aufgaben aus. Bitte warten Sie, bis es fertig ist.",
            [Keys.ResultHeader] = "Ergebnis",
            [Keys.OutcomeSuccess] = "Erfolgreich",
            [Keys.OutcomeIntentionalFailures] = "Abgeschlossen mit beabsichtigten Fehlern",
            [Keys.OutcomeFailure] = "Fehlgeschlagen",
            [Keys.ReasonFormat] = "Grund: {0}",
            [Keys.ReasonCancelled] = "der Lauf wurde abgebrochen",
            [Keys.ReasonTimedOut] = "der Lauf hat das Zeitlimit überschritten",
            [Keys.ReasonFaulted] = "der Lauf wurde wegen eines Fehlers beendet (siehe Anwendungsprotokoll)",
            [Keys.SummaryFormat] = "{0} von {1} Aufgaben sind fehlgeschlagen",
            [Keys.ColumnPosition] = "#",
            [Keys.ColumnTaskIndex] = "Aufgabenindex",
            [Keys.ColumnType] = "Typ",
            [Keys.ColumnResult] = "Ergebnis",
            [Keys.ColumnErrorCode] = "Fehlercode",
            [Keys.RowOk] = "OK",
            [Keys.RowFailed] = "Fehlgeschlagen",
            [Keys.RowUnknown] = "-",
            [Keys.RowNotReached] = "Nicht erreicht",
            [Keys.RowConditionNotMet] = "Übersprungen: Bedingung nicht erfüllt",
            [Keys.RowNotExecuted] = "Nicht ausgeführt",
            [Keys.RowTaskUnavailable] = "Aufgabe nicht verfügbar",
            [Keys.ProjectRequired] = "Wählen Sie eine Projektdatei aus.",
            [Keys.ProjectNotAbsolute] = "Geben Sie den vollständigen Pfad der Projektdatei ein.",
            [Keys.ProjectNotFound] = "Die Projektdatei existiert nicht.",
            [Keys.LoadPathNotFound] = "RFiDGear hat die Projektdatei nicht gefunden.",
            [Keys.LoadUnsupportedFormat] = "RFiDGear kann diese Datei nicht als Projekt lesen.",
            [Keys.LoadBusy] = "RFiDGear ist beschäftigt. Warten Sie, bis der laufende Vorgang beendet ist, und versuchen Sie es erneut.",
            [Keys.LoadUnknown] = "Das Projekt konnte nicht geladen werden (siehe Anwendungsprotokoll).",
            [Keys.ReportNotAbsolute] = "Die Berichtsdatei muss ein vollständiger Pfad sein, zum Beispiel C:\\Berichte\\bericht.pdf.",
            [Keys.ReportFolderMissing] = "Der Ordner der Berichtsdatei existiert nicht.",
            [Keys.ReportIsFolder] = "Das Berichtsziel muss eine Datei sein, kein Ordner.",
            [Keys.ReportUnusable] = "Dieser Pfad kann nicht als Berichtsdatei verwendet werden.",
            [Keys.RunBusy] = "RFiDGear ist beschäftigt. Warten Sie, bis der laufende Vorgang beendet ist, und versuchen Sie es erneut.",
            [Keys.RunUnexpectedFormat] = "Der Lauf konnte nicht abgeschlossen werden ({0}). Siehe Anwendungsprotokoll.",
            [Keys.CloseBlocked] = "Ein Lauf ist in Arbeit. Warten Sie, bis er beendet ist, bevor Sie dieses Fenster schließen.",
            [Keys.ComponentMissing] = "Der Card Check Assistant kann nicht starten. Die Datei {0} fehlt neben {1} oder passt nicht zu dieser Version. Kopieren Sie beide Dateien in den Ordner Extensions von RFiDGear (%ProgramData%\\RFiDGear\\Extensions) und starten Sie RFiDGear neu.",
            [Keys.ImportTitle] = "Einstellungen des alten Card Check Assistant",
            [Keys.ImportFound] = "Einstellungen des alten Card Check Assistant wurden gefunden:\n{0}\n(zuletzt geändert {1})\n\nMöchten Sie sie in diese Version übernehmen? Die alten Dateien werden nur gelesen, nie verändert.",
            [Keys.ImportMore] = "Es wurden {0} weitere Datei(en) gefunden; angeboten wird die neueste.",
            [Keys.ImportButton] = "Übernehmen",
            [Keys.ImportNotNow] = "Jetzt nicht",
            [Keys.ImportNever] = "Nie wieder fragen",
            [Keys.ImportConfirm] = "{0} Einstellung(en) des alten Assistenten können übernommen werden, {1} nicht (sie sind leer oder werden nicht mehr verwendet). Was Sie hier bereits eingegeben haben, bleibt erhalten; es können also weniger tatsächlich geändert werden. Benutzername, Passwort und Port des alten Assistenten werden nie gelesen.\n\nFortfahren?",
            [Keys.ImportSqliteNote] = "Der alte Assistent hat SQLite verwendet. Diese Version nicht, daher werden seine Datenbankeinstellungen nicht übernommen.\n",
            [Keys.SettingsDamaged] = "Die Einstellungsdatei war beschädigt. Sie wurde unter anderem Namen aufbewahrt, angezeigt werden die Standardwerte.",
            [Keys.SettingsNewerVersion] = "Die Einstellungen wurden von einer neueren Version dieser Erweiterung geschrieben. Angezeigt werden die Standardwerte, es kann nichts gespeichert werden.",
            [Keys.SettingsUnreadable] = "Die Einstellungsdatei kann gerade nicht gelesen werden; möglicherweise verwendet ein anderes Programm sie. Schließen Sie dieses Fenster und versuchen Sie es gleich noch einmal.",
            [Keys.SettingsUnavailable] = "Es gibt keinen Ort für Einstellungen (das Benutzerkonto hat keinen Ordner für Anwendungsdaten). Es kann nichts gespeichert werden.",
            [Keys.SaveNewerVersion] = "Nicht gespeichert: Die Einstellungsdatei stammt von einer neueren Version und wird nicht überschrieben.",
            [Keys.SaveUnreadable] = "Nicht gespeichert: Die Einstellungsdatei kann gerade nicht gelesen werden. Versuchen Sie es gleich noch einmal.",
            [Keys.SaveBusy] = "Nicht gespeichert: Ein anderes Fenster oder Programm verwendet die Einstellungsdatei. Versuchen Sie es gleich noch einmal.",
            [Keys.SaveTooLarge] = "Nicht gespeichert: Die Einstellungen sind zu groß. Kürzen Sie die Textbausteine.",
            [Keys.SaveFailed] = "Nicht gespeichert: Die Einstellungsdatei konnte nicht geschrieben werden (siehe Anwendungsprotokoll).",
            [Keys.FixEntries] = "Korrigieren Sie zuerst die markierten Eingaben.",
            [Keys.FolderNotAbsolute] = "Geben Sie den vollständigen Pfad des Ordners ein.",
            [Keys.FolderUnusable] = "Dieser Pfad kann für Berichte nicht verwendet werden. Zeichen wie ? * < > | und Anführungszeichen sind darin nicht erlaubt.",
            [Keys.FolderIsFile] = "Dieser Pfad ist eine Datei, kein Ordner.",
            [Keys.FolderNotFound] = "Dieser Ordner existiert nicht. Er muss existieren, wenn ein Bericht geschrieben wird.",
            [Keys.DatabaseIncomplete] = "Geben Sie Server, Datenbank und Tabelle an, oder lassen Sie Server und Datenbank leer.",
            [Keys.TextBlockNameBlank] = "Geben Sie einen Namen ein.",
            [Keys.TextBlockNameDuplicate] = "Ein anderer Textbaustein hat diesen Namen.",
            [Keys.TextBlockNewName] = "Neuer Textbaustein",
            [Keys.PickFolderTitle] = "Ordner für die Berichte auswählen",
            [Keys.DiscardTitle] = "Nicht gespeicherte Änderungen",
            [Keys.DiscardMessage] = "Sie haben Einstellungen geändert, die nicht gespeichert sind. Änderungen verwerfen?",
            [Keys.TestTesting] = "Die Verbindung wird geprüft...",
            [Keys.TestSucceeded] = "Verbunden. Die Tabelle hat die Spalten, die der Assistent verwendet.",
            [Keys.TestNotConfigured] = "Geben Sie zuerst Server und Datenbank ein.",
            [Keys.TestTableInvalid] = "Der Tabellenname ist ungültig. Verwenden Sie einen Namen wie T_Orders oder dbo.T_Orders.",
            [Keys.TestConnectionInvalid] = "Der Server- oder Datenbankname kann so nicht verwendet werden.",
            [Keys.TestUnreachable] = "Der Server ist nicht erreichbar. Prüfen Sie den Servernamen und das Netzwerk.",
            [Keys.TestAccessDenied] = "Der Server hat Ihr Windows-Konto abgelehnt.",
            [Keys.TestSchemaMismatch] = "Der Server hat geantwortet, aber die Tabelle oder eine ihrer Spalten wurde nicht gefunden.",
            [Keys.TestTimeout] = "Der Server hat nicht rechtzeitig geantwortet.",
            [Keys.TestDriverMissing] = "Der Datenbanktreiber fehlt. Kopieren Sie die mit der Erweiterung gelieferten Dateien daneben und starten Sie RFiDGear neu.",
            [Keys.TestFailed] = "Die Verbindung konnte nicht geprüft werden (siehe Anwendungsprotokoll).",
            [Keys.SettingsButton] = "Einstellungen...",
            [Keys.SettingsTitle] = "Einstellungen des Card Check Assistant",
            [Keys.SectionReports] = "Berichte",
            [Keys.SectionProjects] = "Projekte",
            [Keys.SectionDatabase] = "Auftragsdatenbank",
            [Keys.SectionTextBlocks] = "Textbausteine",
            [Keys.OutputFolderLabel] = "Ordner für die Berichte",
            [Keys.OneFolderPerJobLabel] = "Für jeden Auftrag einen Ordner anlegen (benannt nach der Auftragsnummer)",
            [Keys.RemoveTemporaryReportsLabel] = "Arbeitsberichte entfernen, wenn eine Prüfung abgeschlossen ist",
            [Keys.ReportLanguageLabel] = "Sprache der Berichte",
            [Keys.LanguageGerman] = "Deutsch",
            [Keys.LanguageEnglish] = "Englisch",
            [Keys.DefaultProjectLabel] = "Standardprojekt (.rfPrj)",
            [Keys.DefaultProjectHint] = "Neben name.rfPrj dürfen die Dateien name.deutsch.rfPrj und name.englisch.rfPrj liegen. Die zur Sprache des Berichts passende wird verwendet, wenn sie existiert.",
            [Keys.MemoryProjectLabel] = "Projekt, das den Speicher der Karte ausliest (.rfPrj)",
            [Keys.DatabaseHint] = "Zur Anmeldung wird das Windows-Konto der Person verwendet, die die Prüfung ausführt.",
            [Keys.DatabaseServerLabel] = "Server (Host oder Host\\Instanz)",
            [Keys.DatabaseNameLabel] = "Datenbank",
            [Keys.DatabaseTableLabel] = "Tabelle (mit oder ohne Schema)",
            [Keys.TestConnectionButton] = "Verbindung testen",
            [Keys.TextBlocksHint] = "Textbausteine können in die Anmerkungen eines Berichts eingefügt werden.",
            [Keys.TextBlockNameLabel] = "Name des Textbausteins",
            [Keys.TextBlockContentLabel] = "Einzufügender Text",
            [Keys.AddTextBlockButton] = "Hinzufügen",
            [Keys.RemoveTextBlockButton] = "Entfernen",
            [Keys.SaveButton] = "Speichern",
            [Keys.CancelButton] = "Abbrechen",
        };

        /// <summary>The keys of the English dictionary, which is the complete one.</summary>
        internal static IEnumerable<string> AllKeys => English.Keys;

        /// <summary>The keys of the German dictionary.</summary>
        internal static IEnumerable<string> GermanKeys => German.Keys;

        /// <summary>
        /// Whether a UI culture name (for example <c>de-DE</c>) selects German. Anything else, including a blank name,
        /// selects English.
        /// </summary>
        /// <param name="cultureName">The host's <c>IAppContext.UICultureName</c>.</param>
        /// <returns><c>true</c> for German.</returns>
        internal static bool IsGerman(string? cultureName) =>
            !string.IsNullOrWhiteSpace(cultureName)
            && cultureName.Trim().StartsWith("de", StringComparison.OrdinalIgnoreCase)
            && (cultureName.Trim().Length == 2 || cultureName.Trim()[2] == '-' || cultureName.Trim()[2] == '_');

        /// <summary>Looks a text up for a culture, falling back to English and, for an unknown key, to the key itself.</summary>
        /// <param name="key">One of <see cref="Keys"/>.</param>
        /// <param name="cultureName">The host's <c>IAppContext.UICultureName</c>.</param>
        /// <returns>The text.</returns>
        internal static string Get(string key, string? cultureName)
        {
            if (IsGerman(cultureName) && German.TryGetValue(key, out var german))
            {
                return german;
            }

            return English.TryGetValue(key, out var english) ? english : key;
        }
    }
}
