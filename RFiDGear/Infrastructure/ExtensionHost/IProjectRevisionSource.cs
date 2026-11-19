#nullable enable

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// What the external control bridge needs of the project host to tell clients in which state the loaded project is. It is
    /// internal and is not part of the host contract: a host that does not implement it simply does not offer the capability.
    /// </summary>
    internal interface IProjectRevisionSource
    {
        /// <summary>
        /// Gets the revision of the project as it is now, changes that were not saved included (see <see cref="ProjectRevision"/>).
        /// It reads the tasks, so it is called on the thread of the main window.
        /// </summary>
        /// <returns>The revision, or <see langword="null"/> when no project is loaded or the revision could not be made.</returns>
        string? GetProjectRevision();
    }
}
