#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.Models;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// What the task view models do with their list of <see cref="KeyBindingEntry"/>. The list is only ever replaced as a
    /// whole, by a copy: the dialog that edits a task copies every public property by reference, and a list shared
    /// between the task and its edit copy would let a cancelled edit change the task.
    /// </summary>
    internal static class KeyBindingList
    {
        /// <summary>Makes an independent copy. Missing lists and missing entries give an empty list and no entry.</summary>
        /// <param name="entries">The entries to copy; may be <c>null</c>.</param>
        /// <returns>A new array of new entries.</returns>
        internal static KeyBindingEntry[] Copy(IEnumerable<KeyBindingEntry>? entries) =>
            entries == null
                ? Array.Empty<KeyBindingEntry>()
                : entries.Where(entry => entry != null).Select(entry => entry.Clone()).ToArray();

        /// <summary>Finds the entry of a slot. The slot ids are compared ordinally, so case counts.</summary>
        /// <param name="entries">The entries of a task.</param>
        /// <param name="slotId">The slot.</param>
        /// <returns>The first entry for the slot, or <c>null</c>.</returns>
        internal static KeyBindingEntry? Find(IEnumerable<KeyBindingEntry> entries, string slotId) =>
            entries.FirstOrDefault(entry => string.Equals(entry.Slot, slotId, StringComparison.Ordinal));

        /// <summary>
        /// Returns whether a slot has an entry. Whether the entry is usable does not matter: a slot that has one is bound,
        /// and a broken binding must fail the task, not fall back to an embedded key.
        /// </summary>
        /// <param name="entries">The entries of a task.</param>
        /// <param name="slotId">The slot.</param>
        /// <returns><c>true</c> when the slot has an entry.</returns>
        internal static bool IsBound(IEnumerable<KeyBindingEntry> entries, string slotId) => Find(entries, slotId) != null;

        /// <summary>
        /// Makes a list in which a slot has the given binding and no other: an entry for the slot is replaced in its place,
        /// others (a duplicate that a damaged project may hold) are dropped, and a slot without an entry gets one at the end.
        /// The result is a copy; so is the entry that goes into it.
        /// </summary>
        /// <param name="entries">The entries of a task.</param>
        /// <param name="entry">The binding of the slot.</param>
        /// <returns>The new list.</returns>
        internal static KeyBindingEntry[] With(IEnumerable<KeyBindingEntry> entries, KeyBindingEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            var result = new List<KeyBindingEntry>();
            var placed = false;

            foreach (var existing in Copy(entries))
            {
                if (!string.Equals(existing.Slot, entry.Slot, StringComparison.Ordinal))
                {
                    result.Add(existing);
                }
                else if (!placed)
                {
                    result.Add(entry.Clone());
                    placed = true;
                }
            }

            if (!placed)
            {
                result.Add(entry.Clone());
            }

            return result.ToArray();
        }

        /// <summary>Makes a copy of a list without the entries of a slot.</summary>
        /// <param name="entries">The entries of a task.</param>
        /// <param name="slotId">The slot.</param>
        /// <returns>The new list.</returns>
        internal static KeyBindingEntry[] Without(IEnumerable<KeyBindingEntry> entries, string slotId) =>
            Copy(entries).Where(entry => !string.Equals(entry.Slot, slotId, StringComparison.Ordinal)).ToArray();
    }
}
