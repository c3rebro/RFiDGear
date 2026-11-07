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
            var load = _store.Load();

            if (string.Equals(load.Settings.LastProjectFolder, folder, System.StringComparison.Ordinal))
            {
                return;
            }

            // A file from a newer version, or one that cannot be read just now, comes back from Load as the defaults, but
            // Save looks at the file itself and refuses to write over it, so one remembered folder never costs the rest.
            _store.Save(load.Settings with { LastProjectFolder = folder });
        }
    }
}
