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
