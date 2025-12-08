using System.Globalization;
using System.Threading.Tasks;
using RFiDGear.Model;

namespace RFiDGear.Services
{
    /// <summary>
    /// Provides an abstraction for reading and persisting application settings
    /// that are required to initialize the main window.
    /// </summary>
    public interface ISettingsBootstrapper
    {
        /// <summary>
        /// Loads persisted settings and returns the values required to initialize
        /// the main window state.
        /// </summary>
        /// <returns>A snapshot of the current settings.</returns>
        Task<SettingsBootstrapResult> LoadAsync();

        /// <summary>
        /// Persists settings changes.
        /// </summary>
        /// <param name="updateAction">Callback to mutate the persisted <see cref="DefaultSpecification"/>.</param>
        Task SaveAsync(System.Action<DefaultSpecification> updateAction);
    }

    /// <summary>
    /// Represents the data required to initialize the main window from persisted settings.
    /// </summary>
    public class SettingsBootstrapResult
    {
        public string CurrentReaderName { get; set; }

        public ReaderTypes DefaultReaderProvider { get; set; }

        public int PortNumber { get; set; }

        public bool AutoLoadLastUsedProject { get; set; }

        public string LastUsedProjectPath { get; set; }

        public CultureInfo Culture { get; set; }

        public DefaultSpecification DefaultSpecification { get; set; }
    }
}
