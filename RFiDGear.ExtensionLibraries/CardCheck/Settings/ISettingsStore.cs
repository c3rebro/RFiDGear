namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>How <see cref="ISettingsStore.Load"/> got its settings.</summary>
    public enum SettingsLoadStatus
    {
        /// <summary>The settings file was read.</summary>
        Loaded,

        /// <summary>There is no settings file yet; these are the defaults.</summary>
        NotFound,

        /// <summary>
        /// The file was damaged. It was renamed next to the original (the name gets a <c>.corrupt-</c> suffix) so
        /// nothing is lost, and these are the defaults.
        /// </summary>
        Recovered,

        /// <summary>
        /// The file was written by a newer version of the assistant. It is left alone and these are the defaults;
        /// saving is refused so the newer settings are never overwritten.
        /// </summary>
        NewerSchema,

        /// <summary>The file exists but could not be read just now (another program holds it); these are the defaults.</summary>
        Unreadable,
    }

    /// <summary>The settings and how they were obtained.</summary>
    /// <param name="Settings">The settings; the defaults unless <paramref name="Status"/> is <see cref="SettingsLoadStatus.Loaded"/>.</param>
    /// <param name="Status">How they were obtained.</param>
    public sealed record SettingsLoad(CardCheckSettings Settings, SettingsLoadStatus Status);

    /// <summary>
    /// Keeps the settings of the Card Check Assistant in one JSON file below the user's application data. Nothing
    /// here throws because of the file: a missing, damaged or unreadable file gives defaults, a failed save gives a
    /// <see cref="Failure"/>.
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>
        /// Reads the settings. Values are tidied (a missing value becomes empty, blanks around names and paths are
        /// removed) and an unknown language falls back to German, so one bad entry does not discard the file.
        /// </summary>
        /// <returns>The settings and how they were obtained; never <c>null</c>.</returns>
        SettingsLoad Load();

        /// <summary>
        /// Writes the settings. The file is written next to its final place and then swapped in, so a crash or a
        /// full disk leaves the old file whole. A file written by a newer version is never overwritten.
        /// </summary>
        /// <param name="settings">The settings to keep.</param>
        /// <returns>Success, or why the settings could not be kept.</returns>
        Result Save(CardCheckSettings settings);
    }
}
