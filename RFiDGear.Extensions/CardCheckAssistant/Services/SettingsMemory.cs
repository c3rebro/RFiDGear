using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>The page's memory, kept in the support library's settings file.</summary>
    internal sealed class SettingsMemory : IAssistantSettings
    {
        private readonly ISettingsStore _store;

        /// <summary>Creates the memory.</summary>
        /// <param name="store">The settings store; it never throws because of the file.</param>
        public SettingsMemory(ISettingsStore store)
        {
            _store = store ?? throw new System.ArgumentNullException(nameof(store));
        }

        /// <inheritdoc/>
        public AssistantDefaults Load()
        {
            var load = _store.Load();

            return new AssistantDefaults(load.Settings.DefaultProjectPath, load.Settings.LastProjectFolder);
        }

        /// <inheritdoc/>
        public void RememberProjectFolder(string folder)
        {
            // Read, change and write in one step: the settings may have been changed meanwhile by an import, or by another
            // window, and this must not write a stale copy over that. A file from a newer version, or one that cannot be
            // read just now, is refused by the store, so one remembered folder never costs the rest.
            _store.Update(settings => Result<CardCheckSettings>.Ok(
                string.Equals(settings.LastProjectFolder, folder, System.StringComparison.Ordinal)
                    ? settings
                    : settings with { LastProjectFolder = folder }));
        }
    }
}
