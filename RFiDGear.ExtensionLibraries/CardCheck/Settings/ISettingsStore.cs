using System;

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

        /// <summary>
        /// Changes the settings as one step: reads them, hands them to <paramref name="change"/> and writes what it
        /// returns. No other store for the same file (another window of the extension, another program) can read or
        /// write in between, so no change is lost to a stale copy. Use this, not <see cref="Load"/> followed by
        /// <see cref="Save"/>, whenever the new settings depend on the old ones.
        /// </summary>
        /// <param name="change">
        /// Builds the new settings from the current ones. Return the same object to change nothing, or a failure to
        /// stop; nothing is written in either case. It runs while the file is held, so it must be quick and must not call
        /// this store.
        /// </param>
        /// <returns>
        /// Success, or why nothing was written: the failure of <paramref name="change"/>, a file from a newer version, a
        /// file that cannot be read just now, or one that another program holds for too long.
        /// </returns>
        Result Update(Func<CardCheckSettings, Result<CardCheckSettings>> change);
    }
}
