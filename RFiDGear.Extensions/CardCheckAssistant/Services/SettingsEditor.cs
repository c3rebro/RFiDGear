using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>The settings UI's access to the settings file and to the order database, over the support library.</summary>
    internal sealed class SettingsEditor : IAssistantSettingsEditor
    {
        private readonly ISettingsStore _store;
        private readonly Func<DatabaseSettings, IOrderStore> _createOrderStore;

        /// <summary>Creates the editor.</summary>
        /// <param name="store">The settings store; it never throws because of the file.</param>
        /// <param name="createOrderStore">Makes the access to the order table for the values to try; replaced by a test.</param>
        public SettingsEditor(ISettingsStore store, Func<DatabaseSettings, IOrderStore> createOrderStore)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _createOrderStore = createOrderStore ?? throw new ArgumentNullException(nameof(createOrderStore));
        }

        /// <inheritdoc/>
        public SettingsEditorLoad Load()
        {
            var load = _store.Load();

            return new SettingsEditorLoad(ToData(load.Settings), ProblemOf(load.Status));
        }

        /// <inheritdoc/>
        public SettingsSaveOutcome Save(SettingsData baseline, SettingsData edited)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(nameof(baseline));
            }

            if (edited == null)
            {
                throw new ArgumentNullException(nameof(edited));
            }

            // One step with reading, and a value is only written where the operator changed it: what the editor does not
            // show (the folder the picker was last in, the marker of an earlier import) and anything another window changed
            // since this one loaded are carried over, not written over with a stale copy.
            var result = _store.Update(current => Result<CardCheckSettings>.Ok(current with
            {
                Report = current.Report with
                {
                    OutputPath = Pick(baseline.OutputFolder, edited.OutputFolder, current.Report.OutputPath),
                    CreateSubdirectory = Pick(baseline.OneFolderPerJob, edited.OneFolderPerJob, current.Report.CreateSubdirectory),
                    RemoveTemporaryReports = Pick(baseline.RemoveTemporaryReports, edited.RemoveTemporaryReports, current.Report.RemoveTemporaryReports),
                    DefaultLanguage = baseline.ReportLanguage == edited.ReportLanguage
                        ? current.Report.DefaultLanguage
                        : edited.ReportLanguage == ReportLanguageChoice.English ? ReportLanguage.English : ReportLanguage.German,
                },
                DefaultProjectPath = Pick(baseline.DefaultProjectPath, edited.DefaultProjectPath, current.DefaultProjectPath),
                CustomProjectPath = Pick(baseline.MemoryProjectPath, edited.MemoryProjectPath, current.CustomProjectPath),
                Database = current.Database with
                {
                    Server = Pick(baseline.Database.Server, edited.Database.Server, current.Database.Server),
                    Database = Pick(baseline.Database.Database, edited.Database.Database, current.Database.Database),
                    Table = Pick(baseline.Database.Table, edited.Database.Table, current.Database.Table),
                },
                TextTemplates = baseline.TextBlocks.SequenceEqual(edited.TextBlocks)
                    ? current.TextTemplates
                    : edited.TextBlocks.Select(block => new TextTemplate { Name = block.Name, Content = block.Content }).ToList(),
            }));

            return result.IsSuccess ? SettingsSaveOutcome.Saved : OutcomeOf(result.Failure!.Code);
        }

        // The edited value if the operator changed it, otherwise whatever the settings hold now.
        private static T Pick<T>(T baseline, T edited, T current) => EqualityComparer<T>.Default.Equals(baseline, edited) ? current : edited;

        /// <inheritdoc/>
        public async Task<ConnectionTestOutcome> TestConnectionAsync(DatabaseData database, CancellationToken cancellationToken)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            var store = _createOrderStore(new DatabaseSettings { Server = database.Server, Database = database.Database, Table = database.Table });
            var result = await store.TestConnectionAsync(cancellationToken).ConfigureAwait(false);

            return result.IsSuccess ? ConnectionTestOutcome.Succeeded : OutcomeOf(result.Failure!);
        }

        private static SettingsData ToData(CardCheckSettings settings) => new SettingsData
        {
            OutputFolder = settings.Report.OutputPath,
            OneFolderPerJob = settings.Report.CreateSubdirectory,
            RemoveTemporaryReports = settings.Report.RemoveTemporaryReports,
            ReportLanguage = settings.Report.DefaultLanguage == ReportLanguage.English ? ReportLanguageChoice.English : ReportLanguageChoice.German,
            DefaultProjectPath = settings.DefaultProjectPath,
            MemoryProjectPath = settings.CustomProjectPath,
            Database = new DatabaseData(settings.Database.Server, settings.Database.Database, settings.Database.Table),
            TextBlocks = settings.TextTemplates.Select(template => new TextBlockData(template.Name, template.Content)).ToList(),
        };

        private static SettingsProblem ProblemOf(SettingsLoadStatus status)
        {
            switch (status)
            {
                case SettingsLoadStatus.NewerSchema:
                    return SettingsProblem.NewerVersion;
                case SettingsLoadStatus.Unreadable:
                    return SettingsProblem.Unreadable;
                case SettingsLoadStatus.Recovered:
                    return SettingsProblem.DamagedFileSetAside;
                default:
                    return SettingsProblem.None;
            }
        }

        private static SettingsSaveOutcome OutcomeOf(string code)
        {
            switch (code)
            {
                case "settings.newerSchema":
                    return SettingsSaveOutcome.NewerVersion;
                case "settings.unreadable":
                case "settings.save.unreadable":
                    return SettingsSaveOutcome.Unreadable;
                case "settings.busy":
                    return SettingsSaveOutcome.Busy;
                case "settings.save.tooLarge":
                    return SettingsSaveOutcome.TooLarge;
                default:
                    return SettingsSaveOutcome.Failed;
            }
        }

        private static ConnectionTestOutcome OutcomeOf(Failure failure)
        {
            switch (failure.Code)
            {
                case "orders.notConfigured":
                    return ConnectionTestOutcome.NotConfigured;
                case "orders.table.invalid":
                    return ConnectionTestOutcome.TableInvalid;
                case "orders.connection.invalid":
                    return ConnectionTestOutcome.ConnectionInvalid;
                case "orders.unreachable":
                    return ConnectionTestOutcome.Unreachable;
                case "orders.denied":
                    return ConnectionTestOutcome.AccessDenied;
                case "orders.schema":
                    return ConnectionTestOutcome.SchemaMismatch;
                case "orders.timeout":
                    return ConnectionTestOutcome.Timeout;
                case "orders.driver.missing":
                    return ConnectionTestOutcome.DriverMissing;
                case "orders.cancelled":
                    return ConnectionTestOutcome.Cancelled;
                default:
                    return ConnectionTestOutcome.Failed;
            }
        }
    }
}
