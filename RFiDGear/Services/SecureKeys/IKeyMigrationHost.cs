#nullable enable
using System.Collections.Generic;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// What the migration of a project to a key store needs from a task: which of its keys the project file holds as text,
    /// and the keys themselves. Both setup view models implement it. A task is moved with <see cref="IKeySlotHost.BindKeySlot"/>.
    /// </summary>
    internal interface IKeyMigrationHost : IKeySlotHost
    {
        /// <summary>
        /// Returns the slots whose key the project file holds as text: a key is typed into the task, it is not bound, and the
        /// task would write it. A copy that the task keeps only for its own convenience, and does not write, is not among them.
        /// </summary>
        /// <returns>The slot ids; <see cref="SecureKeySlots"/> has them.</returns>
        IReadOnlyList<string> EmbeddedKeySlots();

        /// <summary>Returns the key that is typed into the task for a slot, as the project holds it.</summary>
        /// <param name="slotId">The slot id.</param>
        /// <returns>The key as text; empty when there is none.</returns>
        string GetEmbeddedKey(string slotId);
    }
}
