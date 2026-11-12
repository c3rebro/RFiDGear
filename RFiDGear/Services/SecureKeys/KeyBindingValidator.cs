#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RFiDGear.Models;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>What is wrong with a key binding.</summary>
    internal enum KeyBindingProblem
    {
        /// <summary>The slot is not one of the slots of the task.</summary>
        UnknownSlot = 1,

        /// <summary>The slot has more than one entry, so it is not clear which one is meant.</summary>
        DuplicateSlot = 2,

        /// <summary>The provider id is empty or not of the valid shape.</summary>
        InvalidProviderId = 3,

        /// <summary>The reference is empty or not of the valid shape.</summary>
        InvalidReference = 4,
    }

    /// <summary>A problem with the binding of one slot. Holds no reference, so it may be logged.</summary>
    /// <param name="Slot">The slot as written in the entry.</param>
    /// <param name="Problem">What is wrong.</param>
    internal sealed record KeyBindingIssue(string Slot, KeyBindingProblem Problem);

    /// <summary>
    /// Checks the key bindings of a task. A binding with a problem is never treated as "no binding": the task that has it
    /// fails, so a typo cannot turn into a silent fall back to an embedded key.
    /// </summary>
    internal static class KeyBindingValidator
    {
        /// <summary>Checks every entry. An entry with no problem is not listed.</summary>
        /// <param name="entries">The entries of a task.</param>
        /// <param name="knownSlots">The slots the task has; see <see cref="SecureKeySlots"/>.</param>
        /// <returns>
        /// One issue per entry that has a problem, in the order of the entries. A slot that appears more than once is
        /// reported for each of its entries.
        /// </returns>
        internal static IReadOnlyList<KeyBindingIssue> Check(IEnumerable<KeyBindingEntry> entries, IReadOnlyCollection<string> knownSlots)
        {
            var all = entries.ToList();
            var duplicated = all
                .GroupBy(entry => entry.Slot, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
            var issues = new List<KeyBindingIssue>();

            foreach (var entry in all)
            {
                var problem = ProblemOf(entry, knownSlots, duplicated);

                if (problem != null)
                {
                    issues.Add(new KeyBindingIssue(entry.Slot, problem.Value));
                }
            }

            return issues;
        }

        private static KeyBindingProblem? ProblemOf(KeyBindingEntry entry, IReadOnlyCollection<string> knownSlots, ISet<string> duplicated)
        {
            if (!knownSlots.Contains(entry.Slot, StringComparer.Ordinal))
            {
                return KeyBindingProblem.UnknownSlot;
            }

            if (duplicated.Contains(entry.Slot))
            {
                return KeyBindingProblem.DuplicateSlot;
            }

            if (!SecureKeySyntax.IsValidProviderId(entry.ProviderId))
            {
                return KeyBindingProblem.InvalidProviderId;
            }

            return SecureKeySyntax.IsValidReference(entry.Reference) ? null : KeyBindingProblem.InvalidReference;
        }
    }
}
