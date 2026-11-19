#nullable enable
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>
        /// Loads a project like <see cref="RFiDGear.Contracts.IProjectHost.LoadAsync"/> does, and gives the revision of the project
        /// as it was committed: taken in the same action that puts the project in place, so that an edit that follows is a change of
        /// the project that was loaded and not part of the answer.
        /// </summary>
        /// <param name="path">The path of the project.</param>
        /// <param name="ct">Cancels the load.</param>
        /// <returns>The result of the load, and the revision when it succeeded (<see langword="null"/> when the revision could not be made).</returns>
        Task<(RFiDGear.Contracts.ProjectOpenResult Result, string? Revision)> LoadWithRevisionAsync(string path, CancellationToken ct = default);
    }
}
